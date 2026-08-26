using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>
/// Section 27: records cash/instrument received or refunded. Distinct from revenue -
/// FolioService/POSService post the charge, PaymentService posts what was actually
/// collected against it (they can differ: deposits, partial payments, credit).
/// </summary>
public interface IPaymentService
{
    Task<Payment> RecordPaymentAsync(
        int? guestId,
        int? guestFolioId,
        int? posTransactionId,
        decimal amount,
        PaymentMethod method,
        string? referenceNumber,
        string processedBy);

    /// <summary>Section 10: a completed payment is never deleted, only voided with a
    /// reason - requires the Void permission on the Payments module.</summary>
    Task VoidPaymentAsync(int paymentId, string voidedBy, string reason);

    Task RefundPaymentAsync(int paymentId, string refundedBy, string reason);
}
