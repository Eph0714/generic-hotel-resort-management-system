using HotelResortMS.Core.Common;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 24/25/28: Check-In, Guest Folio, Check-Out. All the actual workflow
/// logic lives in IFrontDeskService - this controller only translates HTTP <-> service
/// calls and turns exceptions into user-facing messages (Section 52).</summary>
[RequirePermission(SystemModules.FrontDesk, PermissionAction.View)]
public class FrontDeskController : Controller
{
    private readonly IFrontDeskService _frontDeskService;
    private readonly ApplicationDbContext _db;

    public FrontDeskController(IFrontDeskService frontDeskService, ApplicationDbContext db)
    {
        _frontDeskService = frontDeskService;
        _db = db;
    }

    /// <summary>Section 24: today's confirmed reservations awaiting check-in.</summary>
    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var arrivals = await _db.Reservations
            .Include(r => r.Guest)
            .Where(r => r.Status == Core.Entities.ReservationStatus.Confirmed && r.CheckInDate <= today)
            .OrderBy(r => r.CheckInDate)
            .ToListAsync();

        var inHouse = await _db.Reservations
            .Include(r => r.Guest)
            .Where(r => r.Status == Core.Entities.ReservationStatus.CheckedIn)
            .OrderBy(r => r.CheckOutDate)
            .ToListAsync();

        ViewBag.InHouse = inHouse;
        return View(arrivals);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.FrontDesk, PermissionAction.Add)]
    public async Task<IActionResult> CheckIn(int id)
    {
        try
        {
            var folio = await _frontDeskService.CheckInAsync(id, User.Identity?.Name ?? "Unknown");
            TempData["Success"] = $"Checked in. Folio {folio.FolioNumber} opened.";
            return RedirectToAction(nameof(Folio), new { reservationId = id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Details", "Reservations", new { id });
        }
    }

    public async Task<IActionResult> Folio(int reservationId)
    {
        var folio = await _frontDeskService.GetFolioForReservationAsync(reservationId);
        if (folio is null) return NotFound();
        return View(folio);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.FrontDesk, PermissionAction.Approve)]
    public async Task<IActionResult> CheckOut(int reservationId, bool authorizeOutstandingBalance = false)
    {
        try
        {
            await _frontDeskService.CheckOutAsync(reservationId, User.Identity?.Name ?? "Unknown", authorizeOutstandingBalance);
            TempData["Success"] = "Guest checked out successfully.";
            return RedirectToAction("Details", "Reservations", new { id = reservationId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Folio), new { reservationId });
        }
    }
}
