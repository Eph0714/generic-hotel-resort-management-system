using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Operations;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 30: physical assets that can need maintenance.</summary>
[RequirePermission(SystemModules.Maintenance, PermissionAction.View)]
public class EquipmentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public EquipmentController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var equipment = await _db.Equipment.Include(e => e.Room).OrderBy(e => e.Name).ToListAsync();
        return View(equipment);
    }

    [RequirePermission(SystemModules.Maintenance, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new EquipmentEditViewModel();
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Add)]
    public async Task<IActionResult> Create(EquipmentEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        var entity = new Equipment { Name = model.Name, Description = model.Description, RoomId = model.RoomId, CreatedBy = User.Identity?.Name };
        _db.Equipment.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Maintenance, "CreateEquipment", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Equipment created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Maintenance, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.Equipment.FindAsync(id);
        if (entity is null) return NotFound();

        var model = new EquipmentEditViewModel { Id = entity.Id, Name = entity.Name, Description = entity.Description, RoomId = entity.RoomId, IsActive = entity.IsActive };
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(EquipmentEditViewModel model)
    {
        var entity = await _db.Equipment.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.RoomId = model.RoomId;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Maintenance, "UpdateEquipment", entity.Id.ToString());
        TempData["Success"] = "Equipment updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.Equipment.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Maintenance, "DeactivateEquipment", id.ToString());

        TempData["Success"] = "Equipment deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if any maintenance request has ever been logged against it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Equipment.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.MaintenanceRequests.AnyAsync(m => m.EquipmentId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This equipment cannot be deleted because it has maintenance history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.Equipment.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Maintenance, "DeleteEquipment", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Equipment deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(EquipmentEditViewModel model)
    {
        model.Rooms = await _db.Rooms.Where(r => r.IsActive).OrderBy(r => r.RoomNumber).ToListAsync();
    }
}
