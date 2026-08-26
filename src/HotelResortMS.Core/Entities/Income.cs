namespace HotelResortMS.Core.Entities;

/// <summary>Section 38: where revenue comes from. Kept as a fixed enum (rather than
/// free text) so financial reports can group reliably.</summary>
public enum IncomeCategory
{
    RoomRevenue,
    AmenityRevenue,
    POS,
    FoodAndBeverage,
    Services,
    Events,
    Packages,
    Rentals,
    Other
}

public enum IncomeStatus
{
    Posted,
    Voided
}

/// <summary>
/// Section 38 (Income Management): revenue recognized when a charge is posted (room
/// charge at check-in, POS sale at time of sale), not when cash is collected - Section 38's
/// "Revenue is not always equal to Cash Received" is the whole reason Income and Payment
/// are two separate ledgers rather than one.
/// </summary>
public class Income
{
    public int Id { get; set; }

    public string IncomeNumber { get; set; } = string.Empty;

    public IncomeCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;

    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }

    /// <summary>"Reservation", "POS", etc., plus the record's own number/id - traces this
    /// income row back to the transaction that generated it.</summary>
    public string ReferenceType { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }

    public IncomeStatus Status { get; set; } = IncomeStatus.Posted;

    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string RecordedBy { get; set; } = string.Empty;

    public DateTime? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}
