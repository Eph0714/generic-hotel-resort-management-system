using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Operations;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 31: Events/Function Hall bookings. Venue double-booking prevention
/// and revenue recognition go through IEventService.</summary>
[RequirePermission(SystemModules.Events, PermissionAction.View)]
public class EventsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IEventService _eventService;

    public EventsController(ApplicationDbContext db, IEventService eventService)
    {
        _db = db;
        _eventService = eventService;
    }

    public async Task<IActionResult> Index(EventStatus? status)
    {
        var query = _db.Events.Include(e => e.EventType).Include(e => e.EventVenue).Include(e => e.Guest).AsQueryable();
        if (status is not null) query = query.Where(e => e.Status == status);

        ViewBag.Status = status;
        var events = await query.OrderByDescending(e => e.StartDateTime).ToListAsync();
        return View(events);
    }

    [RequirePermission(SystemModules.Events, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new EventCreateViewModel { StartDateTime = DateTime.Today.AddHours(9), EndDateTime = DateTime.Today.AddHours(17) };
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Events, PermissionAction.Add)]
    public async Task<IActionResult> Create(EventCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        var ev = new Event
        {
            EventTypeId = model.EventTypeId,
            EventVenueId = model.EventVenueId,
            GuestId = model.GuestId,
            ClientName = model.ClientName,
            ClientContact = model.ClientContact,
            StartDateTime = model.StartDateTime,
            EndDateTime = model.EndDateTime,
            ExpectedGuests = model.ExpectedGuests,
            TotalAmount = model.TotalAmount,
            DepositAmount = model.DepositAmount,
            Notes = model.Notes,
            CreatedBy = User.Identity?.Name
        };

        try
        {
            ev = await _eventService.CreateEventAsync(ev);
            TempData["Success"] = $"Event {ev.EventNumber} created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateAsync(model);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Events, PermissionAction.Approve)]
    public async Task<IActionResult> Confirm(int id)
    {
        try { await _eventService.ConfirmAsync(id, User.Identity?.Name ?? "Unknown"); TempData["Success"] = "Event confirmed."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Events, PermissionAction.Edit)]
    public async Task<IActionResult> Complete(int id)
    {
        try { await _eventService.CompleteAsync(id, User.Identity?.Name ?? "Unknown"); TempData["Success"] = "Event completed and revenue recognized."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Events, PermissionAction.Delete)]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        try { await _eventService.CancelAsync(id, reason, User.Identity?.Name ?? "Unknown"); TempData["Success"] = "Event cancelled."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(EventCreateViewModel model)
    {
        model.EventTypes = await _db.EventTypes.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
        model.Venues = await _db.EventVenues.Where(v => v.IsActive).OrderBy(v => v.Name).ToListAsync();
        model.Guests = await _db.Guests.Where(g => g.IsActive).OrderBy(g => g.LastName).ToListAsync();
    }
}
