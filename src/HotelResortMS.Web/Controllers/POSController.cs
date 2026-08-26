using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Sales;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 26: Point of Sale. This controller only gathers input and renders
/// results - every total, discount rule, and folio/payment posting happens in POSService
/// so the same math applies whether a sale is rung up here or (later) via an API client.</summary>
[RequirePermission(SystemModules.POS, PermissionAction.View)]
public class POSController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPOSService _posService;
    private readonly IPermissionService _permissionService;

    public POSController(ApplicationDbContext db, IPOSService posService, IPermissionService permissionService)
    {
        _db = db;
        _posService = posService;
        _permissionService = permissionService;
    }

    [RequirePermission(SystemModules.POS, PermissionAction.Add)]
    public async Task<IActionResult> Index()
    {
        var model = new POSSaleViewModel
        {
            Products = await _db.Products.Where(p => p.IsActive).Include(p => p.ProductCategory).OrderBy(p => p.Name).ToListAsync(),
            Categories = await _db.ProductCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
            ActiveDiscounts = await _db.Discounts.Where(d => d.IsActive && d.EligibleForProducts).OrderBy(d => d.Name).ToListAsync(),
            Guests = await _db.Guests.Where(g => g.IsActive).OrderBy(g => g.LastName).ToListAsync()
        };

        // Section 18/55: the manual-override controls only render for a cashier who
        // actually holds the Discounts Approve permission - a hidden checkbox is not a
        // security boundary, but there is no reason to show a control the server would
        // reject anyway.
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        ViewBag.CanOverrideDiscounts = userId is not null
            && await _permissionService.HasPermissionAsync(userId, SystemModules.Discounts, PermissionAction.Approve);

        return View(model);
    }

    /// <summary>AJAX: open (unclosed) folios for a guest, so the cashier can charge a sale
    /// to the guest's room instead of collecting payment now (Section 26 - Room Charge).</summary>
    [HttpGet]
    public async Task<IActionResult> OpenFolios(int guestId)
    {
        var folios = await _db.GuestFolios
            .Where(f => f.GuestId == guestId && f.Status == FolioStatus.Open)
            .Include(f => f.Reservation)
            .Select(f => new { f.Id, f.FolioNumber, ReservationNumber = f.Reservation!.ReservationNumber })
            .ToListAsync();
        return Json(folios);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.POS, PermissionAction.Add)]
    public async Task<IActionResult> Complete(POSCompleteRequest request)
    {
        if (request.ProductId.Length == 0)
        {
            TempData["Error"] = "Add at least one item to the sale.";
            return RedirectToAction(nameof(Index));
        }

        var items = request.ProductId
            .Zip(request.Quantity, (productId, qty) => new POSCartItem { ProductId = productId, Quantity = qty })
            .Where(i => i.Quantity > 0)
            .ToList();

        // Section 18: a manual override is only honored for a user who actually holds the
        // Discounts Approve permission - checked here (server-side, Section 55), not just
        // by whether the checkbox happened to be rendered on the screen.
        var isManualOverride = request.IsManualOverride;
        if (isManualOverride)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null || !await _permissionService.HasPermissionAsync(userId, SystemModules.Discounts, PermissionAction.Approve))
            {
                TempData["Error"] = "You are not authorized to apply a manual discount override.";
                return RedirectToAction(nameof(Index));
            }
            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                TempData["Error"] = "A reason is required for a manual discount override.";
                return RedirectToAction(nameof(Index));
            }
        }

        try
        {
            var sale = await _posService.CompleteSaleAsync(
                items,
                request.GuestId,
                request.GuestFolioId,
                request.DiscountId,
                request.PaymentMethod,
                request.PaymentReference,
                User.Identity?.Name ?? "Unknown",
                isManualOverride,
                request.OverrideReason);

            TempData["Success"] = $"Sale {sale.PosNumber} completed.";
            return RedirectToAction(nameof(Receipt), new { id = sale.Id });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // Section 52: surface as a friendly message, never a raw exception page.
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var sale = await _db.POSTransactions
            .Include(s => s.Details)
            .Include(s => s.Guest)
            .Include(s => s.GuestFolio)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale is null) return NotFound();
        return View(sale);
    }

    public async Task<IActionResult> History(string? status)
    {
        var query = _db.POSTransactions.Include(s => s.Guest).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<POSTransactionStatus>(status, out var s))
        {
            query = query.Where(x => x.Status == s);
        }
        ViewBag.Status = status;
        var sales = await query.OrderByDescending(s => s.ActualDateTime).ToListAsync();
        return View(sales);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.POS, PermissionAction.Void)]
    public async Task<IActionResult> Void(int id, string reason)
    {
        try
        {
            await _posService.VoidSaleAsync(id, User.Identity?.Name ?? "Unknown", reason);
            TempData["Success"] = "Sale voided.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(History));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.POS, PermissionAction.Refund)]
    public async Task<IActionResult> Refund(int id, string reason)
    {
        try
        {
            await _posService.RefundSaleAsync(id, User.Identity?.Name ?? "Unknown", reason);
            TempData["Success"] = "Sale refunded.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(History));
    }
}
