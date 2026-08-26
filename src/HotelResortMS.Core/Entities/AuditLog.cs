namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 46 (Audit Trail) / Section 56 (CRUD Audit Trail): one row per auditable action.
/// Written by AuditService, never edited afterwards, never deleted - financial and operational
/// records must never disappear without trace.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>Module the action happened in, e.g. "Rooms", "Reservations" (see SystemModules).</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Create, Update, Delete, Archive, Void, Refund, Login, Logout, etc.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Primary key (as string) of the record affected, when applicable.</summary>
    public string? RecordId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public string? Reason { get; set; }
    public string? IpAddress { get; set; }

    /// <summary>Actual wall-clock timestamp of the action.</summary>
    public DateTime ActualDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>The hotel's operating Business Date at the time of the action (Section 12) - may
    /// differ from ActualDateTime's calendar date for actions taken after midnight before Night Audit.</summary>
    public DateOnly BusinessDate { get; set; }
}
