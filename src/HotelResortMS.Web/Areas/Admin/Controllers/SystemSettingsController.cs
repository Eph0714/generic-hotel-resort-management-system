using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Infrastructure.Services;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HotelResortMS.Web.Areas.Admin.Controllers;

/// <summary>Section 49: centralized, editable settings so nothing hotel-specific is
/// hard-coded (hotel name, logo, banner, application theme, currency, tax rate,
/// discount percentages, etc.).
/// Restricted to Super Admin ONLY - unlike every other module in the app, this is not
/// gated through the Roles &amp; Permissions matrix (<see cref="RequirePermissionAttribute"/>),
/// specifically so no role grant can ever expose these hotel-wide/security-adjacent
/// settings to a non-Super-Admin account. <see cref="AuthorizeAttribute.Roles"/> checks
/// the role claim baked into the sign-in cookie by ASP.NET Core Identity's own
/// AddIdentity/SignInManager pipeline - the same "Super Admin" role name
/// <see cref="PermissionService"/> already treats as all-access everywhere else.</summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SuperAdmin)]
public class SystemSettingsController : Controller
{
    private static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
    private const long MaxLogoBytes = 2 * 1024 * 1024;
    private const long MaxBannerBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;

    public SystemSettingsController(ApplicationDbContext db, IAuditService auditService, IMemoryCache cache, IWebHostEnvironment env)
    {
        _db = db;
        _auditService = auditService;
        _cache = cache;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _db.SystemSettings.OrderBy(s => s.Category).ThenBy(s => s.Key).ToListAsync();
        ViewBag.SupportedThemes = SupportedThemes.All;
        ViewBag.SupportedBannerModes = SupportedBannerModes.All;
        ViewBag.SupportedBannerAlignments = SupportedBannerAlignments.All;
        return View(settings.GroupBy(s => s.Category).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string? value)
    {
        var setting = await _db.SystemSettings.FindAsync(id);
        if (setting is null) return NotFound();

        if (setting.Key == "App.Theme" && !SupportedThemes.IsValid(value))
        {
            TempData["Error"] = $"'{value}' is not a supported theme.";
            return RedirectToAction(nameof(Index));
        }

        if (setting.Key == "Hotel.BannerMode" && !SupportedBannerModes.IsValid(value))
        {
            TempData["Error"] = $"'{value}' is not a supported banner display option.";
            return RedirectToAction(nameof(Index));
        }

        if (setting.Key == "Hotel.BannerAlignment" && !SupportedBannerAlignments.IsValid(value))
        {
            TempData["Error"] = $"'{value}' is not a supported banner alignment.";
            return RedirectToAction(nameof(Index));
        }

        if (setting.Key == "App.ThemeColor" && !SupportedThemeColors.IsValidHex(value))
        {
            TempData["Error"] = $"'{value}' is not a valid color.";
            return RedirectToAction(nameof(Index));
        }

        var oldValue = setting.Value;
        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.SystemSettings, "Update", setting.Id.ToString(),
            oldValues: new { setting.Key, Value = oldValue }, newValues: new { setting.Key, Value = value });

        InvalidateBrandingCacheIfNeeded(setting.Key);

        TempData["Success"] = $"{setting.Key} updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(IFormFile? file)
    {
        var error = await SaveBrandingImageAsync(file, "logo", "Hotel.LogoPath", MaxLogoBytes);
        TempData[error is null ? "Success" : "Error"] = error ?? "Logo updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBanner(IFormFile? file)
    {
        var error = await SaveBrandingImageAsync(file, "banner", "Hotel.BannerPath", MaxBannerBytes);
        TempData[error is null ? "Success" : "Error"] = error ?? "Banner updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLogo() => await RemoveBrandingImageAsync("Hotel.LogoPath");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBanner() => await RemoveBrandingImageAsync("Hotel.BannerPath");

    /// <summary>
    /// Saves an uploaded logo/banner to wwwroot/uploads/branding, replacing any file
    /// previously uploaded under a different extension, and upserts the given setting
    /// key with the resulting relative URL. Returns an error message, or null on success.
    /// </summary>
    private async Task<string?> SaveBrandingImageAsync(IFormFile? file, string baseName, string settingKey, long maxBytes)
    {
        if (file is null || file.Length == 0)
        {
            return "Please choose an image file first.";
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            return $"Unsupported file type '{ext}'. Allowed: {string.Join(", ", AllowedImageExtensions)}.";
        }

        if (file.Length > maxBytes)
        {
            return $"File is too large (max {maxBytes / (1024 * 1024)} MB).";
        }

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "branding");
        Directory.CreateDirectory(uploadsDir);

        // Remove any previously uploaded file for this slot under a different extension
        // so switching from a .png logo to a .svg one doesn't leave the old file behind.
        foreach (var oldExt in AllowedImageExtensions)
        {
            var oldPath = Path.Combine(uploadsDir, baseName + oldExt);
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }
        }

        var fileName = baseName + ext;
        var fullPath = Path.Combine(uploadsDir, fileName);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl = $"/uploads/branding/{fileName}";
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
        if (setting is null)
        {
            setting = new SystemSetting { Key = settingKey, Category = "Branding & Theme" };
            _db.SystemSettings.Add(setting);
        }

        var oldValue = setting.Value;
        setting.Value = relativeUrl;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.SystemSettings, "Upload", setting.Id.ToString(),
            oldValues: new { Key = settingKey, Value = oldValue }, newValues: new { Key = settingKey, Value = relativeUrl });

        InvalidateBrandingCacheIfNeeded(settingKey);
        return null;
    }

    private async Task<IActionResult> RemoveBrandingImageAsync(string settingKey)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
        if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
        {
            var fullPath = Path.Combine(_env.WebRootPath, setting.Value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }

            var oldValue = setting.Value;
            setting.Value = null;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = User.Identity?.Name;
            await _db.SaveChangesAsync();

            await _auditService.LogAsync(SystemModules.SystemSettings, "Remove", setting.Id.ToString(),
                oldValues: new { Key = settingKey, Value = oldValue }, newValues: new { Key = settingKey, Value = (string?)null });

            InvalidateBrandingCacheIfNeeded(settingKey);
        }

        TempData["Success"] = "Removed.";
        return RedirectToAction(nameof(Index));
    }

    private void InvalidateBrandingCacheIfNeeded(string settingKey)
    {
        // Invalidate immediately rather than waiting out the 5-minute header cache - an
        // admin changing branding/theme should see it change on their very next page load.
        if (settingKey is "Hotel.Name" or "Hotel.LogoPath" or "Hotel.BannerPath"
            or "Hotel.BannerMode" or "Hotel.BannerColor" or "Hotel.BannerText"
            or "Hotel.BannerAlignment" or "App.Theme" or "App.ThemeColor")
        {
            _cache.Remove(HotelBrandingService.CacheKey);
        }
    }
}
