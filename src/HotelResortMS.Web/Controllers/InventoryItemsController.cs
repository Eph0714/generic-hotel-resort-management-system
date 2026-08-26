using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Inventory;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 33: Inventory master data plus manual stock adjustments. Every
/// adjustment goes through IInventoryService.PostTransactionAsync - CurrentStock is never
/// edited directly here (Section 33: "Inventory transactions cannot be permanently deleted
/// after affecting stock").</summary>
[RequirePermission(SystemModules.Inventory, PermissionAction.View)]
public class InventoryItemsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditService _auditService;

    public InventoryItemsController(ApplicationDbContext db, IInventoryService inventoryService, IAuditService auditService)
    {
        _db = db;
        _inventoryService = inventoryService;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, bool lowStockOnly = false)
    {
        var query = _db.InventoryItems.Include(i => i.UnitOfMeasure).Include(i => i.InventoryLocation).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(i => i.Name.Contains(search) || (i.Sku != null && i.Sku.Contains(search)));
        }
        if (lowStockOnly)
        {
            query = query.Where(i => i.CurrentStock <= i.ReorderLevel);
        }
        ViewBag.Search = search;
        ViewBag.LowStockOnly = lowStockOnly;

        var items = await query.OrderBy(i => i.Name).ToListAsync();
        return View(items);
    }

    [RequirePermission(SystemModules.Inventory, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new InventoryItemEditViewModel();
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Add)]
    public async Task<IActionResult> Create(InventoryItemEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        var entity = new InventoryItem
        {
            Sku = model.Sku,
            Name = model.Name,
            Description = model.Description,
            UnitOfMeasureId = model.UnitOfMeasureId,
            InventoryLocationId = model.InventoryLocationId,
            Cost = model.Cost,
            ReorderLevel = model.ReorderLevel,
            ExpirationDate = model.ExpirationDate,
            CurrentStock = 0,
            CreatedBy = User.Identity?.Name
        };
        _db.InventoryItems.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Inventory, "Create", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Inventory item created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Inventory, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.InventoryItems.FindAsync(id);
        if (entity is null) return NotFound();

        var model = new InventoryItemEditViewModel
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Description = entity.Description,
            UnitOfMeasureId = entity.UnitOfMeasureId,
            InventoryLocationId = entity.InventoryLocationId,
            Cost = entity.Cost,
            ReorderLevel = entity.ReorderLevel,
            ExpirationDate = entity.ExpirationDate,
            IsActive = entity.IsActive
        };
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(InventoryItemEditViewModel model)
    {
        var entity = await _db.InventoryItems.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        entity.Sku = model.Sku;
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.UnitOfMeasureId = model.UnitOfMeasureId;
        entity.InventoryLocationId = model.InventoryLocationId;
        entity.Cost = model.Cost;
        entity.ReorderLevel = model.ReorderLevel;
        entity.ExpirationDate = model.ExpirationDate;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Inventory, "Update", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Inventory item updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.InventoryItems.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Inventory, "Deactivate", id.ToString());

        TempData["Success"] = "Inventory item deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if the item has any stock movement history, is used in a
    // recipe/BOM, appears on a purchase order, or is linked to a resale Product.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.InventoryItems.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.InventoryTransactions.AnyAsync(t => t.InventoryItemId == id)
            || await _db.RecipeDetails.AnyAsync(r => r.InventoryItemId == id)
            || await _db.PurchaseOrderDetails.AnyAsync(p => p.InventoryItemId == id)
            || await _db.Products.AnyAsync(p => p.InventoryItemId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This inventory item cannot be deleted because it has stock, recipe, purchase order, or product history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.InventoryItems.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Inventory, "Delete", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Inventory item deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Section 44 Stock Card: the full movement history for one item.</summary>
    public async Task<IActionResult> StockCard(int id)
    {
        var item = await _db.InventoryItems.Include(i => i.UnitOfMeasure).FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();

        ViewBag.Item = item;
        var transactions = await _db.InventoryTransactions
            .Where(t => t.InventoryItemId == id)
            .OrderByDescending(t => t.ActualDateTime)
            .ToListAsync();
        return View(transactions);
    }

    [RequirePermission(SystemModules.Inventory, PermissionAction.Add)]
    public async Task<IActionResult> Adjust(int id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item is null) return NotFound();

        return View(new StockAdjustmentViewModel { InventoryItemId = item.Id, ItemName = item.Name, CurrentStock = item.CurrentStock });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Add)]
    public async Task<IActionResult> Adjust(StockAdjustmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var item = await _db.InventoryItems.FindAsync(model.InventoryItemId);
            model.ItemName = item?.Name ?? string.Empty;
            model.CurrentStock = item?.CurrentStock ?? 0;
            return View(model);
        }

        try
        {
            // Stock Out/Waste/Transfer reduce stock - the signed quantity direction is
            // implied by the chosen transaction type, not left for the user to get wrong.
            var signedQuantity = model.Type is InventoryTransactionType.StockOut or InventoryTransactionType.Waste or InventoryTransactionType.Transfer
                ? -Math.Abs(model.Quantity)
                : Math.Abs(model.Quantity);

            await _inventoryService.PostTransactionAsync(
                model.InventoryItemId, model.Type, signedQuantity, "Manual", null,
                User.Identity?.Name ?? "Unknown", model.Notes);

            TempData["Success"] = "Stock adjustment posted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(StockCard), new { id = model.InventoryItemId });
    }

    private async Task PopulateAsync(InventoryItemEditViewModel model)
    {
        model.Units = await _db.UnitsOfMeasure.Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync();
        model.Locations = await _db.InventoryLocations.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
    }
}
