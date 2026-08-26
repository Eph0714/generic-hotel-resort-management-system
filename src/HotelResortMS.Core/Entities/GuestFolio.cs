namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 25: the running bill for a guest's stay. Created at Check-In (Section 24),
/// closed at Check-Out (Section 28). Every charge/discount/tax/payment/refund is a
/// FolioDetail line - the folio itself never stores a single lump total so every
/// adjustment stays traceable (Section 25: "Every adjustment must be traceable").
/// </summary>
public class GuestFolio
{
    public int Id { get; set; }

    public string FolioNumber { get; set; } = string.Empty;

    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public int GuestId { get; set; }
    public Guest? Guest { get; set; }

    public FolioStatus Status { get; set; } = FolioStatus.Open;

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ICollection<FolioDetail> Details { get; set; } = new List<FolioDetail>();
}

public enum FolioStatus
{
    Open,
    Closed
}

public enum FolioDetailType
{
    RoomCharge,
    AmenityCharge,
    PosCharge,
    ServiceCharge,
    PackageCharge,
    Discount,
    Tax,
    Deposit,
    Payment,
    Refund
}

public class FolioDetail
{
    public int Id { get; set; }

    public int GuestFolioId { get; set; }
    public GuestFolio? GuestFolio { get; set; }

    public FolioDetailType Type { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Positive = charge (increases balance), negative = payment/discount/refund
    /// (decreases balance) - keeping the sign convention consistent is what makes summing
    /// Details.Sum(d => d.Amount) give the correct running balance.</summary>
    public decimal Amount { get; set; }

    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string? RecordedBy { get; set; }
}
