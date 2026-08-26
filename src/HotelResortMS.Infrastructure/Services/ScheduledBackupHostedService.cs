using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HotelResortMS.Infrastructure.Services;

/// <summary>
/// Section 50 (Scheduled Backup): a lightweight in-process scheduler - checks once a
/// minute whether it is past today's configured backup time and no successful backup has
/// run yet today, and if so runs one via IBackupService. Controlled entirely through
/// System Settings (Backup.ScheduleEnabled / Backup.ScheduleTimeOfDay) so an administrator
/// can turn it on/off or retime it without a redeploy.
///
/// This is intentionally simple (no Quartz/Hangfire dependency) - it only needs to notice
/// "has the configured time passed today" once per minute, which a plain polling loop
/// does perfectly well for a single-instance deployment. A multi-instance deployment would
/// need a distributed lock to avoid every instance backing up at once; out of scope here.
/// </summary>
public class ScheduledBackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledBackupHostedService> _logger;

    public ScheduledBackupHostedService(IServiceScopeFactory scopeFactory, ILogger<ScheduledBackupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckAndRunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // A failed check must never crash the host - just log and try again next tick.
                _logger.LogError(ex, "Scheduled backup check failed.");
            }
        }
    }

    private async Task CheckAndRunAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var enabledRaw = await db.SystemSettings.Where(s => s.Key == "Backup.ScheduleEnabled").Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        if (!bool.TryParse(enabledRaw, out var enabled) || !enabled)
        {
            return;
        }

        var timeRaw = await db.SystemSettings.Where(s => s.Key == "Backup.ScheduleTimeOfDay").Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        if (!TimeOnly.TryParse(timeRaw, out var scheduledTime))
        {
            _logger.LogWarning("Backup.ScheduleTimeOfDay ('{Raw}') is not a valid time - skipping scheduled backup check.", timeRaw);
            return;
        }

        var now = DateTime.Now;
        if (TimeOnly.FromDateTime(now) < scheduledTime)
        {
            return; // Not yet time today.
        }

        // Don't fire twice: skip if a successful backup already ran today (regardless of
        // whether it was this scheduler or a manual "Backup Now" click).
        var todayStart = now.Date;
        var alreadyRanToday = await db.BackupRecords
            .AnyAsync(b => b.Status == BackupStatus.Success && b.StartedAt >= todayStart, cancellationToken);
        if (alreadyRanToday)
        {
            return;
        }

        _logger.LogInformation("Running scheduled daily backup...");
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
        var record = await backupService.CreateBackupAsync("System (Scheduled)");

        if (record.Status == BackupStatus.Success)
        {
            _logger.LogInformation("Scheduled backup completed: {FileName} ({Size} bytes).", record.FileName, record.SizeBytes);
        }
        else
        {
            _logger.LogError("Scheduled backup failed: {Error}", record.ErrorMessage);
        }
    }
}
