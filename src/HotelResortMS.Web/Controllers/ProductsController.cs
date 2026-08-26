using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Sales;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 6/26/33: the product catalog the POS sells from.</summary>
[RequirePermission(SystemModules.Products, PermissionAction.View)]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public ProductsController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Products.Include(p => p.ProductCategory).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search) || (p.Sku != null && p.Sku.Contains(search)));
        }
        ViewBag.Search = search;
        var products = await query.OrderBy(p => p.Name).ToListAsync();
        return View(products);
    }

    [RequirePermission(SystemModules.Products, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new ProductEditViewModel();
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Products, PermissionAction.Add)]
    public async Task<IActionResult> Create(ProductEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        var entity = new Product
        {
            Sku = model.Sku,
            Name = model.Name,
            Description = model.Description,
            ProductCategoryId = model.ProductCategoryId,
            Type = model.Type,
            UnitPrice = model.UnitPrice,
            Cost = model.Cost,
            TrackInventory = model.TrackInventory,
            InventoryItemId = model.TrackInventory ? model.InventoryItemId : null,
            DiscountEligible = model.DiscountEligible,
            CreatedBy = User.Identity?.Name
        };
        _db.Products.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Products, "Create", entity.Id.ToString(), newValues: new { entity.Name, entity.UnitPrice });
        TempData["Success"] = "Product created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Products, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.Products.FindAsync(id);
        if (entity is null) return NotFound();

        var model = new ProductEditViewModel
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Description = entity.Description,
            ProductCategoryId = entity.ProductCategoryId,
            Type = entity.Type,
            UnitPrice = entity.UnitPrice,
            Cost = entity.Cost,
            TrackInventory = entity.TrackInventory,
            InventoryItemId = entity.InventoryItemId,
            DiscountEligible = entity.DiscountEligible,
            IsActive = entity.IsActive
        };
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Products, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(ProductEditViewModel model)
    {
        var entity = await _db.Products.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        // Section 15: changing the price only affects future sales - POSTransactionDetail
        // already snapshots the price actually charged on past sales.
        var oldValues = new { entity.UnitPrice, entity.Cost };

        entity.Sku = model.Sku;
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.ProductCategoryId = model.ProductCategoryId;
        entity.Type = model.Type;
        entity.UnitPrice = model.UnitPrice;
        entity.Cost = model.Cost;
        entity.TrackInventory = model.TrackInventory;
        entity.InventoryItemId = model.TrackInventory ? model.InventoryItemId : null;
        entity.DiscountEligible = model.DiscountEligible;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Products, "Update", entity.Id.ToString(), oldValues, new { entity.UnitPrice, entity.Cost });
        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8: a product already sold on a POS transaction is kept for historical
    // integrity - it can only be deactivated (hidden from the POS picker), never deleted.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Products, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.Products.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Products, "Deactivate", id.ToString());

        TempData["Success"] = "Product deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if the product has ever been sold (POSTransactionDetail) or has
    // a Recipe/BOM defined against it - either way, deleting the row would erase or orphan
    // that history. Deactivate instead in those cases.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Products, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Products.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.POSTransactionDetails.AnyAsync(d => d.ProductId == id)
            || await _db.Recipes.AnyAsync(r => r.ProductId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This product cannot be deleted because it has sales or recipe history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.Products.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Products, "Delete", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Product deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(ProductEditViewModel model)
    {
        model.Categories = await _db.ProductCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        model.InventoryItems = await _db.InventoryItems.Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync();
    }
}
