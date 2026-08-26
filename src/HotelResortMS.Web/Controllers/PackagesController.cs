using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Operations;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 32: bundled package master data - see Package.cs for why components
/// are free-text rather than strict FKs at this phase.</summary>
[RequirePermission(SystemModules.Packages, PermissionAction.View)]
public class PackagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public PackagesController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var packages = await _db.Packages.Include(p => p.Components).OrderBy(p => p.Name).ToListAsync();
        return View(packages);
    }

    [RequirePermission(SystemModules.Packages, PermissionAction.Add)]
    public IActionResult Create() => View(new PackageEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Packages, PermissionAction.Add)]
    public async Task<IActionResult> Create(PackageEditViewModel model, string[] componentDescription, int[] componentQuantity)
    {
        if (!ModelState.IsValid) return View(model);

        var package = new Package
        {
            Name = model.Name,
            Description = model.Description,
            Price = model.Price,
            Capacity = model.Capacity,
            EffectiveDate = model.EffectiveDate,
            ExpirationDate = model.ExpirationDate,
            CreatedBy = User.Identity?.Name
        };

        foreach (var (desc, qty) in componentDescription.Zip(componentQuantity, (d, q) => (d, q)))
        {
            if (!string.IsNullOrWhiteSpace(desc))
            {
                package.Components.Add(new PackageComponent { Description = desc, Quantity = qty <= 0 ? 1 : qty });
            }
        }

        _db.Packages.Add(package);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Packages, "Create", package.Id.ToString(), newValues: new { package.Name, package.Price });
        TempData["Success"] = "Package created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Packages, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.Packages.Include(p => p.Components).FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return NotFound();

        return View(new PackageEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            Capacity = entity.Capacity,
            EffectiveDate = entity.EffectiveDate,
            ExpirationDate = entity.ExpirationDate,
            IsActive = entity.IsActive,
            Components = entity.Components.Select(c => new PackageComponentInput { Description = c.Description, Quantity = c.Quantity }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Packages, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(PackageEditViewModel model, string[] componentDescription, int[] componentQuantity)
    {
        var entity = await _db.Packages.Include(p => p.Components).FirstOrDefaultAsync(p => p.Id == model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.Price = model.Price;
        entity.Capacity = model.Capacity;
        entity.EffectiveDate = model.EffectiveDate;
        entity.ExpirationDate = model.ExpirationDate;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        _db.PackageComponents.RemoveRange(entity.Components);
        entity.Components.Clear();
        foreach (var (desc, qty) in componentDescription.Zip(componentQuantity, (d, q) => (d, q)))
        {
            if (!string.IsNullOrWhiteSpace(desc))
            {
                entity.Components.Add(new PackageComponent { Description = desc, Quantity = qty <= 0 ? 1 : qty });
            }
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Packages, "Update", entity.Id.ToString(), newValues: new { entity.Name, entity.Price });
        TempData["Success"] = "Package updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Packages, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.Packages.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Packages, "Deactivate", id.ToString());

        TempData["Success"] = "Package deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if any reservation has ever booked this package. Its own
    // Components are just line items of the package itself, so they're removed with it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Packages, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Packages.Include(p => p.Components).FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.Reservations.AnyAsync(r => r.PackageId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This package cannot be deleted because it has reservation history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.PackageComponents.RemoveRange(entity.Components);
        _db.Packages.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Packages, "Delete", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Package deleted.";
        return RedirectToAction(nameof(Index));
    }
}
