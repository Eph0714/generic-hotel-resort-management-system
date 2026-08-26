namespace HotelResortMS.Core.Entities;

public enum POSTransactionStatus
{
    Completed,
    Voided,
    Refunded
}

/// <summary>
/// Section 26: one point-of-sale sale. Deliberately mirrors GuestFolio's gross->discount->
/// tax->net breakdown (Section 19) so every sales surface in the system computes totals the
/// same way. A completed transaction is never hard-deleted (Section 10/26) - only Void or
/// Refund, both requiring the matching permission and leaving an audit trail.
/// </summary>
public class POSTransaction : BaseEntity
{
    public string PosNumber { get; set; } = string.Empty;

    /// <summary>Null for a walk-in/anonymous sale.</summary>
    public int? GuestId { get; set; }
    public Guest? Guest { get; set; }

    /// <summary>When set, this sale was charged to the guest's room folio instead of paid
    /// directly (Section 26: POS -> Guest Folio integration).</summary>
    public int? GuestFolioId { get; set; }
    public GuestFolio? GuestFolio { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal NetAmount { get; set; }

    /// <summary>Section 38: the Income row recognizing this sale's revenue at the moment
    /// it was rung up - voided together with the sale (Void/Refund), never independently.</summary>
    public int? IncomeId { get; set; }
    public Income? Income { get; set; }

    public POSTransactionStatus Status { get; set; } = POSTransactionStatus.Completed;

    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;

    public DateTime? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public DateTime? RefundedAt { get; set; }
    public string? RefundedBy { get; set; }
    public string? RefundReason { get; set; }

    public ICollection<POSTransactionDetail> Details { get; set; } = new List<POSTransactionDetail>();
}

/// <summary>One line item on a POS sale. Snapshots the product name/price at sale time so
/// later edits to the Product catalog never alter historical sales (Section 15).</summary>
public class POSTransactionDetail
{
    public int Id { get; set; }

    public int POSTransactionId { get; set; }
    public POSTransaction? POSTransaction { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
