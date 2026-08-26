using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Amenities;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 14: Amenity master data CRUD plus its status board, mirroring Rooms.</summary>
[RequirePermission(SystemModules.Amenities, PermissionAction.View)]
public class AmenitiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public AmenitiesController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var amenities = await _db.Amenities.Include(a => a.AmenityCategory).OrderBy(a => a.Name).ToListAsync();
        return View(amenities);
    }

    public async Task<IActionResult> StatusBoard()
    {
        var amenities = await _db.Amenities.Include(a => a.AmenityCategory).Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();
        return View(amenities);
    }

    [RequirePermission(SystemModules.Amenities, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new AmenityEditViewModel();
        await PopulateCategoriesAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Amenities, PermissionAction.Add)]
    public async Task<IActionResult> Create(AmenityEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(model);
            return View(model);
        }

        var entity = new Amenity
        {
            Name = model.Name,
            AmenityCategoryId = model.AmenityCategoryId,
            Description = model.Description,
            Capacity = model.Capacity,
            HourlyRate = model.HourlyRate,
            DailyRate = model.DailyRate,
            RegularRate = model.RegularRate,
            WeekendRate = model.WeekendRate,
            HolidayRate = model.HolidayRate,
            SeasonalRate = model.SeasonalRate,
            MinimumHours = model.MinimumHours,
            AdditionalChargePerHour = model.AdditionalChargePerHour,
            Status = AmenityStatus.Available
        };
        _db.Amenities.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Amenities, "Create", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Amenity created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Amenities, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.Amenities.FindAsync(id);
        if (entity is null) return NotFound();

        var model = new AmenityEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            AmenityCategoryId = entity.AmenityCategoryId,
            Description = entity.Description,
            Capacity = entity.Capacity,
            HourlyRate = entity.HourlyRate,
            DailyRate = entity.DailyRate,
            RegularRate = entity.RegularRate,
            WeekendRate = entity.WeekendRate,
            HolidayRate = entity.HolidayRate,
            SeasonalRate = entity.SeasonalRate,
            MinimumHours = entity.MinimumHours,
            AdditionalChargePerHour = entity.AdditionalChargePerHour,
            IsActive = entity.IsActive
        };
        await PopulateCategoriesAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Amenities, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(AmenityEditViewModel model)
    {
        var entity = await _db.Amenities.FindAsync(model.Id);
        if (entity is null) return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(model);
            return View(model);
        }

        entity.Name = model.Name;
        entity.AmenityCategoryId = model.AmenityCategoryId;
        entity.Description = model.Description;
        entity.Capacity = model.Capacity;
        entity.HourlyRate = model.HourlyRate;
        entity.DailyRate = model.DailyRate;
        entity.RegularRate = model.RegularRate;
        entity.WeekendRate = model.WeekendRate;
        entity.HolidayRate = model.HolidayRate;
        entity.SeasonalRate = model.SeasonalRate;
        entity.MinimumHours = model.MinimumHours;
        entity.AdditionalChargePerHour = model.AdditionalChargePerHour;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Amenities, "Update", entity.Id.ToString());

        TempData["Success"] = "Amenity updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Amenities, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.Amenities.FindAsync(id);
        if (entity is null) return NotFound();

        if (entity.Status is AmenityStatus.Reserved or AmenityStatus.InUse)
        {
            TempData["Error"] = "This amenity cannot be deactivated while it is reserved or in use.";
            return RedirectToAction(nameof(Index));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Amenities, "Deactivate", id.ToString());

        TempData["Success"] = "Amenity deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if the amenity has ever been booked on a reservation.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Amenities, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Amenities.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.ReservationAmenities.AnyAsync(ra => ra.AmenityId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This amenity cannot be deleted because it has reservation history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.Amenities.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Amenities, "Delete", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Amenity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCategoriesAsync(AmenityEditViewModel model)
    {
        model.Categories = await _db.AmenityCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
    }
}
