using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 29: the housekeeping task board. All status transitions go through
/// IHousekeepingService, which also drives the Room's own Status (Section 13).</summary>
[RequirePermission(SystemModules.Housekeeping, PermissionAction.View)]
public class HousekeepingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IHousekeepingService _housekeepingService;
    private readonly UserManager<Core.Entities.Identity.ApplicationUser> _userManager;

    public HousekeepingController(ApplicationDbContext db, IHousekeepingService housekeepingService, UserManager<Core.Entities.Identity.ApplicationUser> userManager)
    {
        _db = db;
        _housekeepingService = housekeepingService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(HousekeepingStatus? status)
    {
        var query = _db.HousekeepingTasks
            .Include(t => t.Room)
            .Include(t => t.AssignedToUser)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(t => t.Status == status);
        }
        else
        {
            // Default view: only the active pipeline, not the (potentially large) history
            // of already-Ready tasks.
            query = query.Where(t => t.Status != HousekeepingStatus.Ready);
        }

        ViewBag.Status = status;
        ViewBag.Staff = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();

        var tasks = await query.OrderBy(t => t.Status).ThenBy(t => t.CreatedAt).ToListAsync();
        return View(tasks);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Housekeeping, PermissionAction.Edit)]
    public async Task<IActionResult> Assign(int id, string assignedToUserId)
    {
        await _housekeepingService.AssignAsync(id, assignedToUserId, User.Identity?.Name ?? "Unknown");
        TempData["Success"] = "Task assigned.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Housekeeping, PermissionAction.Edit)]
    public async Task<IActionResult> Start(int id)
    {
        try
        {
            await _housekeepingService.StartCleaningAsync(id, User.Identity?.Name ?? "Unknown");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Housekeeping, PermissionAction.Edit)]
    public async Task<IActionResult> Complete(int id, string? notes)
    {
        try
        {
            await _housekeepingService.CompleteCleaningAsync(id, User.Identity?.Name ?? "Unknown", notes);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Housekeeping, PermissionAction.Approve)]
    public async Task<IActionResult> Inspect(int id, bool passed, string? notes)
    {
        try
        {
            // InspectedByUserId is a real FK to AspNetUsers - it needs the user's Id, not
            // their display name/email (User.Identity.Name), or the FK constraint fails.
            var inspectorId = _userManager.GetUserId(User) ?? throw new InvalidOperationException("Could not identify the current user.");
            await _housekeepingService.InspectAsync(id, inspectorId, passed, notes);
            TempData["Success"] = passed ? "Room passed inspection and is now Available." : "Inspection failed - sent back for cleaning.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
