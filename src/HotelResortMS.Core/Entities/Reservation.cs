namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 20: a booking for one or more rooms/amenities. RateAmount on each
/// ReservationRoom/ReservationAmenity is a snapshot of the rate in effect at booking time
/// (Section 15 - historical transactions must retain the rate actually used, even if
/// RoomType/Amenity rates change later).
/// </summary>
public class Reservation : BaseEntity
{
    public string ReservationNumber { get; set; } = string.Empty;

    public int GuestId { get; set; }
    public Guest? Guest { get; set; }

    public DateTime ReservationDate { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    /// <summary>Section 32: an optional bundled Package added on top of the room charges.
    /// PackagePrice snapshots the package's price at booking time (Section 15 - a later
    /// price change on the Package master record must never alter this reservation's
    /// already-agreed total) and is folded into TotalAmount by ReservationService.</summary>
    public int? PackageId { get; set; }
    public Package? Package { get; set; }
    public decimal PackagePrice { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DepositRequired { get; set; }
    public decimal AmountPaid { get; set; }

    /// <summary>TotalAmount - DiscountAmount - AmountPaid; kept as a stored/derived field so
    /// it is easy to query for Accounts Receivable without recomputing from line items every time.</summary>
    public decimal BalanceDue { get; set; }

    public string? SpecialRequests { get; set; }
    public string? Notes { get; set; }

    /// <summary>Section 23: which policy governs a cancellation of this reservation - null
    /// means no fee/forfeiture is ever computed (ReservationService treats that as free
    /// cancellation), so existing reservations from before Phase 6 keep working unchanged.</summary>
    public int? CancellationPolicyId { get; set; }
    public CancellationPolicy? CancellationPolicy { get; set; }

    /// <summary>Populated only when Status becomes Cancelled/NoShow, per the configurable
    /// Cancellation Policy applied (Section 23).</summary>
    public string? CancellationReason { get; set; }
    public decimal CancellationFeeAmount { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }

    public ICollection<ReservationRoom> Rooms { get; set; } = new List<ReservationRoom>();
    public ICollection<ReservationAmenity> Amenities { get; set; } = new List<ReservationAmenity>();
}

public enum ReservationStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    CheckedOut,
    Cancelled,
    NoShow
}

/// <summary>One row per room on a (possibly multi-room) reservation.</summary>
public class ReservationRoom
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public int RoomId { get; set; }
    public Room? Room { get; set; }

    /// <summary>Rate snapshot at booking time (Section 15) - never recalculated from the
    /// room/room type's current rate after the fact.</summary>
    public decimal RateAmount { get; set; }
    public string RateType { get; set; } = "Regular"; // Regular/Weekend/Holiday/Seasonal
}

/// <summary>One row per amenity booked alongside a reservation.</summary>
public class ReservationAmenity
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public int AmenityId { get; set; }
    public Amenity? Amenity { get; set; }

    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }

    public decimal RateAmount { get; set; }
}
