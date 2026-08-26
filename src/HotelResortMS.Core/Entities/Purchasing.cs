namespace HotelResortMS.Core.Entities;

public enum PurchaseOrderStatus
{
    Draft,
    Submitted,
    PartiallyReceived,
    Received,
    Cancelled
}

/// <summary>
/// Section 35: Purchase Order -> Receiving -> Inventory Update -> Supplier Payable ->
/// Supplier Payment. Supports partial receiving (Section 35) - QuantityReceived on each
/// detail line accumulates across one or more Receiving records until it reaches
/// QuantityOrdered, at which point the PO's own Status flips to Received.
/// </summary>
public class PurchaseOrder : BaseEntity
{
    public string PONumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<PurchaseOrderDetail> Details { get; set; } = new List<PurchaseOrderDetail>();
}

public class PurchaseOrderDetail
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
}

/// <summary>One receiving event against a PO - may be partial (Section 35).</summary>
public class Receiving : BaseEntity
{
    public string ReceivingNumber { get; set; } = string.Empty;

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public DateTime ReceivedDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public ICollection<ReceivingDetail> Details { get; set; } = new List<ReceivingDetail>();
}

public class ReceivingDetail
{
    public int Id { get; set; }

    public int ReceivingId { get; set; }
    public Receiving? Receiving { get; set; }

    public int PurchaseOrderDetailId { get; set; }
    public PurchaseOrderDetail? PurchaseOrderDetail { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }

    /// <summary>Section 35: damaged/short items received are flagged here rather than
    /// silently added to good stock - InventoryService posts them as Waste instead of StockIn.</summary>
    public decimal QuantityDamaged { get; set; }
}

public enum AccountsPayableStatus
{
    Open,
    PartiallyPaid,
    Paid
}

/// <summary>Section 37: what is owed to a supplier after Receiving posts goods. Supports
/// partial payment (Section 35).</summary>
public class AccountsPayable : BaseEntity
{
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    public DateOnly? DueDate { get; set; }
    public AccountsPayableStatus Status { get; set; } = AccountsPayableStatus.Open;
}
