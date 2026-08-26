namespace HotelResortMS.Core.Entities;

/// <summary>Section 6/13: master data describing a category of room (e.g. Standard,
/// Deluxe, Suite). Individual Rooms reference this for their default rates/capacity.</summary>
public class RoomType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BaseCapacity { get; set; }

    // Current default rates for this room type (Section 13/15). Actual reservations snapshot
    // the rate in effect at booking time onto ReservationRoom.RateAmount - editing these
    // fields never rewrites historical transactions.
    public decimal RegularRate { get; set; }
    public decimal WeekendRate { get; set; }
    public decimal HolidayRate { get; set; }
    public decimal SeasonalRate { get; set; }
    public decimal ExtraPersonRate { get; set; }
}

public class BedType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>Section 6: Floors/Areas master data.</summary>
public class FloorArea : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class RoomFeature : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
