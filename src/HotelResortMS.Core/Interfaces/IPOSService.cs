using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

public class POSCartItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Section 26 (Point of Sale): builds one sale end-to-end - line items -> discount ->
/// tax/service charge -> either a room-charge folio posting or an immediate Payment ->
/// audit trail. Inventory deduction hooks in here in Phase 4 without changing this
/// contract's shape.
/// </summary>
public interface IPOSService
{
    /// <summary>
    /// Completes a sale. If <paramref name="guestFolioId"/> is set, the net amount is
    /// posted as a room charge to that folio instead of collecting payment directly
    /// (Section 26: "Room Charge"). Otherwise <paramref name="paymentMethod"/> must be
    /// provided and a Payment is recorded immediately.
    /// </summary>
    /// <param name="isManualOverride">Section 18: bypasses the discount's normal active-
    /// window/eligibility checks - requires <paramref name="overrideReason"/> and must only
    /// be set by a caller that has already checked the Discounts Approve permission.</param>
    Task<POSTransaction> CompleteSaleAsync(
        IReadOnlyList<POSCartItem> items,
        int? guestId,
        int? guestFolioId,
        int? discountId,
        Core.Entities.PaymentMethod? paymentMethod,
        string? paymentReference,
        string processedBy,
        bool isManualOverride = false,
        string? overrideReason = null);

    /// <summary>Section 26/10: voids a completed sale. Reverses the folio charge if the
    /// sale was a room charge; the original transaction row is kept, only its Status
    /// changes - it is never deleted.</summary>
    Task VoidSaleAsync(int posTransactionId, string voidedBy, string reason);

    Task RefundSaleAsync(int posTransactionId, string refundedBy, string reason);
}
