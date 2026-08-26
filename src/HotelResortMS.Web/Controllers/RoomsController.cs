using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Rooms;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 13: Room master data CRUD plus the visual Room Status Board. Status
/// itself is never edited directly from this controller's Edit form - it only changes
/// through IRoomService, driven by Reservation/Check-In/Check-Out/Housekeeping/Maintenance.</summary>
[RequirePermission(SystemModules.Rooms, PermissionAction.View)]
public class RoomsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public RoomsController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var rooms = await _db.Rooms
            .Include(r => r.RoomType)
            .OrderBy(r => r.RoomNumber)
            .Select(r => new RoomListItemViewModel
            {
                Id = r.Id,
                RoomNumber = r.RoomNumber,
                RoomName = r.RoomName,
                RoomTypeName = r.RoomType!.Name,
                Status = r.Status,
                IsActive = r.IsActive
            })
            .ToListAsync();

        return View(rooms);
    }

    /// <summary>Section 13: the real-time Room Status Board - a card per room, color-coded
    /// by status, refreshed whenever Reservation/Front Desk/Housekeeping change it.</summary>
    public async Task<IActionResult> StatusBoard()
    {
        var rooms = await _db.Rooms
            .Include(r => r.RoomType)
            .Where(r => r.IsActive)
            .OrderBy(r => r.RoomNumber)
            .ToListAsync();
        return View(rooms);
    }

    [RequirePermission(SystemModules.Rooms, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new RoomEditViewModel();
        await PopulateLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Rooms, PermissionAction.Add)]
    public async Task<IActionResult> Create(RoomEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(model);
            return View(model);
        }

        var entity = new Room
        {
            RoomNumber = model.RoomNumber,
            RoomName = model.RoomName,
            RoomTypeId = model.RoomTypeId,
            BedTypeId = model.BedTypeId,
            FloorAreaId = model.FloorAreaId,
            Capacity = model.Capacity,
            Description = model.Description,
            Status = RoomStatus.Available
        };
        _db.Rooms.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Rooms, "Create", entity.Id.ToString(), newValues: new { entity.RoomNumber });
        TempData["Success"] = "Room created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Rooms, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.Rooms.FindAsync(id);
        if (entity is null) return NotFound();

        var model = new RoomEditViewModel
        {
            Id = entity.Id,
            RoomNumber = entity.RoomNumber,
            RoomName = entity.RoomName,
            RoomTypeId = entity.RoomTypeId,
            BedTypeId = entity.BedTypeId,
            FloorAreaId = entity.FloorAreaId,
            Capacity = entity.Capacity,
            Description = entity.Description,
            IsActive = entity.IsActive
        };
        await PopulateLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Rooms, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(RoomEditViewModel model)
    {
        var entity = await _db.Rooms.FindAsync(model.Id);
        if (entity is null) return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(model);
            return View(model);
        }

        entity.RoomNumber = model.RoomNumber;
        entity.RoomName = model.RoomName;
        entity.RoomTypeId = model.RoomTypeId;
        entity.BedTypeId = model.BedTypeId;
        entity.FloorAreaId = model.FloorAreaId;
        entity.Capacity = model.Capacity;
        entity.Description = model.Description;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Rooms, "Update", entity.Id.ToString());

        TempData["Success"] = "Room updated.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8: blocked if the room has any reservation history at all - deactivate instead.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Rooms, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.Rooms.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.ReservationRooms.AnyAsync(rr => rr.RoomId == id);
        if (hasHistory && entity.Status is RoomStatus.Occupied or RoomStatus.Reserved)
        {
            TempData["Error"] = "This room cannot be deactivated because it has an active reservation. Check it out or cancel the reservation first.";
            return RedirectToAction(nameof(Index));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Rooms, "Deactivate", id.ToString());

        TempData["Success"] = "Room deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: only allowed when the room has never had a single reservation,
    // housekeeping task, or maintenance request against it - otherwise removing the row
    // would orphan/erase that history, so it's blocked in favor of Deactivate instead.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Rooms, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Rooms.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.ReservationRooms.AnyAsync(rr => rr.RoomId == id)
            || await _db.HousekeepingTasks.AnyAsync(h => h.RoomId == id)
            || await _db.MaintenanceRequests.AnyAsync(m => m.RoomId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This room cannot be deleted because it has reservation, housekeeping, or maintenance history. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.Rooms.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Rooms, "Delete", id.ToString(), oldValues: new { entity.RoomNumber });

        TempData["Success"] = "Room deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateLookupsAsync(RoomEditViewModel model)
    {
        model.RoomTypes = await _db.RoomTypes.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync();
        model.BedTypes = await _db.BedTypes.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync();
        model.FloorAreas = await _db.FloorAreas.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync();
    }
}
