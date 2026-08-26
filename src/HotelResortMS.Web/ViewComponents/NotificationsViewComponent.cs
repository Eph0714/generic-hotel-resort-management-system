using HotelResortMS.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HotelResortMS.Web.ViewComponents;

/// <summary>Backs the header notification bell (Section 3: header "Notifications").
/// Shares IDashboardService.GetAlertsAsync with the Dashboard page's own Alerts panel so
/// the two never show different counts for the same underlying condition.</summary>
public class NotificationsViewComponent : ViewComponent
{
    private readonly IDashboardService _dashboardService;

    public NotificationsViewComponent(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var alerts = await _dashboardService.GetAlertsAsync();
        return View(alerts);
    }
}
