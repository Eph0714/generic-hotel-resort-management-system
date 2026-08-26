using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Inventory;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 35: Purchase Order -> Receiving -> Inventory Update -> Supplier
/// Payable. All the actual chain logic lives in IPurchasingService; this controller only
/// gathers input and renders results.</summary>
[RequirePermission(SystemModules.Purchasing, PermissionAction.View)]
public class PurchaseOrdersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPurchasingService _purchasingService;

    public PurchaseOrdersController(ApplicationDbContext db, IPurchasingService purchasingService)
    {
        _db = db;
        _purchasingService = purchasingService;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.PurchaseOrders.Include(p => p.Supplier).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PurchaseOrderStatus>(status, out var s))
        {
            query = query.Where(p => p.Status == s);
        }
        ViewBag.Status = status;
        var orders = await query.OrderByDescending(p => p.OrderDate).ToListAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Details).ThenInclude(d => d.InventoryItem)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (po is null) return NotFound();

        ViewBag.Receivings = await _db.Receivings.Where(r => r.PurchaseOrderId == id).OrderByDescending(r => r.ReceivedDateTime).ToListAsync();
        return View(po);
    }

    [RequirePermission(SystemModules.Purchasing, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new PurchaseOrderCreateViewModel();
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Purchasing, PermissionAction.Add)]
    public async Task<IActionResult> Create(PurchaseOrderCreateViewModel model)
    {
        var lines = model.Lines.Where(l => l.QuantityOrdered > 0).ToList();
        if (lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one line item.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        try
        {
            var po = await _purchasingService.CreatePurchaseOrderAsync(
                model.SupplierId, model.OrderDate, model.ExpectedDate,
                lines.Select(l => new PurchaseOrderLineInput { InventoryItemId = l.InventoryItemId, QuantityOrdered = l.QuantityOrdered, UnitCost = l.UnitCost }).ToList(),
                User.Identity?.Name ?? "Unknown");

            TempData["Success"] = $"Purchase order {po.PONumber} created.";
            return RedirectToAction(nameof(Details), new { id = po.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateAsync(model);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Purchasing, PermissionAction.Approve)]
    public async Task<IActionResult> Submit(int id)
    {
        try
        {
            await _purchasingService.SubmitPurchaseOrderAsync(id, User.Identity?.Name ?? "Unknown");
            TempData["Success"] = "Purchase order submitted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Purchasing, PermissionAction.Delete)]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        try
        {
            await _purchasingService.CancelPurchaseOrderAsync(id, reason, User.Identity?.Name ?? "Unknown");
            TempData["Success"] = "Purchase order cancelled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [RequirePermission(SystemModules.Purchasing, PermissionAction.Add)]
    public async Task<IActionResult> Receive(int id)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Details).ThenInclude(d => d.InventoryItem).FirstOrDefaultAsync(p => p.Id == id);
        if (po is null) return NotFound();
        if (po.Status is not (PurchaseOrderStatus.Submitted or PurchaseOrderStatus.PartiallyReceived))
        {
            TempData["Error"] = $"Cannot receive against a purchase order that is {po.Status}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(po);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Purchasing, PermissionAction.Add)]
    public async Task<IActionResult> Receive(int id, int[] purchaseOrderDetailId, decimal[] quantityReceived, decimal[] quantityDamaged, string? notes)
    {
        var lines = purchaseOrderDetailId
            .Select((detailId, i) => new ReceivingLineInput { PurchaseOrderDetailId = detailId, QuantityReceived = quantityReceived[i], QuantityDamaged = quantityDamaged[i] })
            .Where(l => l.QuantityReceived > 0)
            .ToList();

        if (lines.Count == 0)
        {
            TempData["Error"] = "Enter a received quantity for at least one line.";
            return RedirectToAction(nameof(Receive), new { id });
        }

        try
        {
            var receiving = await _purchasingService.ReceiveAsync(id, lines, User.Identity?.Name ?? "Unknown", notes);
            TempData["Success"] = $"Receiving {receiving.ReceivingNumber} posted.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateAsync(PurchaseOrderCreateViewModel model)
    {
        model.Suppliers = await _db.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        model.InventoryItems = await _db.InventoryItems.Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync();
    }
}
