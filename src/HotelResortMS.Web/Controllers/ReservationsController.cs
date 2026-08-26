using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Reservations;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 20/21/22/23: Reservation CRUD. All availability checking and room
/// assignment goes through IReservationService - this controller never touches
/// ReservationRoom rows directly.</summary>
[RequirePermission(SystemModules.Reservations, PermissionAction.View)]
public class ReservationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IReservationService _reservationService;
    private readonly IAuditService _auditService;

    public ReservationsController(ApplicationDbContext db, IReservationService reservationService, IAuditService auditService)
    {
        _db = db;
        _reservationService = reservationService;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.Reservations.Include(r => r.Guest).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReservationStatus>(status, out var s))
        {
            query = query.Where(r => r.Status == s);
        }
        ViewBag.Status = status;
        var reservations = await query.OrderByDescending(r => r.ReservationDate).ToListAsync();
        return View(reservations);
    }

    public async Task<IActionResult> Details(int id)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Guest)
            .Include(r => r.CancellationPolicy)
            .Include(r => r.Package).ThenInclude(p => p!.Components)
            .Include(r => r.Rooms).ThenInclude(rr => rr.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();
        return View(reservation);
    }

    [RequirePermission(SystemModules.Reservations, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new ReservationCreateViewModel();
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Reservations, PermissionAction.Add)]
    public async Task<IActionResult> Create(ReservationCreateViewModel model)
    {
        if (model.CheckOutDate <= model.CheckInDate)
        {
            ModelState.AddModelError(nameof(model.CheckOutDate), "Check-out date must be after check-in date.");
        }
        if (model.RoomIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.RoomIds), "Select at least one room.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        var reservation = new Reservation
        {
            GuestId = model.GuestId,
            CheckInDate = model.CheckInDate,
            CheckOutDate = model.CheckOutDate,
            NumberOfGuests = model.NumberOfGuests,
            DiscountAmount = model.DiscountAmount,
            AmountPaid = model.AmountPaid,
            SpecialRequests = model.SpecialRequests,
            Notes = model.Notes,
            CancellationPolicyId = model.CancellationPolicyId,
            PackageId = model.PackageId
        };

        try
        {
            reservation = await _reservationService.CreateReservationAsync(reservation, model.RoomIds);
        }
        catch (InvalidOperationException ex)
        {
            // Section 21: surfaces the double-booking conflict as a clear message rather
            // than a raw exception (Section 52).
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateAsync(model);
            return View(model);
        }

        TempData["Success"] = $"Reservation {reservation.ReservationNumber} created.";
        return RedirectToAction(nameof(Details), new { id = reservation.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Reservations, PermissionAction.Delete)]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        try
        {
            await _reservationService.CancelReservationAsync(id, reason, User.Identity?.Name ?? "Unknown");
            TempData["Success"] = "Reservation cancelled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>AJAX endpoint backing the room picker on the Create form - filters to rooms
    /// actually available for the selected date range (Section 21).</summary>
    [HttpGet]
    public async Task<IActionResult> AvailableRooms(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn) return BadRequest("Check-out must be after check-in.");
        var rooms = await _reservationService.GetAvailableRoomsAsync(checkIn, checkOut);
        return Json(rooms.Select(r => new { r.Id, r.RoomNumber, r.RoomName, RoomType = r.RoomType?.Name }));
    }

    private async Task PopulateAsync(ReservationCreateViewModel model)
    {
        model.Guests = await _db.Guests.Where(g => g.IsActive).OrderBy(g => g.LastName).ToListAsync();
        model.AvailableRooms = await _reservationService.GetAvailableRoomsAsync(model.CheckInDate, model.CheckOutDate);
        model.CancellationPolicies = await _db.CancellationPolicies.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        model.Packages = await _db.Packages.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
    }
}
