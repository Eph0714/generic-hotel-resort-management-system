using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IPurchasingService"/>
public class PurchasingService : IPurchasingService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IBusinessDateService _businessDateService;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditService _auditService;

    public PurchasingService(
        ApplicationDbContext db,
        INumberingService numberingService,
        IBusinessDateService businessDateService,
        IInventoryService inventoryService,
        IAuditService auditService)
    {
        _db = db;
        _numberingService = numberingService;
        _businessDateService = businessDateService;
        _inventoryService = inventoryService;
        _auditService = auditService;
    }

    public async Task<PurchaseOrder> CreatePurchaseOrderAsync(
        int supplierId, DateOnly orderDate, DateOnly? expectedDate, IReadOnlyList<PurchaseOrderLineInput> lines, string createdBy)
    {
        if (lines.Count == 0)
        {
            throw new ArgumentException("A purchase order must have at least one line.");
        }

        var po = new PurchaseOrder
        {
            PONumber = await _numberingService.GenerateAsync("Purchase"),
            SupplierId = supplierId,
            OrderDate = orderDate,
            ExpectedDate = expectedDate,
            Status = PurchaseOrderStatus.Draft,
            CreatedBy = createdBy
        };

        foreach (var line in lines)
        {
            po.Details.Add(new PurchaseOrderDetail
            {
                InventoryItemId = line.InventoryItemId,
                QuantityOrdered = line.QuantityOrdered,
                UnitCost = line.UnitCost
            });
        }
        po.TotalAmount = lines.Sum(l => l.QuantityOrdered * l.UnitCost);

        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Purchasing, "Create", po.Id.ToString(), newValues: new { po.PONumber, po.TotalAmount });
        return po;
    }

    public async Task SubmitPurchaseOrderAsync(int purchaseOrderId, string submittedBy)
    {
        var po = await _db.PurchaseOrders.FindAsync(purchaseOrderId)
            ?? throw new InvalidOperationException("Purchase order not found.");

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException($"Only a Draft purchase order can be submitted (current status: {po.Status}).");
        }

        po.Status = PurchaseOrderStatus.Submitted;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Purchasing, "Submit", po.Id.ToString());
    }

    public async Task CancelPurchaseOrderAsync(int purchaseOrderId, string reason, string cancelledBy)
    {
        var po = await _db.PurchaseOrders.FindAsync(purchaseOrderId)
            ?? throw new InvalidOperationException("Purchase order not found.");

        if (po.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled)
        {
            // Section 8/10: once goods have been received (and posted to inventory/AP),
            // the PO is historical - cancel it before receiving, not after.
            throw new InvalidOperationException($"A {po.Status} purchase order cannot be cancelled.");
        }

        po.Status = PurchaseOrderStatus.Cancelled;
        po.CancelledAt = DateTime.UtcNow;
        po.CancelledBy = cancelledBy;
        po.CancellationReason = reason;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Purchasing, "Cancel", po.Id.ToString(), reason: reason);
    }

    public async Task<Receiving> ReceiveAsync(int purchaseOrderId, IReadOnlyList<ReceivingLineInput> lines, string receivedBy, string? notes = null)
    {
        if (lines.Count == 0)
        {
            throw new ArgumentException("A receiving must have at least one line.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var po = await _db.PurchaseOrders.Include(p => p.Details).FirstOrDefaultAsync(p => p.Id == purchaseOrderId)
            ?? throw new InvalidOperationException("Purchase order not found.");

        if (po.Status is not (PurchaseOrderStatus.Submitted or PurchaseOrderStatus.PartiallyReceived))
        {
            throw new InvalidOperationException($"Cannot receive against a purchase order that is {po.Status}.");
        }

        var businessDate = await _businessDateService.GetCurrentAsync();

        var receiving = new Receiving
        {
            ReceivingNumber = await _numberingService.GenerateAsync("Receiving"),
            PurchaseOrderId = purchaseOrderId,
            ReceivedDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            ReceivedBy = receivedBy,
            Notes = notes,
            CreatedBy = receivedBy
        };

        decimal receivedTotal = 0m;

        foreach (var line in lines)
        {
            var poDetail = po.Details.FirstOrDefault(d => d.Id == line.PurchaseOrderDetailId)
                ?? throw new InvalidOperationException("Purchase order line not found.");

            var goodQuantity = line.QuantityReceived - line.QuantityDamaged;
            if (goodQuantity < 0)
            {
                throw new ArgumentException("Damaged quantity cannot exceed quantity received.");
            }

            receiving.Details.Add(new ReceivingDetail
            {
                PurchaseOrderDetailId = poDetail.Id,
                InventoryItemId = poDetail.InventoryItemId,
                QuantityReceived = line.QuantityReceived,
                UnitCost = poDetail.UnitCost,
                QuantityDamaged = line.QuantityDamaged
            });

            poDetail.QuantityReceived += line.QuantityReceived;
            receivedTotal += goodQuantity * poDetail.UnitCost;

            // Section 33/35: good quantity goes into stock; damaged quantity is posted as
            // Waste instead of silently added to sellable/usable stock.
            if (goodQuantity > 0)
            {
                await _inventoryService.PostTransactionAsync(
                    poDetail.InventoryItemId, InventoryTransactionType.Purchase, goodQuantity,
                    "PurchaseReceiving", receiving.ReceivingNumber, receivedBy, allowNegative: true);
            }
            if (line.QuantityDamaged > 0)
            {
                await _inventoryService.PostTransactionAsync(
                    poDetail.InventoryItemId, InventoryTransactionType.Waste, 0,
                    "PurchaseReceiving", receiving.ReceivingNumber, receivedBy,
                    $"{line.QuantityDamaged} unit(s) received damaged", allowNegative: true);
            }
        }

        _db.Receivings.Add(receiving);

        // Section 35: PO status reflects whether every line has now been fully received.
        po.Status = po.Details.All(d => d.QuantityReceived >= d.QuantityOrdered)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;

        // Section 37: create/extend the payable for what was actually received (good
        // quantity only - damaged goods received are not something the hotel owes for
        // once the supplier is notified, but that dispute process is outside this system's
        // scope for now, so it is still initially payable and adjusted manually if waived).
        if (receivedTotal > 0)
        {
            var payable = new AccountsPayable
            {
                SupplierId = po.SupplierId,
                PurchaseOrderId = po.Id,
                Amount = receivedTotal,
                AmountPaid = 0,
                Balance = receivedTotal,
                Status = AccountsPayableStatus.Open,
                CreatedBy = receivedBy
            };
            _db.AccountsPayables.Add(payable);
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        await _auditService.LogAsync(SystemModules.Purchasing, "Receive", po.Id.ToString(),
            newValues: new { receiving.ReceivingNumber, receivedTotal });

        return receiving;
    }
}
