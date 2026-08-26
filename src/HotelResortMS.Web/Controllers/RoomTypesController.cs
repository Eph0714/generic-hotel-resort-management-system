using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Rooms;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 6/13/15: Room Type master data, including its default rates. Editing a
/// rate here only changes the *default* going forward - already-booked ReservationRoom
/// rows keep their historical snapshot (Section 15).</summary>
[RequirePermission(SystemModules.Rooms, PermissionAction.View)]
public class RoomTypesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public RoomTypesController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var types = await _db.RoomTypes.OrderBy(r => r.Name).ToListAsync();
        return View(types);
    }

    [RequirePermission(SystemModules.Rooms, PermissionAction.Add)]
    public IActionResult Create() => View(new RoomTypeEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Rooms, PermissionAction.Add)]
    public async Task<IActionResult> Create(RoomTypeEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var entity = new RoomType
        {
            Name = model.Name,
            Description = model.Description,
            BaseCapacity = model.BaseCapacity,
            RegularRate = model.RegularRate,
            WeekendRate = model.WeekendRate,
            HolidayRate = model.HolidayRate,
            SeasonalRate = model.SeasonalRate,
            ExtraPersonRate = model.ExtraPersonRate
        };
        _db.RoomTypes.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Rooms, "CreateRoomType", entity.Id.ToString(), newValues: new { entity.Name });
        TempData["Success"] = "Room type created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Rooms, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.RoomTypes.FindAsync(id);
        if (entity is null) return NotFound();

        return View(new RoomTypeEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            BaseCapacity = entity.BaseCapacity,
            RegularRate = entity.RegularRate,
            WeekendRate = entity.WeekendRate,
            HolidayRate = entity.HolidayRate,
            SeasonalRate = entity.SeasonalRate,
            ExtraPersonRate = entity.ExtraPersonRate,
            IsActive = entity.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Rooms, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(RoomTypeEditViewModel model)
    {
        var entity = await _db.RoomTypes.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        var oldValues = new { entity.RegularRate, entity.WeekendRate, entity.HolidayRate, entity.SeasonalRate };

        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.BaseCapacity = model.BaseCapacity;
        entity.RegularRate = model.RegularRate;
        entity.WeekendRate = model.WeekendRate;
        entity.HolidayRate = model.HolidayRate;
        entity.SeasonalRate = model.SeasonalRate;
        entity.ExtraPersonRate = model.ExtraPersonRate;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Rooms, "UpdateRoomType", entity.Id.ToString(), oldValues,
            new { entity.RegularRate, entity.WeekendRate, entity.HolidayRate, entity.SeasonalRate });

        TempData["Success"] = "Room type updated.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8: a room type in use by any room cannot be deleted/deactivated blind -
    // rooms referencing it would be left without a valid type.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Rooms, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.RoomTypes.FindAsync(id);
        if (entity is null) return NotFound();

        var roomCount = await _db.Rooms.CountAsync(r => r.RoomTypeId == id && r.IsActive);
        if (roomCount > 0)
        {
            TempData["Error"] = $"This room type cannot be deactivated because {roomCount} active room(s) still use it.";
            return RedirectToAction(nameof(Index));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Rooms, "DeactivateRoomType", id.ToString());

        TempData["Success"] = "Room type deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if any room (active or not) still references this type, since
    // removing it would leave that room without a valid Room Type.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Rooms, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.RoomTypes.FindAsync(id);
        if (entity is null) return NotFound();

        var roomCount = await _db.Rooms.CountAsync(r => r.RoomTypeId == id);
        if (roomCount > 0)
        {
            TempData["Error"] = $"This room type cannot be deleted because {roomCount} room(s) still use it. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.RoomTypes.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Rooms, "DeleteRoomType", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Room type deleted.";
        return RedirectToAction(nameof(Index));
    }
}
