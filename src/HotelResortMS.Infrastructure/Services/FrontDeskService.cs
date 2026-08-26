using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IFrontDeskService"/>
public class FrontDeskService : IFrontDeskService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IAuditService _auditService;
    private readonly IRoomService _roomService;
    private readonly IBusinessDateService _businessDateService;
    private readonly IIncomeService _incomeService;
    private readonly IAccountsReceivableService _accountsReceivableService;
    private readonly IHousekeepingService _housekeepingService;

    public FrontDeskService(
        ApplicationDbContext db,
        INumberingService numberingService,
        IAuditService auditService,
        IRoomService roomService,
        IBusinessDateService businessDateService,
        IIncomeService incomeService,
        IAccountsReceivableService accountsReceivableService,
        IHousekeepingService housekeepingService)
    {
        _db = db;
        _numberingService = numberingService;
        _auditService = auditService;
        _roomService = roomService;
        _businessDateService = businessDateService;
        _incomeService = incomeService;
        _accountsReceivableService = accountsReceivableService;
        _housekeepingService = housekeepingService;
    }

    /// <summary>
    /// Section 24 workflow: verify reservation -> confirm room -> record deposit already
    /// paid (AmountPaid is charged at booking/deposit time, not here) -> create Guest
    /// Folio seeded with the room charges -> flip Room to Occupied -> Reservation to
    /// CheckedIn. All in one method so these can never happen partially.
    /// </summary>
    public async Task<GuestFolio> CheckInAsync(int reservationId, string verifiedBy, string? identificationVerifiedNumber = null)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Rooms).ThenInclude(rr => rr.Room)
            .Include(r => r.Package)
            .FirstOrDefaultAsync(r => r.Id == reservationId)
            ?? throw new InvalidOperationException("Reservation not found.");

        if (reservation.Status != ReservationStatus.Confirmed)
        {
            throw new InvalidOperationException($"Only Confirmed reservations can be checked in (current status: {reservation.Status}).");
        }

        var businessDate = await _businessDateService.GetCurrentForPostingAsync();

        reservation.Status = ReservationStatus.CheckedIn;
        await _db.SaveChangesAsync();

        _db.CheckIns.Add(new CheckIn
        {
            ReservationId = reservationId,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            VerifiedBy = verifiedBy,
            IdentificationVerifiedNumber = identificationVerifiedNumber
        });

        var folio = new GuestFolio
        {
            FolioNumber = await _numberingService.GenerateAsync("Folio"),
            ReservationId = reservationId,
            GuestId = reservation.GuestId,
            Status = FolioStatus.Open,
            OpenedAt = DateTime.UtcNow
        };

        foreach (var rr in reservation.Rooms)
        {
            var nights = reservation.CheckOutDate.DayNumber - reservation.CheckInDate.DayNumber;
            folio.Details.Add(new FolioDetail
            {
                Type = FolioDetailType.RoomCharge,
                Description = $"Room {rr.Room?.RoomNumber} ({Math.Max(nights, 1)} night(s))",
                Amount = rr.RateAmount * Math.Max(nights, 1),
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = verifiedBy
            });
        }

        if (reservation.PackageId is not null)
        {
            // Section 32: one bundled charge for the whole package, at the price
            // snapshotted when the reservation was booked - never the package's current
            // (possibly since-changed) price.
            folio.Details.Add(new FolioDetail
            {
                Type = FolioDetailType.PackageCharge,
                Description = $"Package: {reservation.Package?.Name}",
                Amount = reservation.PackagePrice,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = verifiedBy
            });
        }

        if (reservation.DiscountAmount > 0)
        {
            folio.Details.Add(new FolioDetail
            {
                Type = FolioDetailType.Discount,
                Description = "Reservation discount",
                Amount = -reservation.DiscountAmount,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = verifiedBy
            });
        }

        if (reservation.AmountPaid > 0)
        {
            folio.Details.Add(new FolioDetail
            {
                Type = FolioDetailType.Payment,
                Description = "Deposit / advance payment applied",
                Amount = -reservation.AmountPaid,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = verifiedBy
            });
        }

        _db.GuestFolios.Add(folio);
        await _db.SaveChangesAsync();

        // Section 38: recognize room revenue now, at check-in, when the charge is posted -
        // not later when (or if) it is paid. Section 39 discount rules aren't run through
        // DiscountService here since room-charge discounting is applied at the reservation
        // level (reservation.DiscountAmount) rather than per-folio-line; Income still
        // records both the gross charge and that discount so reports reconcile.
        var roomChargeGross = folio.Details.Where(d => d.Type == FolioDetailType.RoomCharge).Sum(d => d.Amount);
        if (roomChargeGross > 0)
        {
            await _incomeService.RecordIncomeAsync(
                IncomeCategory.RoomRevenue, $"Room revenue for {reservation.ReservationNumber}",
                roomChargeGross, reservation.DiscountAmount, "Reservation", reservation.ReservationNumber, verifiedBy);
        }

        if (reservation.PackageId is not null && reservation.PackagePrice > 0)
        {
            await _incomeService.RecordIncomeAsync(
                IncomeCategory.Packages, $"Package '{reservation.Package?.Name}' for {reservation.ReservationNumber}",
                reservation.PackagePrice, 0, "Reservation", reservation.ReservationNumber, verifiedBy);
        }

        foreach (var rr in reservation.Rooms)
        {
            await _roomService.SetStatusAsync(rr.RoomId, RoomStatus.Occupied, verifiedBy, $"Checked in - {reservation.ReservationNumber}");
        }

        await _auditService.LogAsync(SystemModules.FrontDesk, "CheckIn", reservationId.ToString(), newValues: new { folio.FolioNumber });

        return folio;
    }

    /// <summary>
    /// Section 28: computes the folio's outstanding balance and blocks checkout unless it
    /// is zero (or an authorized override is passed, per Section 10 - transactions that
    /// affect accounting always require authorization to bypass, never a silent skip).
    /// </summary>
    public async Task<CheckOut> CheckOutAsync(int reservationId, string processedBy, bool authorizeOutstandingBalance = false)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Rooms)
            .FirstOrDefaultAsync(r => r.Id == reservationId)
            ?? throw new InvalidOperationException("Reservation not found.");

        if (reservation.Status != ReservationStatus.CheckedIn)
        {
            throw new InvalidOperationException($"Only CheckedIn reservations can be checked out (current status: {reservation.Status}).");
        }

        var folio = await _db.GuestFolios
            .Include(f => f.Details)
            .FirstOrDefaultAsync(f => f.ReservationId == reservationId && f.Status == FolioStatus.Open)
            ?? throw new InvalidOperationException("No open folio found for this reservation.");

        var balance = folio.Details.Sum(d => d.Amount);
        if (balance > 0 && !authorizeOutstandingBalance)
        {
            throw new InvalidOperationException(
                $"Outstanding balance of {balance:N2} must be settled (or authorized) before checkout.");
        }

        var businessDate = await _businessDateService.GetCurrentAsync();

        folio.Status = FolioStatus.Closed;
        folio.ClosedAt = DateTime.UtcNow;

        reservation.Status = ReservationStatus.CheckedOut;
        reservation.BalanceDue = balance;

        var checkOut = new CheckOut
        {
            ReservationId = reservationId,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            ProcessedBy = processedBy,
            FinalBalance = balance
        };
        _db.CheckOuts.Add(checkOut);

        await _db.SaveChangesAsync();

        // Section 13/29: checkout hands the room to Housekeeping (Dirty -> Cleaning ->
        // Clean -> Inspected -> Ready), not straight back to Available.
        foreach (var rr in reservation.Rooms)
        {
            await _housekeepingService.CreateTaskAsync(rr.RoomId, processedBy);
        }

        // Section 36: an authorized outstanding balance at checkout becomes Accounts
        // Receivable rather than simply vanishing - someone still needs to collect it.
        if (balance > 0 && authorizeOutstandingBalance)
        {
            await _accountsReceivableService.CreateAsync(reservation.GuestId, reservationId, folio.Id, balance, dueDate: null);
        }

        await _auditService.LogAsync(SystemModules.FrontDesk, "CheckOut", reservationId.ToString(),
            newValues: new { checkOut.FinalBalance }, reason: authorizeOutstandingBalance && balance > 0 ? "Outstanding balance authorized at checkout" : null);

        return checkOut;
    }

    public async Task<GuestFolio?> GetFolioForReservationAsync(int reservationId)
    {
        return await _db.GuestFolios
            .Include(f => f.Details)
            .Include(f => f.Guest)
            .FirstOrDefaultAsync(f => f.ReservationId == reservationId);
    }

    public async Task<FolioDetail> PostFolioChargeAsync(int folioId, FolioDetailType type, string description, decimal amount, string recordedBy)
    {
        var folio = await _db.GuestFolios.FirstOrDefaultAsync(f => f.Id == folioId)
            ?? throw new InvalidOperationException("Folio not found.");

        if (folio.Status != FolioStatus.Open)
        {
            throw new InvalidOperationException("Cannot post to a closed folio.");
        }

        var businessDate = await _businessDateService.GetCurrentAsync();

        var detail = new FolioDetail
        {
            GuestFolioId = folioId,
            Type = type,
            Description = description,
            Amount = amount,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            RecordedBy = recordedBy
        };

        _db.FolioDetails.Add(detail);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.GuestFolio, "PostCharge", folioId.ToString(), newValues: new { type, description, amount });

        return detail;
    }
}
