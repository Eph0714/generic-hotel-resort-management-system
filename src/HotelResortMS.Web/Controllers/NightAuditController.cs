using HotelResortMS.Core.Common;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 42: shows unresolved exceptions for the current business date and
/// lets an authorized user run (optionally override) Night Audit before Daily Closing.</summary>
[RequirePermission(SystemModules.NightAudit, PermissionAction.View)]
public class NightAuditController : Controller
{
    private readonly INightAuditService _nightAuditService;
    private readonly IBusinessDateService _businessDateService;

    public NightAuditController(INightAuditService nightAuditService, IBusinessDateService businessDateService)
    {
        _nightAuditService = nightAuditService;
        _businessDateService = businessDateService;
    }

    public async Task<IActionResult> Index()
    {
        var businessDate = await _businessDateService.GetCurrentAsync();
        ViewBag.BusinessDate = businessDate;
        var exceptions = await _nightAuditService.FindExceptionsAsync();
        return View(exceptions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.NightAudit, PermissionAction.Approve)]
    public async Task<IActionResult> Run(string? overrideReason)
    {
        try
        {
            await _nightAuditService.RunAsync(User.Identity?.Name ?? "Unknown", overrideReason);
            TempData["Success"] = "Night Audit completed.";
            return RedirectToAction("Index", "BusinessDate");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
