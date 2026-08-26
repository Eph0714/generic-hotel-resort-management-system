using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Operations;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 23: configurable cancellation policies, applied by ReservationService
/// at cancellation time (Section 23) - kept under the Reservations permission module since
/// it's purely a Reservations concept, not a separate module in its own right.</summary>
[RequirePermission(SystemModules.Reservations, PermissionAction.View)]
public class CancellationPoliciesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public CancellationPoliciesController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var policies = await _db.CancellationPolicies.OrderBy(p => p.Name).ToListAsync();
        return View(policies);
    }

    [RequirePermission(SystemModules.Reservations, PermissionAction.Configure)]
    public IActionResult Create() => View(new CancellationPolicyEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Reservations, PermissionAction.Configure)]
    public async Task<IActionResult> Create(CancellationPolicyEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var entity = new CancellationPolicy
        {
            Name = model.Name,
            Description = model.Description,
            Type = model.Type,
            HoursBeforeCheckIn = model.HoursBeforeCheckIn,
            FeePercentage = model.FeePercentage,
            FixedFee = model.FixedFee,
            CreatedBy = User.Identity?.Name
        };
        _db.CancellationPolicies.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Reservations, "CreateCancellationPolicy", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Cancellation policy created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Reservations, PermissionAction.Configure)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.CancellationPolicies.FindAsync(id);
        if (entity is null) return NotFound();

        return View(new CancellationPolicyEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Type = entity.Type,
            HoursBeforeCheckIn = entity.HoursBeforeCheckIn,
            FeePercentage = entity.FeePercentage,
            FixedFee = entity.FixedFee,
            IsActive = entity.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Reservations, PermissionAction.Configure)]
    public async Task<IActionResult> Edit(CancellationPolicyEditViewModel model)
    {
        var entity = await _db.CancellationPolicies.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.Type = model.Type;
        entity.HoursBeforeCheckIn = model.HoursBeforeCheckIn;
        entity.FeePercentage = model.FeePercentage;
        entity.FixedFee = model.FixedFee;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Reservations, "UpdateCancellationPolicy", entity.Id.ToString());
        TempData["Success"] = "Cancellation policy updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Reservations, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var inUse = await _db.Reservations.AnyAsync(r => r.CancellationPolicyId == id
            && r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.CheckedOut);
        if (inUse)
        {
            TempData["Error"] = "This policy is attached to active reservations and cannot be deactivated.";
            return RedirectToAction(nameof(Index));
        }

        var entity = await _db.CancellationPolicies.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Reservations, "DeactivateCancellationPolicy", id.ToString());

        TempData["Success"] = "Cancellation policy deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if any reservation (past or present) references this policy.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Reservations, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.CancellationPolicies.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.Reservations.AnyAsync(r => r.CancellationPolicyId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This policy cannot be deleted because it has reservation history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.CancellationPolicies.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Reservations, "DeleteCancellationPolicy", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Cancellation policy deleted.";
        return RedirectToAction(nameof(Index));
    }
}
