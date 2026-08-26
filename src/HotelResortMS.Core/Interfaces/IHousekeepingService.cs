using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>
/// Section 29: the single place a housekeeping cycle moves forward, driving the Room's
/// own Status alongside the task's (Section 13). A failed inspection sends the room back
/// to Dirty on the same task row rather than creating a new one, so the full history of
/// who cleaned/inspected it stays on one record (Section 29: preserve completed records).
/// </summary>
public interface IHousekeepingService
{
    /// <summary>Creates a task for a room that needs cleaning - normally called
    /// automatically by FrontDeskService at Check-Out, but also usable for ad-hoc/periodic
    /// cleaning. Refuses if the room already has an open (non-Ready) task.</summary>
    Task<HousekeepingTask> CreateTaskAsync(int roomId, string createdBy, string? assignedToUserId = null);

    Task AssignAsync(int taskId, string assignedToUserId, string assignedBy);
    Task StartCleaningAsync(int taskId, string startedBy);
    Task CompleteCleaningAsync(int taskId, string completedBy, string? notes = null);

    /// <summary>Section 13: passing sets the room back to Available (ready for the next
    /// guest); failing sends the task (and the room) back to Dirty for another pass.</summary>
    Task InspectAsync(int taskId, string inspectedBy, bool passed, string? notes = null);
}
