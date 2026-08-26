using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Operations;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 6/30: Maintenance Category master data (Electrical, Plumbing, HVAC, ...).</summary>
[RequirePermission(SystemModules.Maintenance, PermissionAction.View)]
public class MaintenanceCategoriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public MaintenanceCategoriesController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _db.MaintenanceCategories.OrderBy(c => c.Name).ToListAsync();
        return View(categories);
    }

    [RequirePermission(SystemModules.Maintenance, PermissionAction.Add)]
    public IActionResult Create() => View(new MaintenanceCategoryEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Add)]
    public async Task<IActionResult> Create(MaintenanceCategoryEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        _db.MaintenanceCategories.Add(new MaintenanceCategory { Name = model.Name, Description = model.Description });
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Maintenance, "CreateCategory", newValues: new { model.Name });
        TempData["Success"] = "Maintenance category created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Maintenance, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.MaintenanceCategories.FindAsync(id);
        if (entity is null) return NotFound();
        return View(new MaintenanceCategoryEditViewModel { Id = entity.Id, Name = entity.Name, Description = entity.Description, IsActive = entity.IsActive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(MaintenanceCategoryEditViewModel model)
    {
        var entity = await _db.MaintenanceCategories.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.IsActive = model.IsActive;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Maintenance, "UpdateCategory", entity.Id.ToString());
        TempData["Success"] = "Maintenance category updated.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8: a category still in use by an unresolved request cannot be deactivated
    // blind - completed/cancelled history alone does not block it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var openCount = await _db.MaintenanceRequests.CountAsync(m => m.MaintenanceCategoryId == id
            && m.Status != MaintenanceRequestStatus.Completed && m.Status != MaintenanceRequestStatus.Cancelled);
        if (openCount > 0)
        {
            TempData["Error"] = $"This category cannot be deactivated because {openCount} open request(s) still use it.";
            return RedirectToAction(nameof(Index));
        }

        var entity = await _db.MaintenanceCategories.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Maintenance, "DeactivateCategory", id.ToString());

        TempData["Success"] = "Maintenance category deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if any maintenance request (open or completed) references it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.MaintenanceCategories.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.MaintenanceRequests.AnyAsync(m => m.MaintenanceCategoryId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This category cannot be deleted because it has maintenance request history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.MaintenanceCategories.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Maintenance, "DeleteCategory", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Maintenance category deleted.";
        return RedirectToAction(nameof(Index));
    }
}
