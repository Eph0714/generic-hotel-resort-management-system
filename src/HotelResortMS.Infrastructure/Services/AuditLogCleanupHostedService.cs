using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HotelResortMS.Infrastructure.Services;

/// <summary>
/// Automatic Audit Trail retention: deletes any AuditLog row older than the configured
/// retention window (System Settings > Audit.RetentionDays, default 3 days). Runs once at
/// startup and then hourly - a plain polling loop is enough here (same reasoning as
/// ScheduledBackupHostedService) since the delete itself is idempotent: re-running it
/// against rows that are already gone is simply a no-op.
///
/// This is a deliberate exception to AuditLog's own "append-only, never deleted" design
/// note - the user explicitly asked for a bounded retention window plus a manual purge
/// (see AuditTrailController.DeleteEntry/ClearAll), so this project keeps only a rolling
/// recent window rather than an indefinite audit history.
/// </summary>
public class AuditLogCleanupHostedService : BackgroundService
{
    private const int DefaultRetentionDays = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogCleanupHostedService> _logger;

    public AuditLogCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<AuditLogCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        // do/while so cleanup runs once immediately at startup rather than waiting a full
        // hour for the first pass.
        do
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // A failed pass must never crash the host - just log and try again next tick.
                _logger.LogError(ex, "Audit log cleanup failed.");
            }
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var raw = await db.SystemSettings.Where(s => s.Key == "Audit.RetentionDays").Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        if (!int.TryParse(raw, out var retentionDays) || retentionDays <= 0)
        {
            retentionDays = DefaultRetentionDays;
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = await db.AuditLogs.Where(a => a.ActualDateTime < cutoff).ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation("Audit log cleanup: removed {Count} entr{Suffix} older than {Days} day(s).",
                deleted, deleted == 1 ? "y" : "ies", retentionDays);
        }
    }
}
