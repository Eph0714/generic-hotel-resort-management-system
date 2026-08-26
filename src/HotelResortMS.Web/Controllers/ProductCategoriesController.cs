using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Sales;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 6: Product Category master data backing the POS product picker.</summary>
[RequirePermission(SystemModules.Products, PermissionAction.View)]
public class ProductCategoriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public ProductCategoriesController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _db.ProductCategories.OrderBy(c => c.Name).ToListAsync();
        return View(categories);
    }

    [RequirePermission(SystemModules.Products, PermissionAction.Add)]
    public IActionResult Create() => View(new ProductCategoryEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Products, PermissionAction.Add)]
    public async Task<IActionResult> Create(ProductCategoryEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var entity = new ProductCategory { Name = model.Name, Description = model.Description };
        _db.ProductCategories.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Products, "CreateCategory", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Product category created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Products, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.ProductCategories.FindAsync(id);
        if (entity is null) return NotFound();

        return View(new ProductCategoryEditViewModel { Id = entity.Id, Name = entity.Name, Description = entity.Description, IsActive = entity.IsActive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Products, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(ProductCategoryEditViewModel model)
    {
        var entity = await _db.ProductCategories.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Products, "UpdateCategory", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Product category updated.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8: a category with products still assigned to it cannot be deactivated blind.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Products, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var count = await _db.Products.CountAsync(p => p.ProductCategoryId == id && p.IsActive);
        if (count > 0)
        {
            TempData["Error"] = $"This category cannot be deactivated because {count} active product(s) still use it.";
            return RedirectToAction(nameof(Index));
        }

        var entity = await _db.ProductCategories.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Products, "DeactivateCategory", id.ToString());

        TempData["Success"] = "Product category deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if any product (active or not) still references this category.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Products, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.ProductCategories.FindAsync(id);
        if (entity is null) return NotFound();

        var count = await _db.Products.CountAsync(p => p.ProductCategoryId == id);
        if (count > 0)
        {
            TempData["Error"] = $"This category cannot be deleted because {count} product(s) still use it. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.ProductCategories.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Products, "DeleteCategory", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Product category deleted.";
        return RedirectToAction(nameof(Index));
    }
}
