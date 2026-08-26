using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

public class PurchaseOrderLineInput
{
    public int InventoryItemId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal UnitCost { get; set; }
}

public class ReceivingLineInput
{
    public int PurchaseOrderDetailId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityDamaged { get; set; }
}

/// <summary>
/// Section 35: Purchase Order -> Receiving -> Inventory Update -> Supplier Payable ->
/// Supplier Payment, as one coherent chain rather than four disconnected screens.
/// </summary>
public interface IPurchasingService
{
    Task<PurchaseOrder> CreatePurchaseOrderAsync(int supplierId, DateOnly orderDate, DateOnly? expectedDate, IReadOnlyList<PurchaseOrderLineInput> lines, string createdBy);

    Task SubmitPurchaseOrderAsync(int purchaseOrderId, string submittedBy);

    Task CancelPurchaseOrderAsync(int purchaseOrderId, string reason, string cancelledBy);

    /// <summary>
    /// Section 35 chain in one transaction: posts a Receiving record, StockIn (or Waste for
    /// damaged quantities) InventoryTransactions via IInventoryService, accumulates
    /// QuantityReceived on the PO lines (flips the PO to PartiallyReceived/Received), and
    /// creates/increases the matching AccountsPayable balance.
    /// </summary>
    Task<Receiving> ReceiveAsync(int purchaseOrderId, IReadOnlyList<ReceivingLineInput> lines, string receivedBy, string? notes = null);
}
