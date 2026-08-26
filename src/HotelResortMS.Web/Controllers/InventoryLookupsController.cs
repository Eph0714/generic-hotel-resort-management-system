using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Inventory;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 6/33: Units of Measure and Inventory Locations master data - one
/// generic CRUD controller instead of two near-identical ones.</summary>
[RequirePermission(SystemModules.Inventory, PermissionAction.View)]
public class InventoryLookupsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public InventoryLookupsController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(InventoryLookupType type)
    {
        ViewBag.Type = type;
        var items = type == InventoryLookupType.UnitOfMeasure
            ? await _db.UnitsOfMeasure.Select(x => new InventoryLookupItemViewModel { Id = x.Id, Name = x.Name, Description = x.Abbreviation, IsActive = x.IsActive }).ToListAsync()
            : await _db.InventoryLocations.Select(x => new InventoryLookupItemViewModel { Id = x.Id, Name = x.Name, Description = x.Description, IsActive = x.IsActive }).ToListAsync();
        return View(items);
    }

    public IActionResult Create(InventoryLookupType type) => View(new InventoryLookupEditViewModel { Type = type });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Add)]
    public async Task<IActionResult> Create(InventoryLookupEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (model.Type == InventoryLookupType.UnitOfMeasure)
        {
            _db.UnitsOfMeasure.Add(new UnitOfMeasure { Name = model.Name, Abbreviation = model.Description ?? string.Empty });
        }
        else
        {
            _db.InventoryLocations.Add(new InventoryLocation { Name = model.Name, Description = model.Description });
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Inventory, $"Create{model.Type}", newValues: new { model.Name });
        return RedirectToAction(nameof(Index), new { type = model.Type });
    }

    public async Task<IActionResult> Edit(InventoryLookupType type, int id)
    {
        if (type == InventoryLookupType.UnitOfMeasure)
        {
            var uom = await _db.UnitsOfMeasure.FindAsync(id);
            if (uom is null) return NotFound();
            return View(new InventoryLookupEditViewModel { Id = uom.Id, Name = uom.Name, Description = uom.Abbreviation, Type = type });
        }
        var loc = await _db.InventoryLocations.FindAsync(id);
        if (loc is null) return NotFound();
        return View(new InventoryLookupEditViewModel { Id = loc.Id, Name = loc.Name, Description = loc.Description, Type = type });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(InventoryLookupEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (model.Type == InventoryLookupType.UnitOfMeasure)
        {
            var uom = await _db.UnitsOfMeasure.FindAsync(model.Id);
            if (uom is null) return NotFound();
            uom.Name = model.Name;
            uom.Abbreviation = model.Description ?? string.Empty;
        }
        else
        {
            var loc = await _db.InventoryLocations.FindAsync(model.Id);
            if (loc is null) return NotFound();
            loc.Name = model.Name;
            loc.Description = model.Description;
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Inventory, $"Update{model.Type}", model.Id.ToString(), newValues: new { model.Name });
        return RedirectToAction(nameof(Index), new { type = model.Type });
    }

    // Section 8: blocked if any InventoryItem still references this lookup value.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(InventoryLookupType type, int id)
    {
        var inUse = type == InventoryLookupType.UnitOfMeasure
            ? await _db.InventoryItems.AnyAsync(i => i.UnitOfMeasureId == id && i.IsActive)
            : await _db.InventoryItems.AnyAsync(i => i.InventoryLocationId == id && i.IsActive);

        if (inUse)
        {
            TempData["Error"] = "This value cannot be deactivated because active inventory items still use it.";
            return RedirectToAction(nameof(Index), new { type });
        }

        if (type == InventoryLookupType.UnitOfMeasure)
        {
            var uom = await _db.UnitsOfMeasure.FindAsync(id);
            if (uom is not null) uom.IsActive = false;
        }
        else
        {
            var loc = await _db.InventoryLocations.FindAsync(id);
            if (loc is not null) loc.IsActive = false;
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Inventory, $"Deactivate{type}", id.ToString());
        return RedirectToAction(nameof(Index), new { type });
    }

    // Real delete: blocked if any inventory item (active or not) still references this value.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(InventoryLookupType type, int id)
    {
        var inUse = type == InventoryLookupType.UnitOfMeasure
            ? await _db.InventoryItems.AnyAsync(i => i.UnitOfMeasureId == id)
            : await _db.InventoryItems.AnyAsync(i => i.InventoryLocationId == id);

        if (inUse)
        {
            TempData["Error"] = "This value cannot be deleted because inventory items still use it. Deactivate it instead.";
            return RedirectToAction(nameof(Index), new { type });
        }

        if (type == InventoryLookupType.UnitOfMeasure)
        {
            var uom = await _db.UnitsOfMeasure.FindAsync(id);
            if (uom is not null) _db.UnitsOfMeasure.Remove(uom);
        }
        else
        {
            var loc = await _db.InventoryLocations.FindAsync(id);
            if (loc is not null) _db.InventoryLocations.Remove(loc);
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Inventory, $"Delete{type}", id.ToString());
        TempData["Success"] = "Item deleted.";
        return RedirectToAction(nameof(Index), new { type });
    }
}
