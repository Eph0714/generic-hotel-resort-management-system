using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IPaymentService"/>
public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IBusinessDateService _businessDateService;
    private readonly IAuditService _auditService;

    public PaymentService(
        ApplicationDbContext db,
        INumberingService numberingService,
        IBusinessDateService businessDateService,
        IAuditService auditService)
    {
        _db = db;
        _numberingService = numberingService;
        _businessDateService = businessDateService;
        _auditService = auditService;
    }

    public async Task<Payment> RecordPaymentAsync(
        int? guestId,
        int? guestFolioId,
        int? posTransactionId,
        decimal amount,
        PaymentMethod method,
        string? referenceNumber,
        string processedBy)
    {
        if (amount <= 0)
        {
            // Section 52: negative/zero payments are a validation error, not a legitimate
            // "no payment" case - callers that mean no payment simply don't call this.
            throw new ArgumentException("Payment amount must be greater than zero.");
        }

        var businessDate = await _businessDateService.GetCurrentForPostingAsync();

        var payment = new Payment
        {
            PaymentNumber = await _numberingService.GenerateAsync("Payment"),
            GuestId = guestId,
            GuestFolioId = guestFolioId,
            PosTransactionId = posTransactionId,
            Amount = amount,
            Method = method,
            ReferenceNumber = referenceNumber,
            Status = PaymentStatus.Completed,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            ProcessedBy = processedBy
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Section 25: a payment against an open folio also posts as a negative
        // (balance-reducing) FolioDetail line, so the folio's running balance always
        // reflects cash actually collected without the two ledgers drifting apart.
        if (guestFolioId is not null)
        {
            _db.FolioDetails.Add(new FolioDetail
            {
                GuestFolioId = guestFolioId.Value,
                Type = FolioDetailType.Payment,
                Description = $"Payment {payment.PaymentNumber} ({method})",
                Amount = -amount,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = processedBy
            });
            await _db.SaveChangesAsync();
        }

        await _auditService.LogAsync(SystemModules.Payments, "Create", payment.Id.ToString(),
            newValues: new { payment.PaymentNumber, payment.Amount, payment.Method });

        return payment;
    }

    public async Task VoidPaymentAsync(int paymentId, string voidedBy, string reason)
    {
        var payment = await _db.Payments.FindAsync(paymentId)
            ?? throw new InvalidOperationException("Payment not found.");

        if (payment.Status != PaymentStatus.Completed)
        {
            throw new InvalidOperationException($"Only a Completed payment can be voided (current status: {payment.Status}).");
        }

        payment.Status = PaymentStatus.Voided;
        payment.VoidedAt = DateTime.UtcNow;
        payment.VoidedBy = voidedBy;
        payment.VoidReason = reason;

        // Reverse the effect on the folio (if any) with an offsetting line rather than
        // deleting the original - Section 25 requires every adjustment stay traceable.
        if (payment.GuestFolioId is not null)
        {
            var businessDate = await _businessDateService.GetCurrentAsync();
            _db.FolioDetails.Add(new FolioDetail
            {
                GuestFolioId = payment.GuestFolioId.Value,
                Type = FolioDetailType.Payment,
                Description = $"Void of payment {payment.PaymentNumber}: {reason}",
                Amount = payment.Amount,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = voidedBy
            });
        }

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Payments, "Void", payment.Id.ToString(), reason: reason);
    }

    public async Task RefundPaymentAsync(int paymentId, string refundedBy, string reason)
    {
        var payment = await _db.Payments.FindAsync(paymentId)
            ?? throw new InvalidOperationException("Payment not found.");

        if (payment.Status != PaymentStatus.Completed)
        {
            throw new InvalidOperationException($"Only a Completed payment can be refunded (current status: {payment.Status}).");
        }

        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAt = DateTime.UtcNow;
        payment.RefundedBy = refundedBy;
        payment.RefundReason = reason;

        if (payment.GuestFolioId is not null)
        {
            var businessDate = await _businessDateService.GetCurrentAsync();
            _db.FolioDetails.Add(new FolioDetail
            {
                GuestFolioId = payment.GuestFolioId.Value,
                Type = FolioDetailType.Refund,
                Description = $"Refund of payment {payment.PaymentNumber}: {reason}",
                Amount = payment.Amount,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = refundedBy
            });
        }

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Payments, "Refund", payment.Id.ToString(), reason: reason);
    }
}
