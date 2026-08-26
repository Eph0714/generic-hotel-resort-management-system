using HotelResortMS.Core.Common;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 43: previews and posts the Daily Closing summary, locking the
/// business date and carrying ending cash forward to the next day's opening.</summary>
[RequirePermission(SystemModules.DailyClosing, PermissionAction.View)]
public class DailyClosingController : Controller
{
    private readonly IDailyClosingService _dailyClosingService;

    public DailyClosingController(IDailyClosingService dailyClosingService)
    {
        _dailyClosingService = dailyClosingService;
    }

    public async Task<IActionResult> Index()
    {
        var preview = await _dailyClosingService.PreviewAsync();
        return View(preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.DailyClosing, PermissionAction.Approve)]
    public async Task<IActionResult> Close(decimal actualCashCount, string? remarks)
    {
        try
        {
            var record = await _dailyClosingService.CloseAsync(actualCashCount, User.Identity?.Name ?? "Unknown", remarks);
            TempData["Success"] = $"Business date closed. Ending cash: {record.ActualCashCount:N2} (variance {record.CashVariance:N2}).";
            return RedirectToAction("Index", "BusinessDate");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.DailyClosing, PermissionAction.Approve)]
    public async Task<IActionResult> Reopen(string reason)
    {
        try
        {
            await _dailyClosingService.ReopenAsync(User.Identity?.Name ?? "Unknown", reason);
            TempData["Success"] = "Business date reopened.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Index", "BusinessDate");
    }
}
