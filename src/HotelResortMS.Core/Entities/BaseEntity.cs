namespace HotelResortMS.Core.Entities;

/// <summary>
/// Common audit/lifecycle fields shared by every master-data and transactional entity in the system.
/// Section 9 (Soft Delete and Archiving) and Section 56 (CRUD Audit Trail) require every record to
/// carry who/when metadata rather than silently disappearing on delete.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    /// <summary>Soft-delete flag. Records are hidden from normal views but never physically removed
    /// once they have any historical/financial relevance (Section 10).</summary>
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
}
