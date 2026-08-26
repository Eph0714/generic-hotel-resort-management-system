namespace HotelResortMS.Core.Entities;

public enum PaymentMethod
{
    Cash,
    CreditCard,
    DebitCard,
    BankTransfer,
    EWallet,
    Other
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Refunded,
    Voided
}

/// <summary>
/// Section 27: a payment is distinct from revenue - it is cash/instrument actually
/// received (or refunded), while GuestFolio/POSTransaction track the revenue itself. A
/// payment optionally settles a GuestFolio balance or a POSTransaction, but can also stand
/// alone (e.g. a reservation deposit taken before check-in).
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public string PaymentNumber { get; set; } = string.Empty;

    public int? GuestId { get; set; }
    public Guest? Guest { get; set; }

    public int? GuestFolioId { get; set; }
    public GuestFolio? GuestFolio { get; set; }

    public int? PosTransactionId { get; set; }
    public POSTransaction? PosTransaction { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? ReferenceNumber { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;

    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;

    // Section 10: payments are never hard-deleted once completed - only voided/refunded,
    // each with a reason and an audit trail entry.
    public DateTime? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public DateTime? RefundedAt { get; set; }
    public string? RefundedBy { get; set; }
    public string? RefundReason { get; set; }
}
