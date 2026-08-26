using System.Diagnostics;
using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IBackupService"/>
public class BackupService : IBackupService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;
    private readonly string _connectionString;
    private readonly string _mysqlBinDirectory;
    private readonly string _backupDirectory;

    public BackupService(ApplicationDbContext db, IAuditService auditService, IConfiguration configuration)
    {
        _db = db;
        _auditService = auditService;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _mysqlBinDirectory = configuration["Backup:MySqlBinDirectory"] ?? @"C:\Program Files\MySQL\MySQL Server 8.4\bin";
        _backupDirectory = configuration["Backup:Directory"] ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Backups");
    }

    public async Task<BackupRecord> CreateBackupAsync(string createdBy)
    {
        Directory.CreateDirectory(_backupDirectory);

        var builder = new MySqlConnectionStringBuilder(_connectionString);
        var fileName = $"backup-{builder.Database}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.sql";
        var filePath = Path.Combine(_backupDirectory, fileName);

        var record = new BackupRecord
        {
            FileName = fileName,
            FilePath = filePath,
            StartedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Status = BackupStatus.Failed // flipped to Success below only if the dump actually succeeds
        };

        try
        {
            var mysqldump = Path.Combine(_mysqlBinDirectory, "mysqldump.exe");
            if (!File.Exists(mysqldump))
            {
                throw new InvalidOperationException($"mysqldump.exe not found at {mysqldump}. Check Backup:MySqlBinDirectory in appsettings.");
            }

            // Section 3/48: the password never appears on the command line (visible to
            // any other process/user on the machine via the process list) - it is passed
            // through the MYSQL_PWD environment variable of this child process only.
            var startInfo = new ProcessStartInfo
            {
                FileName = mysqldump,
                Arguments = $"--host={builder.Server} --port={builder.Port} --user={builder.UserID} " +
                            $"--routines --triggers --single-transaction {builder.Database}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.Environment["MYSQL_PWD"] = builder.Password;

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start mysqldump process.");

            await using (var outFile = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await process.StandardOutput.BaseStream.CopyToAsync(outFile);
            }
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"mysqldump exited with code {process.ExitCode}: {stderr}");
            }

            record.Status = BackupStatus.Success;
            record.SizeBytes = new FileInfo(filePath).Length;
        }
        catch (Exception ex)
        {
            record.Status = BackupStatus.Failed;
            record.ErrorMessage = ex.Message;
        }
        finally
        {
            record.CompletedAt = DateTime.UtcNow;
        }

        _db.BackupRecords.Add(record);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.BackupRestore, "Backup", record.Id.ToString(),
            newValues: new { record.FileName, record.Status });

        return record;
    }

    public async Task RestoreAsync(int backupId, string restoredBy)
    {
        var record = await _db.BackupRecords.FindAsync(backupId)
            ?? throw new InvalidOperationException("Backup record not found.");

        if (record.Status != BackupStatus.Success)
        {
            throw new InvalidOperationException("Only a successful backup can be restored from.");
        }
        if (!File.Exists(record.FilePath))
        {
            throw new InvalidOperationException($"Backup file no longer exists at {record.FilePath}.");
        }

        var builder = new MySqlConnectionStringBuilder(_connectionString);
        var mysqlCli = Path.Combine(_mysqlBinDirectory, "mysql.exe");
        if (!File.Exists(mysqlCli))
        {
            throw new InvalidOperationException($"mysql.exe not found at {mysqlCli}. Check Backup:MySqlBinDirectory in appsettings.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = mysqlCli,
            Arguments = $"--host={builder.Server} --port={builder.Port} --user={builder.UserID} {builder.Database}",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["MYSQL_PWD"] = builder.Password;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start mysql restore process.");

        await using (var inFile = new FileStream(record.FilePath, FileMode.Open, FileAccess.Read))
        {
            await inFile.CopyToAsync(process.StandardInput.BaseStream);
        }
        process.StandardInput.Close();

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Restore failed (exit {process.ExitCode}): {stderr}");
        }

        record.RestoredAt = DateTime.UtcNow;
        record.RestoredBy = restoredBy;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.BackupRestore, "Restore", record.Id.ToString(), newValues: new { record.FileName });
    }

    public async Task<IReadOnlyList<BackupRecord>> GetHistoryAsync()
    {
        return await _db.BackupRecords.OrderByDescending(b => b.StartedAt).ToListAsync();
    }

    public async Task<BackupRecord?> GetLastSuccessfulAsync()
    {
        return await _db.BackupRecords
            .Where(b => b.Status == BackupStatus.Success)
            .OrderByDescending(b => b.StartedAt)
            .FirstOrDefaultAsync();
    }
}
