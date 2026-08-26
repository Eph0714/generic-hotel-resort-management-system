namespace HotelResortMS.Core.Entities;

/// <summary>Section 17: the configurable discount types the law/business supports.
/// Percentages are never hard-coded in code - they live on the Discount row below and are
/// editable by an administrator (e.g. when Philippine Senior/PWD regulations change).</summary>
public enum DiscountType
{
    SeniorCitizen,
    PWD,
    Promotional,
    Corporate,
    Membership,
    Other
}

public enum DiscountCalculationType
{
    Percentage,
    FixedAmount
}

/// <summary>
/// Section 17/18: one configurable discount definition. DiscountService is the only code
/// path allowed to apply one of these to a transaction - centralizing the math is what
/// keeps Reservations, POS, and Events from ever computing a discount differently.
/// </summary>
public class Discount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public DiscountCalculationType CalculationType { get; set; }

    /// <summary>Only meaningful when CalculationType is Percentage (e.g. 20 = 20%).</summary>
    public decimal Percentage { get; set; }

    /// <summary>Only meaningful when CalculationType is FixedAmount.</summary>
    public decimal FixedAmount { get; set; }

    public bool EligibleForRooms { get; set; }
    public bool EligibleForAmenities { get; set; }
    public bool EligibleForProducts { get; set; }
    public bool EligibleForServices { get; set; }

    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }

    /// <summary>Section 3/48: Senior Citizen/PWD discounts require a verified ID on file
    /// before DiscountService will allow them to be applied without a manual override.</summary>
    public bool RequiresIdVerification { get; set; }
}

/// <summary>
/// Section 18/56: one row per discount actually applied to a transaction (Reservation,
/// POS sale, Event...) - kept separate from Discount itself so editing/deactivating a
/// discount definition never rewrites history (Section 15).
/// </summary>
public class DiscountTransaction
{
    public int Id { get; set; }

    public int DiscountId { get; set; }
    public Discount? Discount { get; set; }

    /// <summary>"Reservation", "POS", "Event", etc.</summary>
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;

    public decimal EligibleAmount { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>Section 18: manual overrides require authorization + a reason, both
    /// captured here as well as in the audit log so a report can be run on overrides alone
    /// (Section 44 - "Discount Reports: Manual Overrides").</summary>
    public bool IsManualOverride { get; set; }
    public string? OverrideReason { get; set; }
    public string? AuthorizedBy { get; set; }

    public string AppliedBy { get; set; } = string.Empty;
    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
}
