namespace HotelResortMS.Core.Entities;

/// <summary>Section 13: an individual physical room. Status drives (and is driven by) the
/// Reservation/Check-In/Check-Out/Housekeeping/Maintenance workflows - never edited
/// directly by an unrelated screen; RoomService is the single place that transitions it.</summary>
public class Room : BaseEntity
{
    public string RoomNumber { get; set; } = string.Empty;
    public string? RoomName { get; set; }

    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }

    public int? BedTypeId { get; set; }
    public BedType? BedType { get; set; }

    public int? FloorAreaId { get; set; }
    public FloorArea? FloorArea { get; set; }

    public int Capacity { get; set; }
    public string? Description { get; set; }
    public string? ImagePath { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Available;

    // Room-specific rate overrides; null falls back to the RoomType's default rates.
    public decimal? RegularRateOverride { get; set; }
    public decimal? WeekendRateOverride { get; set; }
    public decimal? HolidayRateOverride { get; set; }
    public decimal? SeasonalRateOverride { get; set; }
    public decimal? ExtraPersonRateOverride { get; set; }
}

/// <summary>Section 13 status list. Occupied/Reserved come from Reservation/Check-In/
/// Check-Out; Dirty/Cleaning/Clean/Inspected/Ready from Housekeeping; Maintenance/OutOfService
/// from Maintenance - see RoomService for the transition rules.</summary>
public enum RoomStatus
{
    Available,
    Reserved,
    Occupied,
    Dirty,
    Cleaning,
    Clean,
    Inspected,
    Ready,
    Maintenance,
    OutOfService
}
