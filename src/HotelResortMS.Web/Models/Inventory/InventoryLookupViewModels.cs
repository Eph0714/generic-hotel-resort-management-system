using System.ComponentModel.DataAnnotations;

namespace HotelResortMS.Web.Models.Inventory;

/// <summary>Section 6/33: the simple "just a name (+ description)" inventory master data -
/// Units of Measure and Inventory Locations - served by one generic controller/view pair.</summary>
public enum InventoryLookupType
{
    UnitOfMeasure,
    InventoryLocation
}

public class InventoryLookupItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class InventoryLookupEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public InventoryLookupType Type { get; set; }
}
