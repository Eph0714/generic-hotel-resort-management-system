using HotelResortMS.Core.Common;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Areas.Admin.Models;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Areas.Admin.Controllers;

/// <summary>Section 46/56: read-only viewer over the audit log. There is deliberately no
/// edit/delete action here - audit rows are append-only.</summary>
[Area("Admin")]
[RequirePermission(SystemModules.AuditTrail, PermissionAction.View)]
public class AuditTrailController : Controller
{
    private const int PageSize = 25;
    private readonly ApplicationDbContext _db;

    public AuditTrailController(ApplicationDbContext db)
    {
        _db = db;
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

        return View(new AuditTrailViewModel
        {
            Logs = logs,
            ModuleFilter = module,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            TotalPages = totalPages,
            Modules = modules
        });
    }
}
