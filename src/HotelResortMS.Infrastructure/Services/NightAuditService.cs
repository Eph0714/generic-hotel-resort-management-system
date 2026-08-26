using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="INightAuditService"/>
public class NightAuditService : INightAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDateService _businessDateService;
    private readonly IAuditService _auditService;

    public NightAuditService(ApplicationDbContext db, IBusinessDateService businessDateService, IAuditService auditService)
    {
        _db = db;
        _businessDateService = businessDateService;
        _auditService = auditService;
    }

    /// <summary>
    /// Section 42: sweeps for the conditions the spec calls out explicitly - reservations
    /// that should have arrived or departed by now, and open folios with an impossible
    /// (negative) balance. Not exhaustive of every possible data problem, but exactly the
    /// checks the spec names as blocking.
    /// </summary>
    public async Task<IReadOnlyList<NightAuditException>> FindExceptionsAsync()
    {
        var businessDate = await _businessDateService.GetCurrentAsync();
        var exceptions = new List<NightAuditException>();

        var overdueArrivals = await _db.Reservations
            .Where(r => (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                        && r.CheckInDate <= businessDate.Date)
            .ToListAsync();
        foreach (var r in overdueArrivals)
        {
            exceptions.Add(new NightAuditException
            {
                Category = "No-Show Candidate",
                Message = $"Reservation {r.ReservationNumber} was due to arrive {r.CheckInDate:yyyy-MM-dd} but has not checked in."
            });
        }

        var overdueDepartures = await _db.Reservations
            .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckOutDate < businessDate.Date)
            .ToListAsync();
        foreach (var r in overdueDepartures)
        {
            exceptions.Add(new NightAuditException
            {
                Category = "Overdue Check-Out",
                Message = $"Reservation {r.ReservationNumber} was due to check out {r.CheckOutDate:yyyy-MM-dd} but is still checked in."
            });
        }

        var openFolios = await _db.GuestFolios
            .Include(f => f.Details)
            .Where(f => f.Status == FolioStatus.Open)
            .ToListAsync();
        foreach (var f in openFolios.Where(f => f.Details.Sum(d => d.Amount) < 0))
        {
            exceptions.Add(new NightAuditException
            {
                Category = "Folio Anomaly",
                Message = $"Folio {f.FolioNumber} has a negative (overpaid) balance - review before closing."
            });
        }

        return exceptions;
    }

    public async Task RunAsync(string runBy, string? overrideReason = null)
    {
        var businessDate = await _businessDateService.GetCurrentAsync();
        if (businessDate.Status != Core.Entities.BusinessDate.DateStatus.Open)
        {
            throw new InvalidOperationException($"Night Audit has already been run for {businessDate.Date:yyyy-MM-dd}.");
        }

        var exceptions = await FindExceptionsAsync();
        if (exceptions.Count > 0 && string.IsNullOrWhiteSpace(overrideReason))
        {
            // Section 42: "Do not complete Night Audit while critical errors exist unless
            // an authorized user overrides them" - refuse rather than silently proceeding.
            throw new InvalidOperationException(
                $"{exceptions.Count} unresolved exception(s) found. An authorized override reason is required to proceed.");
        }

        _db.NightAuditRecords.Add(new NightAuditRecord
        {
            BusinessDateId = businessDate.Id,
            ExceptionsFound = string.Join("\n", exceptions.Select(e => $"[{e.Category}] {e.Message}")),
            WasOverridden = exceptions.Count > 0,
            OverrideReason = overrideReason,
            RunAt = DateTime.UtcNow,
            RunBy = runBy
        });

        businessDate.Status = Core.Entities.BusinessDate.DateStatus.NightAuditInProgress;
        businessDate.NightAuditStartedAt = DateTime.UtcNow;
        businessDate.NightAuditBy = runBy;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.NightAudit, "Run", businessDate.Id.ToString(),
            newValues: new { ExceptionCount = exceptions.Count }, reason: overrideReason);
    }
}
