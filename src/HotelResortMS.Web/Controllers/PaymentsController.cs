using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Sales;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>
/// Section 27: standalone payments (e.g. settling a guest's outstanding folio balance
/// outside of Check-Out or a POS sale). Most payments in the system are instead recorded
/// automatically by FrontDeskService/POSService at the point of the transaction - this
/// screen exists for the remaining ad-hoc case.
/// </summary>
[RequirePermission(SystemModules.Payments, PermissionAction.View)]
public class PaymentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentService _paymentService;

    public PaymentsController(ApplicationDbContext db, IPaymentService paymentService)
    {
        _db = db;
        _paymentService = paymentService;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.Payments.Include(p => p.Guest).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PaymentStatus>(status, out var s))
        {
            query = query.Where(p => p.Status == s);
        }
        ViewBag.Status = status;
        var payments = await query.OrderByDescending(p => p.ActualDateTime).ToListAsync();
        return View(payments);
    }

    [RequirePermission(SystemModules.Payments, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new PaymentCreateViewModel();
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Payments, PermissionAction.Add)]
    public async Task<IActionResult> Create(PaymentCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        try
        {
            var payment = await _paymentService.RecordPaymentAsync(
                model.GuestId, model.GuestFolioId, null, model.Amount, model.Method, model.ReferenceNumber,
                User.Identity?.Name ?? "Unknown");

            TempData["Success"] = $"Payment {payment.PaymentNumber} recorded.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateAsync(model);
            return View(model);
        }
    }

    /// <summary>AJAX: a guest's open folios, for the "apply to folio" dropdown.</summary>
    [HttpGet]
    public async Task<IActionResult> OpenFolios(int guestId)
    {
        var folios = await _db.GuestFolios
            .Where(f => f.GuestId == guestId && f.Status == FolioStatus.Open)
            .Select(f => new { f.Id, f.FolioNumber })
            .ToListAsync();
        return Json(folios);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Payments, PermissionAction.Void)]
    public async Task<IActionResult> Void(int id, string reason)
    {
        try
        {
            await _paymentService.VoidPaymentAsync(id, User.Identity?.Name ?? "Unknown", reason);
            TempData["Success"] = "Payment voided.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Payments, PermissionAction.Refund)]
    public async Task<IActionResult> Refund(int id, string reason)
    {
        try
        {
            await _paymentService.RefundPaymentAsync(id, User.Identity?.Name ?? "Unknown", reason);
            TempData["Success"] = "Payment refunded.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(PaymentCreateViewModel model)
    {
        model.Guests = await _db.Guests.Where(g => g.IsActive).OrderBy(g => g.LastName).ToListAsync();
    }
}
