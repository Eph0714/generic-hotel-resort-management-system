using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>
/// Section 20/21: the single place reservation availability, creation, and status
/// transitions happen. Centralizing this is what makes double-booking prevention (Section
/// 21) actually reliable - nothing else may create a Reservation/ReservationRoom directly.
/// </summary>
public interface IReservationService
{
    /// <summary>True if the room has no Confirmed/CheckedIn reservation overlapping the
    /// given date range (Section 21 - date/time overlap logic). Excludes rooms on
    /// reservations that are Cancelled/NoShow, and optionally excludes one reservation
    /// (for edit scenarios).</summary>
    Task<bool> IsRoomAvailableAsync(int roomId, DateOnly checkIn, DateOnly checkOut, int? excludeReservationId = null);

    Task<List<Room>> GetAvailableRoomsAsync(DateOnly checkIn, DateOnly checkOut, int? roomTypeId = null);

    /// <summary>Creates the reservation transactionally: re-validates availability for every
    /// requested room inside the same DB transaction that inserts the rows, so two
    /// concurrent booking requests for the same room/date range cannot both succeed
    /// (Section 21/53).</summary>
    Task<Reservation> CreateReservationAsync(Reservation reservation, IEnumerable<int> roomIds);

    Task CancelReservationAsync(int reservationId, string reason, string cancelledBy);
}
