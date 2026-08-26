using System.ComponentModel.DataAnnotations;

namespace HotelResortMS.Web.Models.Lookups;

/// <summary>The simple "just a name (+ optional description)" master-data types, all
/// served by one generic controller/view pair to avoid four near-identical CRUD screens
/// (Section 6: Bed Types, Floors/Areas, Room Features, Amenity Categories).</summary>
public enum LookupType
{
    BedType,
    FloorArea,
    RoomFeature,
    AmenityCategory
}

public class LookupItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class LookupEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public LookupType Type { get; set; }
}
