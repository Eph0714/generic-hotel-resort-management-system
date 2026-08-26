using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IPOSService"/>
public class POSService : IPOSService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IBusinessDateService _businessDateService;
    private readonly IDiscountService _discountService;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;
    private readonly IIncomeService _incomeService;
    private readonly IAuditService _auditService;

    public POSService(
        ApplicationDbContext db,
        INumberingService numberingService,
        IBusinessDateService businessDateService,
        IDiscountService discountService,
        IPaymentService paymentService,
        IInventoryService inventoryService,
        IIncomeService incomeService,
        IAuditService auditService)
    {
        _db = db;
        _numberingService = numberingService;
        _businessDateService = businessDateService;
        _discountService = discountService;
        _paymentService = paymentService;
        _inventoryService = inventoryService;
        _incomeService = incomeService;
        _auditService = auditService;
    }

    /// <summary>
    /// Section 26 chain: line items -> gross -> discount (only over DiscountEligible
    /// products, Section 17/18) -> tax/service charge -> either a Guest Folio room charge
    /// or an immediate Payment. Everything happens inside one transaction so a failure
    /// partway through can never leave a half-posted sale (Section 53).
    /// </summary>
    public async Task<POSTransaction> CompleteSaleAsync(
        IReadOnlyList<POSCartItem> items,
        int? guestId,
        int? guestFolioId,
        int? discountId,
        PaymentMethod? paymentMethod,
        string? paymentReference,
        string processedBy,
        bool isManualOverride = false,
        string? overrideReason = null)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("A sale must have at least one item.");
        }
        if (guestFolioId is null && paymentMethod is null)
        {
            throw new ArgumentException("Either a room charge (guestFolioId) or a payment method is required.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        // Section 41/42/43: a new sale cannot post against a Closed business date - the
        // day must be reopened via "Open Next Business Day" first.
        var businessDate = await _businessDateService.GetCurrentForPostingAsync();
        var details = new List<POSTransactionDetail>();
        decimal grossAmount = 0m;
        decimal eligibleAmount = 0m;

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                throw new ArgumentException("Line item quantity must be greater than zero.");
            }

            var product = await _db.Products.FindAsync(item.ProductId)
                ?? throw new InvalidOperationException($"Product {item.ProductId} not found.");

            var lineTotal = product.UnitPrice * item.Quantity;
            grossAmount += lineTotal;
            if (product.DiscountEligible)
            {
                eligibleAmount += lineTotal;
            }

            details.Add(new POSTransactionDetail
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = lineTotal
            });

        }

        var posNumber = await _numberingService.GenerateAsync("POS");

        var calc = await _discountService.CalculateAsync(
            grossAmount, eligibleAmount, discountId, processedBy, "POS", posNumber, recordTransaction: false, guestId: guestId,
            isManualOverride: isManualOverride, overrideReason: overrideReason, authorizedBy: isManualOverride ? processedBy : null);

        var sale = new POSTransaction
        {
            PosNumber = posNumber,
            GuestId = guestId,
            GuestFolioId = guestFolioId,
            GrossAmount = calc.GrossAmount,
            DiscountAmount = calc.DiscountAmount,
            TaxableAmount = calc.TaxableAmount,
            TaxAmount = calc.TaxAmount,
            ServiceChargeAmount = calc.ServiceChargeAmount,
            NetAmount = calc.NetAmount,
            Status = POSTransactionStatus.Completed,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            ProcessedBy = processedBy,
            CreatedBy = processedBy
        };
        foreach (var detail in details)
        {
            sale.Details.Add(detail);
        }

        _db.POSTransactions.Add(sale);
        await _db.SaveChangesAsync();

        // Section 34: deduct recipe ingredients (or the directly-linked inventory item)
        // for every line sold - inside the same transaction as the sale itself, so a
        // stock shortfall rolls the whole sale back rather than posting a sale with no
        // corresponding deduction.
        foreach (var detail in sale.Details)
        {
            await _inventoryService.DeductForSaleAsync(detail.ProductId, detail.Quantity, "POS", sale.PosNumber, processedBy);
        }

        // Now that the sale has a real Id, record the discount transaction against it
        // (Section 18/56 - audit trail keyed to the actual POS number).
        if (discountId is not null)
        {
            await _discountService.CalculateAsync(
                grossAmount, eligibleAmount, discountId, processedBy, "POS", sale.PosNumber, recordTransaction: true, guestId: guestId,
                isManualOverride: isManualOverride, overrideReason: overrideReason, authorizedBy: isManualOverride ? processedBy : null);
        }

        if (guestFolioId is not null)
        {
            // Section 26: "Room Charge" - post the net amount to the guest's folio instead
            // of collecting payment now; it becomes part of what's owed at checkout.
            _db.FolioDetails.Add(new FolioDetail
            {
                GuestFolioId = guestFolioId.Value,
                Type = FolioDetailType.PosCharge,
                Description = $"POS sale {sale.PosNumber}",
                Amount = sale.NetAmount,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = processedBy
            });
            await _db.SaveChangesAsync();
        }
        else
        {
            await _paymentService.RecordPaymentAsync(
                guestId, null, sale.Id, sale.NetAmount, paymentMethod!.Value, paymentReference, processedBy);
        }

        // Section 38: revenue is recognized now, when the charge is posted - not when (or
        // whether) it is later collected as cash. This is the entire reason Income and
        // Payment are separate ledgers.
        var income = await _incomeService.RecordIncomeAsync(
            IncomeCategory.POS, $"POS sale {sale.PosNumber}", calc.GrossAmount, calc.DiscountAmount,
            "POS", sale.PosNumber, processedBy);
        sale.IncomeId = income.Id;
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        await _auditService.LogAsync(SystemModules.POS, "Sale", sale.Id.ToString(),
            newValues: new { sale.PosNumber, sale.NetAmount });

        return sale;
    }

    public async Task VoidSaleAsync(int posTransactionId, string voidedBy, string reason)
    {
        var sale = await _db.POSTransactions.Include(s => s.Details).FirstOrDefaultAsync(s => s.Id == posTransactionId)
            ?? throw new InvalidOperationException("Sale not found.");

        if (sale.Status != POSTransactionStatus.Completed)
        {
            throw new InvalidOperationException($"Only a Completed sale can be voided (current status: {sale.Status}).");
        }

        foreach (var detail in sale.Details)
        {
            await _inventoryService.ReverseSaleDeductionAsync(detail.ProductId, detail.Quantity, "POS-Void", sale.PosNumber, voidedBy);
        }

        sale.Status = POSTransactionStatus.Voided;
        sale.VoidedAt = DateTime.UtcNow;
        sale.VoidedBy = voidedBy;
        sale.VoidReason = reason;

        if (sale.GuestFolioId is not null)
        {
            var businessDate = await _businessDateService.GetCurrentAsync();
            _db.FolioDetails.Add(new FolioDetail
            {
                GuestFolioId = sale.GuestFolioId.Value,
                Type = FolioDetailType.PosCharge,
                Description = $"Void of POS sale {sale.PosNumber}: {reason}",
                Amount = -sale.NetAmount,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = voidedBy
            });
        }
        else
        {
            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.PosTransactionId == posTransactionId && p.Status == PaymentStatus.Completed);
            if (payment is not null)
            {
                await _paymentService.VoidPaymentAsync(payment.Id, voidedBy, $"POS sale {sale.PosNumber} voided");
            }
        }

        if (sale.IncomeId is not null)
        {
            await _incomeService.VoidIncomeAsync(sale.IncomeId.Value, voidedBy, $"POS sale {sale.PosNumber} voided");
        }

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.POS, "Void", sale.Id.ToString(), reason: reason);
    }

    public async Task RefundSaleAsync(int posTransactionId, string refundedBy, string reason)
    {
        var sale = await _db.POSTransactions.Include(s => s.Details).FirstOrDefaultAsync(s => s.Id == posTransactionId)
            ?? throw new InvalidOperationException("Sale not found.");

        if (sale.Status != POSTransactionStatus.Completed)
        {
            throw new InvalidOperationException($"Only a Completed sale can be refunded (current status: {sale.Status}).");
        }

        foreach (var detail in sale.Details)
        {
            await _inventoryService.ReverseSaleDeductionAsync(detail.ProductId, detail.Quantity, "POS-Refund", sale.PosNumber, refundedBy);
        }

        sale.Status = POSTransactionStatus.Refunded;
        sale.RefundedAt = DateTime.UtcNow;
        sale.RefundedBy = refundedBy;
        sale.RefundReason = reason;

        if (sale.GuestFolioId is not null)
        {
            var businessDate = await _businessDateService.GetCurrentAsync();
            _db.FolioDetails.Add(new FolioDetail
            {
                GuestFolioId = sale.GuestFolioId.Value,
                Type = FolioDetailType.Refund,
                Description = $"Refund of POS sale {sale.PosNumber}: {reason}",
                Amount = -sale.NetAmount,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date,
                RecordedBy = refundedBy
            });
        }
        else
        {
            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.PosTransactionId == posTransactionId && p.Status == PaymentStatus.Completed);
            if (payment is not null)
            {
                await _paymentService.RefundPaymentAsync(payment.Id, refundedBy, $"POS sale {sale.PosNumber} refunded");
            }
        }

        if (sale.IncomeId is not null)
        {
            await _incomeService.VoidIncomeAsync(sale.IncomeId.Value, refundedBy, $"POS sale {sale.PosNumber} refunded");
        }

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.POS, "Refund", sale.Id.ToString(), reason: reason);
    }
}
