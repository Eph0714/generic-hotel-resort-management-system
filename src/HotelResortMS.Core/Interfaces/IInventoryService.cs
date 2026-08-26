using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>
/// Section 33/34: the single write path for stock movements. POSService, PurchasingService,
/// and manual adjustment screens all call this instead of touching InventoryItem.CurrentStock
/// directly - that is what keeps the stock card (Section 44) trustworthy.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Posts one InventoryTransaction and updates CurrentStock. Throws if the result would
    /// go negative and <paramref name="allowNegative"/> is false (Section 33: "Prevent
    /// unauthorized negative inventory").
    /// </summary>
    Task<InventoryTransaction> PostTransactionAsync(
        int inventoryItemId,
        InventoryTransactionType type,
        decimal quantity,
        string referenceType,
        string? referenceId,
        string recordedBy,
        string? notes = null,
        bool allowNegative = false);

    /// <summary>
    /// Section 34: deducts whatever a sale of this product requires - its Recipe's
    /// components if it has one, otherwise its own directly-linked InventoryItem. A no-op
    /// if the product tracks no inventory at all.
    /// </summary>
    Task DeductForSaleAsync(int productId, int quantitySold, string referenceType, string referenceId, string recordedBy);

    /// <summary>Reverses a prior sale's deduction (POS void/refund) with an equal and
    /// opposite StockIn/Return posting rather than deleting the original transaction.</summary>
    Task ReverseSaleDeductionAsync(int productId, int quantitySold, string referenceType, string referenceId, string recordedBy);
}
