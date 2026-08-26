using HotelResortMS.Core.Common;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 12/41: shows the hotel's current operating date and status, and lets
/// an authorized user open the next business day once the current one is Closed.</summary>
[RequirePermission(SystemModules.BusinessDate, PermissionAction.View)]
public class BusinessDateController : Controller
{
    private readonly IBusinessDateService _businessDateService;

    public BusinessDateController(IBusinessDateService businessDateService)
    {
        _businessDateService = businessDateService;
    }

    public async Task<IActionResult> Index()
    {
        var current = await _businessDateService.GetCurrentAsync();
        return View(current);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.BusinessDate, PermissionAction.Approve)]
    public async Task<IActionResult> OpenNextDay()
    {
        try
        {
            var next = await _businessDateService.OpenNextDayAsync(User.Identity?.Name ?? "Unknown");
            TempData["Success"] = $"Business date {next.Date:yyyy-MM-dd} opened.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
