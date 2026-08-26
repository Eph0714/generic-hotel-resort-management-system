using HotelResortMS.Core.Entities.Identity;

namespace HotelResortMS.Core.Entities;

/// <summary>Mirrors the Housekeeping subset of Section 13's Room statuses - kept as its
/// own enum (rather than reusing RoomStatus directly) so a task's lifecycle can be
/// queried/reported on independently of whatever else the room is doing.</summary>
public enum HousekeepingStatus
{
    Dirty,
    Cleaning,
    Clean,
    Inspected,
    Ready
}

/// <summary>
/// Section 29: one cleaning cycle for a room, from "needs cleaning" through to
/// "inspected and ready for the next guest". HousekeepingService is the only place this
/// transitions - it also drives the Room's own Status (Section 13: "Room status must
/// automatically respond to ... Housekeeping"). Completed tasks are kept permanently
/// (Section 29: "Completed historical records should be preserved") rather than deleted;
/// a failed inspection reopens the same row rather than losing the history of who did what.
/// </summary>
public class HousekeepingTask
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public Room? Room { get; set; }

    public HousekeepingStatus Status { get; set; } = HousekeepingStatus.Dirty;

    public string? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }
    public DateTime? AssignedAt { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? InspectedByUserId { get; set; }
    public ApplicationUser? InspectedByUser { get; set; }
    public DateTime? InspectedAt { get; set; }

    /// <summary>Cleaning notes, and - on a failed inspection - what needs to be redone.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
