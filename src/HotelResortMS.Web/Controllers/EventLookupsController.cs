using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Operations;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 6/31: Event Types and Event Venues master data - one generic
/// controller instead of two near-identical ones.</summary>
[RequirePermission(SystemModules.Events, PermissionAction.View)]
public class EventLookupsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public EventLookupsController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(EventLookupType type)
    {
        ViewBag.Type = type;
        var items = type == EventLookupType.EventType
            ? await _db.EventTypes.Select(x => new EventLookupItemViewModel { Id = x.Id, Name = x.Name, Description = x.Description, IsActive = x.IsActive }).ToListAsync()
            : await _db.EventVenues.Select(x => new EventLookupItemViewModel { Id = x.Id, Name = x.Name, Description = x.Description, Capacity = x.Capacity, IsActive = x.IsActive }).ToListAsync();
        return View(items);
    }

    public IActionResult Create(EventLookupType type) => View(new EventLookupEditViewModel { Type = type });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Events, PermissionAction.Add)]
    public async Task<IActionResult> Create(EventLookupEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (model.Type == EventLookupType.EventType)
        {
            _db.EventTypes.Add(new EventType { Name = model.Name, Description = model.Description });
        }
        else
        {
            _db.EventVenues.Add(new EventVenue { Name = model.Name, Description = model.Description, Capacity = model.Capacity });
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Events, $"Create{model.Type}", newValues: new { model.Name });
        return RedirectToAction(nameof(Index), new { type = model.Type });
    }

    public async Task<IActionResult> Edit(EventLookupType type, int id)
    {
        if (type == EventLookupType.EventType)
        {
            var t = await _db.EventTypes.FindAsync(id);
            if (t is null) return NotFound();
            return View(new EventLookupEditViewModel { Id = t.Id, Name = t.Name, Description = t.Description, Type = type });
        }
        var v = await _db.EventVenues.FindAsync(id);
        if (v is null) return NotFound();
        return View(new EventLookupEditViewModel { Id = v.Id, Name = v.Name, Description = v.Description, Capacity = v.Capacity, Type = type });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Events, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(EventLookupEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (model.Type == EventLookupType.EventType)
        {
            var t = await _db.EventTypes.FindAsync(model.Id);
            if (t is null) return NotFound();
            t.Name = model.Name; t.Description = model.Description;
        }
        else
        {
            var v = await _db.EventVenues.FindAsync(model.Id);
            if (v is null) return NotFound();
            v.Name = model.Name; v.Description = model.Description; v.Capacity = model.Capacity;
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Events, $"Update{model.Type}", model.Id.ToString());
        return RedirectToAction(nameof(Index), new { type = model.Type });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Events, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(EventLookupType type, int id)
    {
        if (type == EventLookupType.EventType)
        {
            var t = await _db.EventTypes.FindAsync(id);
            if (t is not null) t.IsActive = false;
        }
        else
        {
            var v = await _db.EventVenues.FindAsync(id);
            if (v is not null) v.IsActive = false;
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Events, $"Deactivate{type}", id.ToString());
        return RedirectToAction(nameof(Index), new { type });
    }

    // Real delete: blocked if any event references this type/venue.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Events, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(EventLookupType type, int id)
    {
        var inUse = type == EventLookupType.EventType
            ? await _db.Events.AnyAsync(e => e.EventTypeId == id)
            : await _db.Events.AnyAsync(e => e.EventVenueId == id);

        if (inUse)
        {
            TempData["Error"] = "This value cannot be deleted because events still use it. Deactivate it instead.";
            return RedirectToAction(nameof(Index), new { type });
        }

        if (type == EventLookupType.EventType)
        {
            var t = await _db.EventTypes.FindAsync(id);
            if (t is not null) _db.EventTypes.Remove(t);
        }
        else
        {
            var v = await _db.EventVenues.FindAsync(id);
            if (v is not null) _db.EventVenues.Remove(v);
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Events, $"Delete{type}", id.ToString());
        TempData["Success"] = "Item deleted.";
        return RedirectToAction(nameof(Index), new { type });
    }
}
