using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>Section 13: the single place a Room's Status changes, so Reservation/Check-In/
/// Check-Out/Housekeeping/Maintenance can never leave it in an inconsistent state and every
/// transition is audited.</summary>
public interface IRoomService
{
    Task SetStatusAsync(int roomId, RoomStatus status, string changedBy, string? reason = null);
}
