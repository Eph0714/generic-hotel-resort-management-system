using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 38: read-only view of recognized revenue - Income rows are written
/// automatically by POSService/FrontDeskService at the moment a charge is posted.</summary>
[RequirePermission(SystemModules.Income, PermissionAction.View)]
public class IncomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IIncomeService _incomeService;

    public IncomeController(ApplicationDbContext db, IIncomeService incomeService)
    {
        _db = db;
        _incomeService = incomeService;
    }

    public async Task<IActionResult> Index(DateOnly? fromDate, DateOnly? toDate, IncomeCategory? category)
    {
        var query = _db.Incomes.AsQueryable();
        if (fromDate is not null) query = query.Where(i => i.BusinessDate >= fromDate);
        if (toDate is not null) query = query.Where(i => i.BusinessDate <= toDate);
        if (category is not null) query = query.Where(i => i.Category == category);

        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Category = category;

        var incomes = await query.OrderByDescending(i => i.ActualDateTime).ToListAsync();
        return View(incomes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Income, PermissionAction.Void)]
    public async Task<IActionResult> Void(int id, string reason)
    {
        try
        {
            await _incomeService.VoidIncomeAsync(id, User.Identity?.Name ?? "Unknown", reason);
            TempData["Success"] = "Income record voided.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
