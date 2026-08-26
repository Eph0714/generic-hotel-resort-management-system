namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 49 (System Settings CRUD): a single configurable key/value store so nothing
/// hotel-specific (name, currency, tax rate, numbering prefixes, etc.) is hard-coded.
/// Read through ISystemSettingService's typed helpers rather than raw string lookups
/// wherever a specific setting is used repeatedly.
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }

    /// <summary>Stable machine key, e.g. "Hotel.Name", "Finance.VatRate".</summary>
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    /// <summary>Human label/grouping shown on the Settings screen.</summary>
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
