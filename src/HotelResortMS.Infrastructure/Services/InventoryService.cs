using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IInventoryService"/>
public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDateService _businessDateService;
    private readonly IAuditService _auditService;

    public InventoryService(ApplicationDbContext db, IBusinessDateService businessDateService, IAuditService auditService)
    {
        _db = db;
        _businessDateService = businessDateService;
        _auditService = auditService;
    }

    public async Task<InventoryTransaction> PostTransactionAsync(
        int inventoryItemId,
        InventoryTransactionType type,
        decimal quantity,
        string referenceType,
        string? referenceId,
        string recordedBy,
        string? notes = null,
        bool allowNegative = false)
    {
        var item = await _db.InventoryItems.FindAsync(inventoryItemId)
            ?? throw new InvalidOperationException("Inventory item not found.");

        var resultingStock = item.CurrentStock + quantity;
        if (resultingStock < 0 && !allowNegative)
        {
            // Section 33: "Prevent unauthorized negative inventory" - refuse rather than
            // let the stock card go below zero without an explicit, authorized override.
            throw new InvalidOperationException(
                $"This action would bring '{item.Name}' stock to {resultingStock:N2}, below zero. Not enough stock on hand.");
        }

        var businessDate = await _businessDateService.GetCurrentAsync();

        var transaction = new InventoryTransaction
        {
            InventoryItemId = inventoryItemId,
            Type = type,
            Quantity = quantity,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Notes = notes,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            RecordedBy = recordedBy
        };

        item.CurrentStock = resultingStock;
        _db.InventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Inventory, type.ToString(), item.Id.ToString(),
            newValues: new { item.Name, quantity, resultingStock });

        return transaction;
    }

    public async Task DeductForSaleAsync(int productId, int quantitySold, string referenceType, string referenceId, string recordedBy)
    {
        var product = await _db.Products.FindAsync(productId)
            ?? throw new InvalidOperationException("Product not found.");

        if (!product.TrackInventory)
        {
            return;
        }

        var recipe = await _db.Recipes
            .Include(r => r.Components)
            .FirstOrDefaultAsync(r => r.ProductId == productId && r.IsActive);

        if (recipe is not null)
        {
            // Section 34: "POS Sale -> Recipe -> Ingredient Deduction" - deduct every
            // component scaled by the quantity of the finished product sold.
            foreach (var component in recipe.Components)
            {
                await PostTransactionAsync(
                    component.InventoryItemId, InventoryTransactionType.StockOut,
                    -component.QuantityRequired * quantitySold,
                    referenceType, referenceId, recordedBy,
                    $"Recipe deduction for {product.Name} x{quantitySold}");
            }
        }
        else if (product.InventoryItemId is not null)
        {
            // Directly-stocked product (e.g. a bottled drink that is itself the inventory item).
            await PostTransactionAsync(
                product.InventoryItemId.Value, InventoryTransactionType.StockOut, -quantitySold,
                referenceType, referenceId, recordedBy, $"Sale of {product.Name} x{quantitySold}");
        }
    }

    public async Task ReverseSaleDeductionAsync(int productId, int quantitySold, string referenceType, string referenceId, string recordedBy)
    {
        var product = await _db.Products.FindAsync(productId)
            ?? throw new InvalidOperationException("Product not found.");

        if (!product.TrackInventory)
        {
            return;
        }

        var recipe = await _db.Recipes
            .Include(r => r.Components)
            .FirstOrDefaultAsync(r => r.ProductId == productId && r.IsActive);

        if (recipe is not null)
        {
            foreach (var component in recipe.Components)
            {
                // Reversal always allows the resulting stock to go up regardless of the
                // negative-stock guard - putting stock back can never be the unsafe direction.
                await PostTransactionAsync(
                    component.InventoryItemId, InventoryTransactionType.Return,
                    component.QuantityRequired * quantitySold,
                    referenceType, referenceId, recordedBy,
                    $"Void/refund reversal for {product.Name} x{quantitySold}", allowNegative: true);
            }
        }
        else if (product.InventoryItemId is not null)
        {
            await PostTransactionAsync(
                product.InventoryItemId.Value, InventoryTransactionType.Return, quantitySold,
                referenceType, referenceId, recordedBy, $"Void/refund reversal for {product.Name} x{quantitySold}", allowNegative: true);
        }
    }
}
