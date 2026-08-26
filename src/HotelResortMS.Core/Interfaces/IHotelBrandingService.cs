using System;
using System.Collections.Generic;
using System.Linq;

namespace HotelResortMS.Core.Interfaces;

/// <summary>The fixed, known set of application themes a user can choose in
/// Admin > System Settings > Branding &amp; Theme (App.Theme) - a plain string constant
/// list, not an open-ended free-text value, so an unrecognized/corrupted setting value
/// always has a safe, defined fallback ("Light").</summary>
public static class SupportedThemes
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    public static readonly string[] All = { Light, Dark };

    public static bool IsValid(string? theme) => theme is not null && All.Contains(theme);
}

/// <summary>Whether the Dashboard banner shows an uploaded image or a plain solid-color
/// fill - a fixed choice, not open-ended free text, same reasoning as
/// <see cref="SupportedThemes"/>.</summary>
public static class SupportedBannerModes
{
    public const string Image = "Image";
    public const string Color = "Color";

    public static readonly string[] All = { Image, Color };

    public static bool IsValid(string? mode) => mode is not null && All.Contains(mode);
}

/// <summary>Where the logo+caption group sits within the Dashboard banner box - a fixed
/// choice, not open-ended free text, same reasoning as <see cref="SupportedThemes"/>.</summary>
public static class SupportedBannerAlignments
{
    public const string Left = "Left";
    public const string Middle = "Middle";
    public const string Right = "Right";

    public static readonly string[] All = { Left, Middle, Right };

    public static bool IsValid(string? alignment) => alignment is not null && All.Contains(alignment);
}

/// <summary>The accent color used for the whole application (every button, the sidebar's
/// nav-link chips, and its active-link accent) - a Super Admin picks any hex color via a
/// native color-picker input, same UX as <see cref="HotelBrandingInfo.BannerColor"/>, no
/// longer a fixed named-palette dropdown. "Validity" here just means "looks like a hex
/// color" (same loose rule <see cref="HotelBrandingInfo.BannerColor"/> already used), not
/// membership in a fixed list.
/// <see cref="LegacyPaletteHex"/> keeps the six original named palettes' hex values only
/// so a database still holding an old palette NAME (e.g. "Emerald", from before this
/// became a free color picker) keeps rendering as that same color rather than silently
/// resetting to the default - a one-time, read-side migration convenience, not a
/// currently-offered choice.</summary>
public static class SupportedThemeColors
{
    public const string DefaultHex = "#16305c";

    private static readonly System.Text.RegularExpressions.Regex HexPattern =
        new(@"^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>The six palettes originally offered before this became a free color
    /// picker - kept only to translate an old stored name to its hex value on read.</summary>
    public static readonly IReadOnlyDictionary<string, string> LegacyPaletteHex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Navy"] = "#16305c",
        ["Emerald"] = "#0f5132",
        ["Burgundy"] = "#5c1a2b",
        ["Purple"] = "#4c1d95",
        ["Charcoal"] = "#374151",
        ["Ocean"] = "#0e7490"
    };

    public static bool IsValidHex(string? color) => color is not null && HexPattern.IsMatch(color);

    /// <summary>Resolves a stored value to a real hex color for rendering: already-valid
    /// hex passes through, a legacy palette name maps to its hex, anything else (empty,
    /// corrupted, never set) falls back to <see cref="DefaultHex"/>.</summary>
    public static string ResolveHex(string? stored)
    {
        if (IsValidHex(stored)) return stored!;
        if (stored is not null && LegacyPaletteHex.TryGetValue(stored, out var legacyHex)) return legacyHex;
        return DefaultHex;
    }
}

/// <summary>Section 49: everything shown in the header/dashboard chrome that's specific
/// to this hotel - name, logo, banner, and the chosen application theme - comes from
/// System Settings, never hard-coded.</summary>
public class HotelBrandingInfo
{
    public string Name { get; set; } = "Hotel and Resort Management System";

    /// <summary>Relative URL (e.g. "/uploads/branding/logo.png"), or null if no logo has
    /// been uploaded - callers must fall back to a plain placeholder, never a fake image.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>One of <see cref="SupportedBannerModes"/>; "Image" if unset.</summary>
    public string BannerMode { get; set; } = SupportedBannerModes.Image;

    /// <summary>Relative URL, or null if no banner image has been uploaded - only
    /// meaningful when <see cref="BannerMode"/> is Image.</summary>
    public string? BannerUrl { get; set; }

    /// <summary>Hex color (e.g. "#2f6fed") used when <see cref="BannerMode"/> is Color.</summary>
    public string BannerColor { get; set; } = "#2f6fed";

    /// <summary>Optional caption text overlaid on the banner (image or color) - null/empty
    /// means no text, never a placeholder caption.</summary>
    public string? BannerText { get; set; }

    /// <summary>One of <see cref="SupportedBannerAlignments"/>; "Middle" if unset - where
    /// the logo+caption group sits within the banner box.</summary>
    public string BannerAlignment { get; set; } = SupportedBannerAlignments.Middle;

    /// <summary>One of <see cref="SupportedThemes"/>; "Light" if unset or an unrecognized
    /// value was ever stored directly in the database.</summary>
    public string Theme { get; set; } = SupportedThemes.Light;

    /// <summary>Hex color (e.g. "#16305c"), same free-choice pattern as
    /// <see cref="BannerColor"/> - the accent color used for every button and the
    /// sidebar's active-link highlight, app-wide. Always a real hex value by the time it
    /// reaches here (see <see cref="SupportedThemeColors.ResolveHex"/>), never a legacy
    /// palette name or an unvalidated raw value.</summary>
    public string ThemeColor { get; set; } = SupportedThemeColors.DefaultHex;
}

/// <summary>
/// Section 49: the hotel/resort name, logo, banner and application theme shown across
/// every page come from System Settings, never hard-coded. Cached briefly since the
/// header reads this on every request but the values change only when an administrator
/// edits Settings.
/// </summary>
public interface IHotelBrandingService
{
    Task<HotelBrandingInfo> GetBrandingAsync();
}
