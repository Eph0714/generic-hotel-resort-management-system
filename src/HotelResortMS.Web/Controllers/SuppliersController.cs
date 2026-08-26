using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Inventory;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 6/35: vendor master data purchase orders are raised against.</summary>
[RequirePermission(SystemModules.Suppliers, PermissionAction.View)]
public class SuppliersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public SuppliersController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var suppliers = await _db.Suppliers.OrderBy(s => s.Name).ToListAsync();
        return View(suppliers);
    }

    [RequirePermission(SystemModules.Suppliers, PermissionAction.Add)]
    public IActionResult Create() => View(new SupplierEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Suppliers, PermissionAction.Add)]
    public async Task<IActionResult> Create(SupplierEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var entity = new Supplier
        {
            Name = model.Name,
            ContactPerson = model.ContactPerson,
            Phone = model.Phone,
            Email = model.Email,
            Address = model.Address,
            Notes = model.Notes,
            CreatedBy = User.Identity?.Name
        };
        _db.Suppliers.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Suppliers, "Create", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Supplier created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Suppliers, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.Suppliers.FindAsync(id);
        if (entity is null) return NotFound();

        return View(new SupplierEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            ContactPerson = entity.ContactPerson,
            Phone = entity.Phone,
            Email = entity.Email,
            Address = entity.Address,
            Notes = entity.Notes,
            IsActive = entity.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Suppliers, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(SupplierEditViewModel model)
    {
        var entity = await _db.Suppliers.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        entity.Name = model.Name;
        entity.ContactPerson = model.ContactPerson;
        entity.Phone = model.Phone;
        entity.Email = model.Email;
        entity.Address = model.Address;
        entity.Notes = model.Notes;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Suppliers, "Update", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Supplier updated.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8: a supplier with purchase order history is kept for traceability.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Suppliers, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.Suppliers.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Suppliers, "Deactivate", id.ToString());

        TempData["Success"] = "Supplier deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if the supplier has any purchase order or payable history.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Suppliers, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Suppliers.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.PurchaseOrders.AnyAsync(p => p.SupplierId == id)
            || await _db.AccountsPayables.AnyAsync(a => a.SupplierId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This supplier cannot be deleted because it has purchase order or payable history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.Suppliers.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Suppliers, "Delete", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Supplier deleted.";
        return RedirectToAction(nameof(Index));
    }
}
