using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Operations;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 30: maintenance work orders. All status transitions go through
/// IMaintenanceService, which also drives Room.Status (Section 13).</summary>
[RequirePermission(SystemModules.Maintenance, PermissionAction.View)]
public class MaintenanceRequestsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IMaintenanceService _maintenanceService;
    private readonly UserManager<Core.Entities.Identity.ApplicationUser> _userManager;

    public MaintenanceRequestsController(ApplicationDbContext db, IMaintenanceService maintenanceService, UserManager<Core.Entities.Identity.ApplicationUser> userManager)
    {
        _db = db;
        _maintenanceService = maintenanceService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(MaintenanceRequestStatus? status)
    {
        var query = _db.MaintenanceRequests
            .Include(m => m.MaintenanceCategory)
            .Include(m => m.Room)
            .Include(m => m.Equipment)
            .Include(m => m.AssignedToUser)
            .AsQueryable();
        if (status is not null) query = query.Where(m => m.Status == status);

        ViewBag.Status = status;
        ViewBag.Staff = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();

        var requests = await query.OrderByDescending(m => m.ReportedAt).ToListAsync();
        return View(requests);
    }

    [RequirePermission(SystemModules.Maintenance, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var model = new MaintenanceRequestCreateViewModel();
        await PopulateAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Add)]
    public async Task<IActionResult> Create(MaintenanceRequestCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);
            return View(model);
        }

        var request = await _maintenanceService.CreateRequestAsync(
            model.MaintenanceCategoryId, model.RoomId, model.EquipmentId, model.Description, User.Identity?.Name ?? "Unknown");

        TempData["Success"] = $"Maintenance request {request.RequestNumber} created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Edit)]
    public async Task<IActionResult> Assign(int id, string assignedToUserId)
    {
        await _maintenanceService.AssignAsync(id, assignedToUserId, User.Identity?.Name ?? "Unknown");
        TempData["Success"] = "Request assigned.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Edit)]
    public async Task<IActionResult> Start(int id)
    {
        try { await _maintenanceService.StartAsync(id, User.Identity?.Name ?? "Unknown"); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Edit)]
    public async Task<IActionResult> Complete(int id, decimal? cost, string? notes)
    {
        try
        {
            await _maintenanceService.CompleteAsync(id, User.Identity?.Name ?? "Unknown", cost, notes);
            TempData["Success"] = "Request completed. Room (if any) sent to Housekeeping.";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Maintenance, PermissionAction.Delete)]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        try { await _maintenanceService.CancelAsync(id, reason, User.Identity?.Name ?? "Unknown"); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(MaintenanceRequestCreateViewModel model)
    {
        model.Categories = await _db.MaintenanceCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        model.Rooms = await _db.Rooms.Where(r => r.IsActive).OrderBy(r => r.RoomNumber).ToListAsync();
        model.EquipmentList = await _db.Equipment.Where(e => e.IsActive).OrderBy(e => e.Name).ToListAsync();
    }
}
