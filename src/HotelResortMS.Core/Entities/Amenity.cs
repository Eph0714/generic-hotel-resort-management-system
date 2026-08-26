namespace HotelResortMS.Core.Entities;

public class AmenityCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>Section 14: bookable facilities (pool, function hall, cottage, etc.). Status
/// mirrors Room's lifecycle and is transitioned only through AmenityService.</summary>
public class Amenity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public int AmenityCategoryId { get; set; }
    public AmenityCategory? AmenityCategory { get; set; }

    public string? Description { get; set; }
    public int Capacity { get; set; }
    public string? ImagePath { get; set; }

    public decimal HourlyRate { get; set; }
    public decimal DailyRate { get; set; }
    public decimal RegularRate { get; set; }
    public decimal WeekendRate { get; set; }
    public decimal HolidayRate { get; set; }
    public decimal SeasonalRate { get; set; }

    public int MinimumHours { get; set; }
    public decimal AdditionalChargePerHour { get; set; }

    public AmenityStatus Status { get; set; } = AmenityStatus.Available;
}

public enum AmenityStatus
{
    Available,
    Reserved,
    InUse,
    Cleaning,
    Maintenance,
    OutOfService
}
