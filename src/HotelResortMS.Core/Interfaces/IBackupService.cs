using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>
/// Section 50: manual database backup/restore via the MySQL server's own dump/restore
/// tools, restricted to authorized administrators (enforced by the controller's
/// permission check, not by this service). Scheduled/automatic backup is a natural
/// follow-up once this manual path is proven - out of scope for this pass.
/// </summary>
public interface IBackupService
{
    Task<BackupRecord> CreateBackupAsync(string createdBy);

    /// <summary>Restores the database from a previously successful backup file. Destructive
    /// and irreversible - the caller must already have confirmed this with the user.</summary>
    Task RestoreAsync(int backupId, string restoredBy);

    Task<IReadOnlyList<BackupRecord>> GetHistoryAsync();

    Task<BackupRecord?> GetLastSuccessfulAsync();
}
