namespace HotelResortMS.Core.Entities;

public enum BackupStatus
{
    Success,
    Failed
}

/// <summary>
/// Section 50 (Backup and Restore): one row per backup attempt, kept permanently (never
/// deleted) so "Last Successful Backup" and the backup history list are always accurate
/// even after a failed attempt or a since-deleted backup file.
/// </summary>
public class BackupRecord
{
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public BackupStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? RestoredAt { get; set; }
    public string? RestoredBy { get; set; }
}
