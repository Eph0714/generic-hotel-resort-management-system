namespace HotelResortMS.Core.Entities;

public class EventType : BaseEntity
{
    public string Name { get; set; } = string.Empty; // Wedding, Birthday, Seminar, Meeting, Conference, Party, Corporate
    public string? Description { get; set; }
}

/// <summary>Section 31: a bookable space for functions - distinct from Amenity (a
/// Function Hall amenity can double as an event venue, but not every venue needs to be a
/// separately-reservable amenity, e.g. an outdoor garden used only for events).</summary>
public class EventVenue : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
}

public enum EventStatus
{
    Pending,
    Confirmed,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>
/// Section 31: weddings, seminars, conferences, etc. Deliberately mirrors Reservation's
/// shape (deposit/balance, its own document number, venue double-booking prevention) so
/// the same mental model applies - an Event is a booking, just against a Venue instead of
/// a Room. Integrates with Payments/Income the same way Reservations and POS do.
/// </summary>
public class Event : BaseEntity
{
    public string EventNumber { get; set; } = string.Empty;

    public int EventTypeId { get; set; }
    public EventType? EventType { get; set; }

    public int EventVenueId { get; set; }
    public EventVenue? EventVenue { get; set; }

    /// <summary>Nullable - a corporate client not in the Guests table can still be typed
    /// in via ClientName/ClientContact.</summary>
    public int? GuestId { get; set; }
    public Guest? Guest { get; set; }
    public string? ClientName { get; set; }
    public string? ClientContact { get; set; }

    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public int ExpectedGuests { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Pending;

    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
}
