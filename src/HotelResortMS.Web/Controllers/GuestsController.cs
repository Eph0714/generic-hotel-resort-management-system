using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Guests;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 16: Guest Management CRUD, plus Section 57 - sensitive Senior
/// Citizen/PWD ID fields are only ever shown to authenticated staff with Guests.View
/// permission, never exposed on any public-facing surface.</summary>
[RequirePermission(SystemModules.Guests, PermissionAction.View)]
public class GuestsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public GuestsController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Guests.Where(g => !g.IsDeleted).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(g => (g.FirstName + " " + g.LastName).Contains(search) ||
                                      (g.Email != null && g.Email.Contains(search)) ||
                                      (g.Phone != null && g.Phone.Contains(search)));
        }
        ViewBag.Search = search;
        return View(await query.OrderBy(g => g.LastName).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var guest = await _db.Guests.FirstOrDefaultAsync(g => g.Id == id);
        if (guest is null) return NotFound();

        var reservations = await _db.Reservations
            .Where(r => r.GuestId == id)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();

        return View(new GuestDetailsViewModel
        {
            Guest = guest,
            Reservations = reservations,
            OutstandingBalance = reservations.Sum(r => r.BalanceDue)
        });
    }

    [RequirePermission(SystemModules.Guests, PermissionAction.Add)]
    public IActionResult Create() => View(new GuestEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Guests, PermissionAction.Add)]
    public async Task<IActionResult> Create(GuestEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        await CreateGuestAsync(model);
        TempData["Success"] = "Guest created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Guests, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var g = await _db.Guests.FindAsync(id);
        if (g is null) return NotFound();

        return View(ToEditViewModel(g));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Guests, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(GuestEditViewModel model)
    {
        var g = await _db.Guests.FindAsync(model.Id);
        if (g is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        await UpdateGuestAsync(g, model);
        TempData["Success"] = "Guest updated.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8/9: a guest with any reservation history is archived, never deleted -
    // their stay/payment/discount history must remain intact for reporting/accounting.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Guests, PermissionAction.Delete)]
    public async Task<IActionResult> Archive(int id)
    {
        var g = await _db.Guests.FindAsync(id);
        if (g is null) return NotFound();

        await ArchiveGuestAsync(g);
        TempData["Success"] = "Guest archived.";
        return RedirectToAction(nameof(Index));
    }

    // ================= Browse Guests picker (New Reservation > Select Guest) =================
    // Section 16/20: lets staff add/edit/archive a guest directly from the "Browse
    // Guests" picker on the New Reservation screen, without losing their in-progress
    // reservation form. These three endpoints are thin JSON wrappers around the exact
    // same create/update/archive logic as the classic Guests Create/Edit/Archive actions
    // above (via the shared private helpers) - no business logic is duplicated, only the
    // response format (JSON instead of a redirect) differs. "Delete" in the picker maps
    // to the same Archive (soft-delete) used everywhere else for Guests - this entity has
    // no hard-delete action anywhere in the app, since a guest's reservation/payment/
    // discount history must never be removable (Section 8/9).

    // GET so the picker's Edit form can be pre-filled with every field (including the
    // ones not shown in the compact quick-edit UI, e.g. Address/GuestType/Notes/
    // IsActive) before posting to EditJson below - otherwise those fields would
    // silently get wiped back to blank/default by a submit that never included them.
    [RequirePermission(SystemModules.Guests, PermissionAction.Edit)]
    public async Task<IActionResult> DetailsJson(int id)
    {
        var g = await _db.Guests.FindAsync(id);
        if (g is null) return NotFound();
        return Json(ToEditViewModel(g));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Guests, PermissionAction.Add)]
    public async Task<IActionResult> CreateJson(GuestEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        var entity = await CreateGuestAsync(model);
        return Json(new { success = true, guest = ToPickerJson(entity) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Guests, PermissionAction.Edit)]
    public async Task<IActionResult> EditJson(GuestEditViewModel model)
    {
        var g = await _db.Guests.FindAsync(model.Id);
        if (g is null) return Json(new { success = false, errors = new[] { "Guest not found." } });
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        await UpdateGuestAsync(g, model);
        return Json(new { success = true, guest = ToPickerJson(g) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Guests, PermissionAction.Delete)]
    public async Task<IActionResult> ArchiveJson(int id)
    {
        var g = await _db.Guests.FindAsync(id);
        if (g is null) return Json(new { success = false, errors = new[] { "Guest not found." } });

        await ArchiveGuestAsync(g);
        return Json(new { success = true, id });
    }

    private static object ToPickerJson(Guest g) => new { id = g.Id, firstName = g.FirstName, lastName = g.LastName, email = g.Email, phone = g.Phone };

    private static GuestEditViewModel ToEditViewModel(Guest g) => new()
    {
        Id = g.Id,
        FirstName = g.FirstName,
        LastName = g.LastName,
        Email = g.Email,
        Phone = g.Phone,
        Address = g.Address,
        City = g.City,
        Country = g.Country,
        GuestType = g.GuestType,
        CompanyName = g.CompanyName,
        IsSeniorCitizen = g.IsSeniorCitizen,
        SeniorCitizenIdNumber = g.SeniorCitizenIdNumber,
        IsPwd = g.IsPwd,
        PwdIdNumber = g.PwdIdNumber,
        Notes = g.Notes,
        IsActive = g.IsActive
    };

    private async Task<Guest> CreateGuestAsync(GuestEditViewModel model)
    {
        var entity = new Guest
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            Phone = model.Phone,
            Address = model.Address,
            City = model.City,
            Country = model.Country,
            GuestType = model.GuestType,
            CompanyName = model.CompanyName,
            IsSeniorCitizen = model.IsSeniorCitizen,
            SeniorCitizenIdNumber = model.SeniorCitizenIdNumber,
            IsPwd = model.IsPwd,
            PwdIdNumber = model.PwdIdNumber,
            Notes = model.Notes,
            CreatedBy = User.Identity?.Name
        };
        _db.Guests.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Guests, "Create", entity.Id.ToString(), newValues: new { entity.FirstName, entity.LastName });
        return entity;
    }

    private async Task UpdateGuestAsync(Guest g, GuestEditViewModel model)
    {
        g.FirstName = model.FirstName;
        g.LastName = model.LastName;
        g.Email = model.Email;
        g.Phone = model.Phone;
        g.Address = model.Address;
        g.City = model.City;
        g.Country = model.Country;
        g.GuestType = model.GuestType;
        g.CompanyName = model.CompanyName;
        g.IsSeniorCitizen = model.IsSeniorCitizen;
        g.SeniorCitizenIdNumber = model.SeniorCitizenIdNumber;
        g.IsPwd = model.IsPwd;
        g.PwdIdNumber = model.PwdIdNumber;
        g.Notes = model.Notes;
        g.IsActive = model.IsActive;
        g.UpdatedAt = DateTime.UtcNow;
        g.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Guests, "Update", g.Id.ToString());
    }

    private async Task ArchiveGuestAsync(Guest g)
    {
        g.IsActive = false;
        g.ArchivedAt = DateTime.UtcNow;
        g.ArchivedBy = User.Identity?.Name;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Guests, "Archive", g.Id.ToString(), reason: "Archived by administrator");
    }
}
