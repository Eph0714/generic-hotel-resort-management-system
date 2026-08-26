using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 37: what the hotel owes suppliers after Receiving posts goods.</summary>
[RequirePermission(SystemModules.AccountsPayable, PermissionAction.View)]
public class AccountsPayableController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccountsPayableService _payableService;

    public AccountsPayableController(ApplicationDbContext db, IAccountsPayableService payableService)
    {
        _db = db;
        _payableService = payableService;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.AccountsPayables.Include(a => a.Supplier).Include(a => a.PurchaseOrder).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AccountsPayableStatus>(status, out var s))
        {
            query = query.Where(a => a.Status == s);
        }
        ViewBag.Status = status;
        var payables = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return View(payables);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.AccountsPayable, PermissionAction.Add)]
    public async Task<IActionResult> Pay(int id, decimal amount, string? reference)
    {
        try
        {
            await _payableService.RecordSupplierPaymentAsync(id, amount, User.Identity?.Name ?? "Unknown", reference);
            TempData["Success"] = "Supplier payment recorded.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
