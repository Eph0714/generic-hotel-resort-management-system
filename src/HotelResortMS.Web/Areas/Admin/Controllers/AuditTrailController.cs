using HotelResortMS.Core.Common;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Areas.Admin.Models;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Areas.Admin.Controllers;

/// <summary>Section 46/56: viewer over the audit log. Rows are append-only from every
/// module's own normal operation (nothing in the app ever edits or deletes an entry as a
/// side effect of anything else it does) - but Audit Trail entries are NOT kept forever:
/// <see cref="HotelResortMS.Infrastructure.Services.AuditLogCleanupHostedService"/>
/// automatically deletes anything older than System Settings > Audit.RetentionDays
/// (default 3 days), and a Super Admin can also purge entries manually below (one at a
/// time, or all of them at once) - both are explicit, deliberate exceptions to the
/// original "never deleted" design, not something any other role or automated process
/// can trigger.</summary>
[Area("Admin")]
[RequirePermission(SystemModules.AuditTrail, PermissionAction.View)]
public class AuditTrailController : Controller
{
    private const int PageSize = 25;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public AuditTrailController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? module, DateOnly? fromDate, DateOnly? toDate, int page = 1)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(a => a.Module == module);
        }
        if (fromDate is not null)
        {
            query = query.Where(a => a.BusinessDate >= fromDate);
        }
        if (toDate is not null)
        {
            query = query.Where(a => a.BusinessDate <= toDate);
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var logs = await query
            .OrderByDescending(a => a.ActualDateTime)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var modules = await _db.AuditLogs.Select(a => a.Module).Distinct().OrderBy(m => m).ToListAsync();

        var retentionRaw = await _db.SystemSettings.Where(s => s.Key == "Audit.RetentionDays").Select(s => s.Value).FirstOrDefaultAsync();
        int.TryParse(retentionRaw, out var retentionDays);

        return View(new AuditTrailViewModel
        {
            Logs = logs,
            ModuleFilter = module,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            TotalPages = totalPages,
            Modules = modules,
            RetentionDays = retentionDays > 0 ? retentionDays : 3
        });
    }

    /// <summary>Deletes a single Audit Trail entry - Super Admin only, regardless of what
    /// the Roles &amp; Permissions matrix grants for the AuditTrail module (same
    /// role-gate pattern as SystemSettingsController).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SystemRoles.SuperAdmin)]
    public async Task<IActionResult> DeleteEntry(int id)
    {
        var entry = await _db.AuditLogs.FindAsync(id);
        if (entry is null) return NotFound();

        _db.AuditLogs.Remove(entry);
        await _db.SaveChangesAsync();

        // Logged AFTER the delete so this new entry is exactly the one row left behind
        // describing the purge - the audit trail still shows that something happened,
        // even though the entry it happened to is now gone.
        await _auditService.LogAsync(SystemModules.AuditTrail, "DeleteEntry", id.ToString(),
            reason: $"Manually deleted by Super Admin (was: {entry.Module}/{entry.Action} at {entry.ActualDateTime:u}).");

        TempData["Success"] = "Audit trail entry deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Deletes every Audit Trail entry - Super Admin only. A full manual purge,
    /// independent of the automatic retention window.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SystemRoles.SuperAdmin)]
    public async Task<IActionResult> ClearAll()
    {
        var count = await _db.AuditLogs.ExecuteDeleteAsync();

        await _auditService.LogAsync(SystemModules.AuditTrail, "ClearAll", null,
            reason: $"Manually cleared {count} audit trail entr{(count == 1 ? "y" : "ies")} by Super Admin.");

        TempData["Success"] = $"Cleared {count} audit trail entr{(count == 1 ? "y" : "ies")}.";
        return RedirectToAction(nameof(Index));
    }
}
