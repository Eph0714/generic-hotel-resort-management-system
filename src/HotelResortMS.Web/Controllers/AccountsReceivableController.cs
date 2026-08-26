using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 36: guest/corporate balances still owed (created automatically at
/// Check-Out when an outstanding balance is authorized - see FrontDeskService).</summary>
[RequirePermission(SystemModules.AccountsReceivable, PermissionAction.View)]
public class AccountsReceivableController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccountsReceivableService _receivableService;

    public AccountsReceivableController(ApplicationDbContext db, IAccountsReceivableService receivableService)
    {
        _db = db;
        _receivableService = receivableService;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.AccountsReceivables.Include(a => a.Guest).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AccountsReceivableStatus>(status, out var s))
        {
            query = query.Where(a => a.Status == s);
        }
        ViewBag.Status = status;
        var receivables = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return View(receivables);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.AccountsReceivable, PermissionAction.Add)]
    public async Task<IActionResult> Pay(int id, decimal amount, string? reference)
    {
        try
        {
            await _receivableService.RecordGuestPaymentAsync(id, amount, User.Identity?.Name ?? "Unknown", reference);
            TempData["Success"] = "Guest payment recorded.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
