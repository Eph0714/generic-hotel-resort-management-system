using HotelResortMS.Core.Common;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 50: manual backup/restore, restricted to Super Admin (the
/// RequirePermission checks below - Restore additionally requires Configure, the highest
/// bar this system has, since a bad restore overwrites the entire live database).</summary>
[RequirePermission(SystemModules.BackupRestore, PermissionAction.View)]
public class BackupController : Controller
{
    private readonly IBackupService _backupService;

    public BackupController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.LastSuccessful = await _backupService.GetLastSuccessfulAsync();
        var history = await _backupService.GetHistoryAsync();
        return View(history);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.BackupRestore, PermissionAction.Add)]
    public async Task<IActionResult> Create()
    {
        var record = await _backupService.CreateBackupAsync(User.Identity?.Name ?? "Unknown");
        TempData[record.Status == Core.Entities.BackupStatus.Success ? "Success" : "Error"] =
            record.Status == Core.Entities.BackupStatus.Success
                ? $"Backup {record.FileName} completed ({record.SizeBytes / 1024} KB)."
                : $"Backup failed: {record.ErrorMessage}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.BackupRestore, PermissionAction.Configure)]
    public async Task<IActionResult> Restore(int id, string confirmationText)
    {
        if (confirmationText != "RESTORE")
        {
            TempData["Error"] = "Restore was not confirmed - type RESTORE exactly to proceed.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _backupService.RestoreAsync(id, User.Identity?.Name ?? "Unknown");
            TempData["Success"] = "Database restored successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = $"Restore failed: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
