using HotelResortMS.Core.Entities.Identity;

namespace HotelResortMS.Core.Entities;

public class MaintenanceCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g. "Electrical", "Plumbing", "HVAC"
    public string? Description { get; set; }
}

/// <summary>Section 30: a physical asset that can need maintenance - optionally tied to
/// the room/area it lives in.</summary>
public class Equipment : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? RoomId { get; set; }
    public Room? Room { get; set; }
}

public enum MaintenanceRequestStatus
{
    Open,
    Assigned,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>
/// Section 30: a maintenance work order. When RoomId is set, creating the request flips
/// that Room to Maintenance status immediately (Section 13: "Maintenance can automatically
/// change room/amenity status"); completing it hands the room to Housekeeping rather than
/// straight back to Available, since a maintenance visit is exactly the kind of thing that
/// leaves a room needing a clean before the next guest.
/// </summary>
public class MaintenanceRequest : BaseEntity
{
    public string RequestNumber { get; set; } = string.Empty;

    public int MaintenanceCategoryId { get; set; }
    public MaintenanceCategory? MaintenanceCategory { get; set; }

    public int? RoomId { get; set; }
    public Room? Room { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public string Description { get; set; } = string.Empty;
    public MaintenanceRequestStatus Status { get; set; } = MaintenanceRequestStatus.Open;

    public string? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }
    public DateTime? AssignedAt { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? Cost { get; set; }

    public string? CancelReason { get; set; }
    public string? Notes { get; set; }

    public string ReportedBy { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; }
    public DateOnly BusinessDate { get; set; }
}
