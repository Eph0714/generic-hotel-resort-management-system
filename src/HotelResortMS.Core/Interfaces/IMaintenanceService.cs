using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>Section 30: maintenance work orders, driving Room.Status alongside the
/// request's own status (Section 13).</summary>
public interface IMaintenanceService
{
    Task<MaintenanceRequest> CreateRequestAsync(
        int categoryId, int? roomId, int? equipmentId, string description, string reportedBy);

    Task AssignAsync(int requestId, string assignedToUserId, string assignedBy);
    Task StartAsync(int requestId, string startedBy);

    /// <summary>Section 13/29/30: completing a request that was tied to a room hands that
    /// room to Housekeeping (Dirty, with a task auto-created) rather than straight back to
    /// Available - a maintenance visit is exactly the kind of thing that leaves a room
    /// needing a clean before the next guest.</summary>
    Task CompleteAsync(int requestId, string completedBy, decimal? cost = null, string? notes = null);

    Task CancelAsync(int requestId, string reason, string cancelledBy);
}
