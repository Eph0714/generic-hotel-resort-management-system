using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IReservationService"/>
public class ReservationService : IReservationService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IAuditService _auditService;
    private readonly IRoomService _roomService;

    public ReservationService(ApplicationDbContext db, INumberingService numberingService, IAuditService auditService, IRoomService roomService)
    {
        _db = db;
        _numberingService = numberingService;
        _auditService = auditService;
        _roomService = roomService;
    }

    /// <summary>
    /// Section 21 double-booking prevention: a room is unavailable for [checkIn, checkOut)
    /// if any *active* reservation (Confirmed, CheckedIn, or Pending awaiting confirmation)
    /// has a room-stay whose own [CheckInDate, CheckOutDate) range overlaps it. Standard
    /// interval-overlap test: StartA < EndB AND StartB < EndA.
    /// </summary>
    public async Task<bool> IsRoomAvailableAsync(int roomId, DateOnly checkIn, DateOnly checkOut, int? excludeReservationId = null)
    {
        var conflicting = await _db.ReservationRooms
            .Where(rr => rr.RoomId == roomId)
            .Where(rr => rr.Reservation!.Status == ReservationStatus.Pending
                      || rr.Reservation!.Status == ReservationStatus.Confirmed
                      || rr.Reservation!.Status == ReservationStatus.CheckedIn)
            .Where(rr => excludeReservationId == null || rr.ReservationId != excludeReservationId)
            .Where(rr => rr.Reservation!.CheckInDate < checkOut && checkIn < rr.Reservation!.CheckOutDate)
            .AnyAsync();

        return !conflicting;
    }

    public async Task<List<Room>> GetAvailableRoomsAsync(DateOnly checkIn, DateOnly checkOut, int? roomTypeId = null)
    {
        var rooms = await _db.Rooms
            .Include(r => r.RoomType)
            .Where(r => r.IsActive && !r.IsDeleted && r.Status != RoomStatus.OutOfService)
            .Where(r => roomTypeId == null || r.RoomTypeId == roomTypeId)
            .ToListAsync();

        var available = new List<Room>();
        foreach (var room in rooms)
        {
            if (await IsRoomAvailableAsync(room.Id, checkIn, checkOut))
            {
                available.Add(room);
            }
        }
        return available;
    }

    /// <summary>
    /// Wraps the whole booking in a DB transaction and re-checks availability for every
    /// requested room *inside* that transaction (Section 21/53) - this is what closes the
    /// race window between "checked available" and "inserted the booking" for two
    /// concurrent requests targeting the same room/date range.
    /// </summary>
    public async Task<Reservation> CreateReservationAsync(Reservation reservation, IEnumerable<int> roomIds)
    {
        var roomIdList = roomIds.ToList();
        if (roomIdList.Count == 0)
        {
            throw new InvalidOperationException("A reservation must include at least one room.");
        }

        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var roomId in roomIdList)
            {
                if (!await IsRoomAvailableAsync(roomId, reservation.CheckInDate, reservation.CheckOutDate))
                {
                    var room = await _db.Rooms.FindAsync(roomId);
                    throw new InvalidOperationException(
                        $"Room {room?.RoomNumber ?? roomId.ToString()} is not available for the selected dates.");
                }
            }

            reservation.ReservationNumber = await _numberingService.GenerateAsync("Reservation");
            reservation.ReservationDate = DateTime.UtcNow;
            reservation.Status = ReservationStatus.Confirmed;

            decimal total = 0;
            foreach (var roomId in roomIdList)
            {
                var room = await _db.Rooms.Include(r => r.RoomType).FirstAsync(r => r.Id == roomId);
                // Snapshot the rate in effect right now onto the reservation line - later
                // changes to RoomType/Room rates must never alter this historical charge
                // (Section 15).
                var rate = room.RegularRateOverride ?? room.RoomType!.RegularRate;
                var nights = reservation.CheckOutDate.DayNumber - reservation.CheckInDate.DayNumber;
                total += rate * Math.Max(nights, 1);

                reservation.Rooms.Add(new ReservationRoom
                {
                    RoomId = roomId,
                    RateAmount = rate,
                    RateType = "Regular"
                });
            }

            // Section 32: fold in the selected Package's price, snapshotted now so a later
            // edit to the Package's master price never changes an already-booked total.
            if (reservation.PackageId is not null)
            {
                var package = await _db.Packages.FindAsync(reservation.PackageId.Value)
                    ?? throw new InvalidOperationException("Selected package not found.");
                reservation.PackagePrice = package.Price;
                total += package.Price;
            }

            reservation.TotalAmount = total;
            reservation.BalanceDue = total - reservation.DiscountAmount - reservation.AmountPaid;

            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync();

            // Reserved (not yet Occupied - that happens at Check-In) so the Room Status
            // Board reflects the booking immediately (Section 13/20).
            foreach (var roomId in roomIdList)
            {
                await _roomService.SetStatusAsync(roomId, RoomStatus.Reserved, "System (Reservation)", $"Reserved by {reservation.ReservationNumber}");
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        await _auditService.LogAsync(SystemModules.Reservations, "Create", reservation.Id.ToString(),
            newValues: new { reservation.ReservationNumber, reservation.CheckInDate, reservation.CheckOutDate, RoomIds = roomIdList });

        return reservation;
    }

    // Section 10/23: reservations are never hard-deleted - cancellation is a status
    // transition with a reason, applying whatever CancellationPolicy the reservation
    // carries (Section 23) so the fee/forfeiture is computed the same way every time.
    public async Task CancelReservationAsync(int reservationId, string reason, string cancelledBy)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Rooms)
            .Include(r => r.CancellationPolicy)
            .FirstOrDefaultAsync(r => r.Id == reservationId)
            ?? throw new InvalidOperationException("Reservation not found.");

        if (reservation.Status is ReservationStatus.CheckedOut or ReservationStatus.Cancelled)
        {
            throw new InvalidOperationException($"Reservation is already {reservation.Status} and cannot be cancelled.");
        }

        // Section 23: no policy attached means free cancellation (existing reservations
        // from before Phase 6, or a hotel that simply hasn't configured one, are never
        // charged a fee they never agreed to).
        var fee = 0m;
        var policy = reservation.CancellationPolicy;
        if (policy is not null)
        {
            var checkInDateTime = reservation.CheckInDate.ToDateTime(TimeOnly.MinValue);
            var hoursUntilCheckIn = (checkInDateTime - DateTime.UtcNow).TotalHours;

            // Cancelling at or before the policy's free-cancellation window costs nothing,
            // regardless of what the policy would otherwise charge.
            if (hoursUntilCheckIn < policy.HoursBeforeCheckIn)
            {
                fee = policy.Type switch
                {
                    CancellationPolicyType.FreeCancellation => 0m,
                    CancellationPolicyType.PartialRefund => reservation.AmountPaid * (1 - policy.FeePercentage / 100m),
                    CancellationPolicyType.CancellationFee => Math.Round(reservation.TotalAmount * policy.FeePercentage / 100m, 2),
                    CancellationPolicyType.DepositForfeiture or CancellationPolicyType.NoShowCharge => reservation.DepositRequired,
                    _ => 0m
                };
                // A fee can never exceed what was actually paid - there is nothing left to forfeit beyond that.
                fee = Math.Min(fee, reservation.AmountPaid);
            }
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancellationReason = reason;
        reservation.CancellationFeeAmount = fee;
        reservation.CancelledAt = DateTime.UtcNow;
        reservation.CancelledBy = cancelledBy;
        await _db.SaveChangesAsync();

        foreach (var rr in reservation.Rooms)
        {
            // Only release the room back to Available if it isn't currently occupied by
            // this same reservation's (already completed) stay - Confirmed/Pending
            // cancellations are the common case this handles.
            var room = await _db.Rooms.FindAsync(rr.RoomId);
            if (room is not null && room.Status == RoomStatus.Reserved)
            {
                await _roomService.SetStatusAsync(rr.RoomId, RoomStatus.Available, cancelledBy, $"Released by cancellation of {reservation.ReservationNumber}");
            }
        }

        await _auditService.LogAsync(SystemModules.Reservations, "Cancel", reservationId.ToString(),
            newValues: new { reservation.CancellationFeeAmount }, reason: reason);
    }
}
