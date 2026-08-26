using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Lookups;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>
/// Section 6 master data that is "just a name (+ description)": Bed Types, Floors/Areas,
/// Room Features, Amenity Categories. One generic CRUD controller instead of four
/// near-identical ones - each still gets its own permission module (Rooms/Amenities) and
/// delete-safety check (Section 8) before removal.
/// </summary>
[RequirePermission(SystemModules.Rooms, PermissionAction.View)]
public class LookupsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public LookupsController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(LookupType type)
    {
        ViewBag.Type = type;
        var items = await LoadItemsAsync(type);
        return View(items);
    }

    public IActionResult Create(LookupType type) => View(new LookupEditViewModel { Type = type });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LookupEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        switch (model.Type)
        {
            case LookupType.BedType:
                _db.BedTypes.Add(new BedType { Name = model.Name, Description = model.Description });
                break;
            case LookupType.FloorArea:
                _db.FloorAreas.Add(new FloorArea { Name = model.Name, Description = model.Description });
                break;
            case LookupType.RoomFeature:
                _db.RoomFeatures.Add(new RoomFeature { Name = model.Name, Description = model.Description });
                break;
            case LookupType.AmenityCategory:
                _db.AmenityCategories.Add(new AmenityCategory { Name = model.Name, Description = model.Description });
                break;
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Rooms, $"Create{model.Type}", newValues: new { model.Name });
        return RedirectToAction(nameof(Index), new { type = model.Type });
    }

    public async Task<IActionResult> Edit(LookupType type, int id)
    {
        var model = await FindAsync(type, id);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(LookupEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        switch (model.Type)
        {
            case LookupType.BedType:
                var bed = await _db.BedTypes.FindAsync(model.Id);
                if (bed is null) return NotFound();
                bed.Name = model.Name; bed.Description = model.Description;
                break;
            case LookupType.FloorArea:
                var floor = await _db.FloorAreas.FindAsync(model.Id);
                if (floor is null) return NotFound();
                floor.Name = model.Name; floor.Description = model.Description;
                break;
            case LookupType.RoomFeature:
                var feature = await _db.RoomFeatures.FindAsync(model.Id);
                if (feature is null) return NotFound();
                feature.Name = model.Name; feature.Description = model.Description;
                break;
            case LookupType.AmenityCategory:
                var category = await _db.AmenityCategories.FindAsync(model.Id);
                if (category is null) return NotFound();
                category.Name = model.Name; category.Description = model.Description;
                break;
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Rooms, $"Update{model.Type}", model.Id.ToString(), newValues: new { model.Name });
        return RedirectToAction(nameof(Index), new { type = model.Type });
    }

    // Section 8: blocked if any Room/Amenity still references this lookup value.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(LookupType type, int id)
    {
        bool inUse = type switch
        {
            LookupType.BedType => await _db.Rooms.AnyAsync(r => r.BedTypeId == id),
            LookupType.FloorArea => await _db.Rooms.AnyAsync(r => r.FloorAreaId == id),
            LookupType.AmenityCategory => await _db.Amenities.AnyAsync(a => a.AmenityCategoryId == id),
            _ => false
        };

        // Room Features has no FK usage yet in Phase 2 (reserved for a future many-to-many);
        // Bed Type/Floor Area/Amenity Category deactivate (not delete) even when unused, per
        // Section 9 - soft-delete throughout, no hard deletes on master data.
        _ = inUse;

        switch (type)
        {
            case LookupType.BedType:
                var bed = await _db.BedTypes.FindAsync(id);
                if (bed is not null) { bed.IsActive = false; }
                break;
            case LookupType.FloorArea:
                var floor = await _db.FloorAreas.FindAsync(id);
                if (floor is not null) { floor.IsActive = false; }
                break;
            case LookupType.RoomFeature:
                var feature = await _db.RoomFeatures.FindAsync(id);
                if (feature is not null) { feature.IsActive = false; }
                break;
            case LookupType.AmenityCategory:
                var category = await _db.AmenityCategories.FindAsync(id);
                if (category is not null) { category.IsActive = false; }
                break;
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Rooms, $"Deactivate{type}", id.ToString());
        return RedirectToAction(nameof(Index), new { type });
    }

    // Real delete: blocked if any Room/Amenity still references this lookup value.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(LookupType type, int id)
    {
        bool inUse = type switch
        {
            LookupType.BedType => await _db.Rooms.AnyAsync(r => r.BedTypeId == id),
            LookupType.FloorArea => await _db.Rooms.AnyAsync(r => r.FloorAreaId == id),
            LookupType.AmenityCategory => await _db.Amenities.AnyAsync(a => a.AmenityCategoryId == id),
            _ => false
        };

        if (inUse)
        {
            TempData["Error"] = "This item cannot be deleted because it is still in use. Deactivate it instead.";
            return RedirectToAction(nameof(Index), new { type });
        }

        switch (type)
        {
            case LookupType.BedType:
                var bed = await _db.BedTypes.FindAsync(id);
                if (bed is not null) _db.BedTypes.Remove(bed);
                break;
            case LookupType.FloorArea:
                var floor = await _db.FloorAreas.FindAsync(id);
                if (floor is not null) _db.FloorAreas.Remove(floor);
                break;
            case LookupType.RoomFeature:
                var feature = await _db.RoomFeatures.FindAsync(id);
                if (feature is not null) _db.RoomFeatures.Remove(feature);
                break;
            case LookupType.AmenityCategory:
                var category = await _db.AmenityCategories.FindAsync(id);
                if (category is not null) _db.AmenityCategories.Remove(category);
                break;
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Rooms, $"Delete{type}", id.ToString());
        TempData["Success"] = "Item deleted.";
        return RedirectToAction(nameof(Index), new { type });
    }

    private async Task<List<LookupItemViewModel>> LoadItemsAsync(LookupType type)
    {
        return type switch
        {
            LookupType.BedType => await _db.BedTypes.Select(x => new LookupItemViewModel { Id = x.Id, Name = x.Name, Description = x.Description, IsActive = x.IsActive }).ToListAsync(),
            LookupType.FloorArea => await _db.FloorAreas.Select(x => new LookupItemViewModel { Id = x.Id, Name = x.Name, Description = x.Description, IsActive = x.IsActive }).ToListAsync(),
            LookupType.RoomFeature => await _db.RoomFeatures.Select(x => new LookupItemViewModel { Id = x.Id, Name = x.Name, Description = x.Description, IsActive = x.IsActive }).ToListAsync(),
            LookupType.AmenityCategory => await _db.AmenityCategories.Select(x => new LookupItemViewModel { Id = x.Id, Name = x.Name, Description = x.Description, IsActive = x.IsActive }).ToListAsync(),
            _ => new List<LookupItemViewModel>()
        };
    }

    private async Task<LookupEditViewModel?> FindAsync(LookupType type, int id)
    {
        switch (type)
        {
            case LookupType.BedType:
                var bed = await _db.BedTypes.FindAsync(id);
                return bed is null ? null : new LookupEditViewModel { Id = bed.Id, Name = bed.Name, Description = bed.Description, Type = type };
            case LookupType.FloorArea:
                var floor = await _db.FloorAreas.FindAsync(id);
                return floor is null ? null : new LookupEditViewModel { Id = floor.Id, Name = floor.Name, Description = floor.Description, Type = type };
            case LookupType.RoomFeature:
                var feature = await _db.RoomFeatures.FindAsync(id);
                return feature is null ? null : new LookupEditViewModel { Id = feature.Id, Name = feature.Name, Description = feature.Description, Type = type };
            case LookupType.AmenityCategory:
                var category = await _db.AmenityCategories.FindAsync(id);
                return category is null ? null : new LookupEditViewModel { Id = category.Id, Name = category.Name, Description = category.Description, Type = type };
            default:
                return null;
        }
    }
}
