using HotelResortMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>
/// Backs the header quick-search box (Section 3). Deliberately thin - it does not
/// reimplement search, it routes to whichever existing screen already searches that kind
/// of record (Guests/Index already supports ?search=, Rooms/Index already lists every
/// room). A reservation number is the one lookup nothing else already exposes, so this is
/// the only new query here.
/// </summary>
[Authorize]
public class SearchController : Controller
{
    private readonly ApplicationDbContext _db;

    public SearchController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Go(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return RedirectToAction("Index", "Dashboard");
        }

        var term = q.Trim();

        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(r => r.ReservationNumber == term);
        if (reservation is not null)
        {
            return RedirectToAction("Details", "Reservations", new { id = reservation.Id });
        }

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == term);
        if (room is not null)
        {
            return RedirectToAction("Edit", "Rooms", new { id = room.Id });
        }

        // Fall through to the existing guest name/phone search screen.
        return RedirectToAction("Index", "Guests", new { search = term });
    }
}
