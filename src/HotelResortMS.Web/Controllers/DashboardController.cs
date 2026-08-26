using HotelResortMS.Core.Common;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Web.Models.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelResortMS.Web.Controllers;

/// <summary>
/// Section 11: the operational at-a-glance view. All statistics come from
/// IDashboardService (which reads the same tables/services every other module already
/// writes through) - nothing here is hard-coded or duplicated business logic. Section
/// visibility is gated by the user's actual RolePermission grants (Section 19), not a
/// hard-coded role name, via IPermissionService - the same check every other controller
/// in the system uses.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IPermissionService _permissionService;

    public DashboardController(IDashboardService dashboardService, IPermissionService permissionService)
    {
        _dashboardService = dashboardService;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index(TrendPeriod period = TrendPeriod.Day)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var model = new DashboardViewModel
        {
            Snapshot = await _dashboardService.GetSnapshotAsync(period)
        };

        if (userId is not null)
        {
            model.CanSeeFinancial = await _permissionService.HasPermissionAsync(userId, SystemModules.Income, PermissionAction.View);
            model.CanSeeRooms = await _permissionService.HasPermissionAsync(userId, SystemModules.Rooms, PermissionAction.View);
            model.CanSeeHousekeeping = await _permissionService.HasPermissionAsync(userId, SystemModules.Housekeeping, PermissionAction.View);
            model.CanSeeMaintenance = await _permissionService.HasPermissionAsync(userId, SystemModules.Maintenance, PermissionAction.View);
            model.CanSeeReservations = await _permissionService.HasPermissionAsync(userId, SystemModules.Reservations, PermissionAction.View);
            model.CanSeeAmenities = await _permissionService.HasPermissionAsync(userId, SystemModules.Amenities, PermissionAction.View);
            model.CanSeePOS = await _permissionService.HasPermissionAsync(userId, SystemModules.POS, PermissionAction.View);
            model.CanSeeInventory = await _permissionService.HasPermissionAsync(userId, SystemModules.Inventory, PermissionAction.View);
            model.CanSeeFrontDesk = await _permissionService.HasPermissionAsync(userId, SystemModules.FrontDesk, PermissionAction.View);
            model.CanSeePayments = await _permissionService.HasPermissionAsync(userId, SystemModules.Payments, PermissionAction.View);
            model.CanSeeGuests = await _permissionService.HasPermissionAsync(userId, SystemModules.Guests, PermissionAction.View);
            model.CanSeeAuditTrail = await _permissionService.HasPermissionAsync(userId, SystemModules.AuditTrail, PermissionAction.View);
        }

        return View(model);
    }
}
