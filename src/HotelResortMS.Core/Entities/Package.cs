namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 32: a bundled offer (e.g. "Honeymoon Package" = room + spa amenity + a
/// welcome basket product). Kept as master data with free-text components for now -
/// selecting a package during Reservation creation to auto-populate its rooms/amenities/
/// products is a natural Phase 7+ enhancement once reporting surfaces which packages
/// actually sell, but is out of scope for this phase.
/// </summary>
public class Package : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Capacity { get; set; }

    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }

    public ICollection<PackageComponent> Components { get; set; } = new List<PackageComponent>();
}

public class PackageComponent
{
    public int Id { get; set; }

    public int PackageId { get; set; }
    public Package? Package { get; set; }

    /// <summary>Free-text description of what's included (e.g. "Deluxe Room, 2 nights",
    /// "Couple's Spa Session") - see Package's remarks on why this isn't a strict FK yet.</summary>
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}
