namespace HotelResortMS.Core.Entities;

/// <summary>Section 6/35: a vendor purchase orders are raised against.</summary>
public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
}
