using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IHotelBrandingService"/>
public class HotelBrandingService : IHotelBrandingService
{
    /// <summary>Shared with SystemSettingsController, which removes this exact key
    /// whenever any of the four settings below is changed (text update or file upload).</summary>
    public const string CacheKey = "HotelBranding.Info";

    private static readonly string[] Keys =
    {
        "Hotel.Name", "Hotel.LogoPath", "Hotel.BannerPath", "Hotel.BannerMode",
        "Hotel.BannerColor", "Hotel.BannerText", "Hotel.BannerAlignment", "App.Theme",
        "App.ThemeColor"
    };

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public HotelBrandingService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<HotelBrandingInfo> GetBrandingAsync()
    {
        if (_cache.TryGetValue(CacheKey, out HotelBrandingInfo? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await _db.SystemSettings
            .Where(s => Keys.Contains(s.Key))
            .Select(s => new { s.Key, s.Value })
            .ToListAsync();

        var info = new HotelBrandingInfo
        {
            Name = rows.FirstOrDefault(r => r.Key == "Hotel.Name")?.Value is string n && !string.IsNullOrWhiteSpace(n)
                ? n : "Hotel and Resort Management System",
            LogoUrl = NullIfEmpty(rows.FirstOrDefault(r => r.Key == "Hotel.LogoPath")?.Value),
            BannerUrl = NullIfEmpty(rows.FirstOrDefault(r => r.Key == "Hotel.BannerPath")?.Value),
            BannerMode = SupportedBannerModes.IsValid(rows.FirstOrDefault(r => r.Key == "Hotel.BannerMode")?.Value)
                ? rows.First(r => r.Key == "Hotel.BannerMode").Value!
                : SupportedBannerModes.Image,
            BannerColor = NullIfEmpty(rows.FirstOrDefault(r => r.Key == "Hotel.BannerColor")?.Value) ?? "#2f6fed",
            BannerText = NullIfEmpty(rows.FirstOrDefault(r => r.Key == "Hotel.BannerText")?.Value),
            BannerAlignment = SupportedBannerAlignments.IsValid(rows.FirstOrDefault(r => r.Key == "Hotel.BannerAlignment")?.Value)
                ? rows.First(r => r.Key == "Hotel.BannerAlignment").Value!
                : SupportedBannerAlignments.Middle,
            Theme = SupportedThemes.IsValid(rows.FirstOrDefault(r => r.Key == "App.Theme")?.Value)
                ? rows.First(r => r.Key == "App.Theme").Value!
                : SupportedThemes.Light,
            // ResolveHex covers three cases: an already-valid hex passes through, an old
            // stored palette NAME (e.g. "Emerald", from before Theme Color became a free
            // color picker) maps to that palette's original hex, anything else falls
            // back to the default - never a raw unvalidated value.
            ThemeColor = SupportedThemeColors.ResolveHex(rows.FirstOrDefault(r => r.Key == "App.ThemeColor")?.Value)
        };

        // Short-lived cache: the header/dashboard read this on every page load, but it
        // only ever changes when an administrator edits Settings or uploads a file.
        _cache.Set(CacheKey, info, TimeSpan.FromMinutes(5));
        return info;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
