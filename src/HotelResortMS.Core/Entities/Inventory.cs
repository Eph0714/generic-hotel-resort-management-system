namespace HotelResortMS.Core.Entities;

public class UnitOfMeasure : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g. "Kilogram"
    public string Abbreviation { get; set; } = string.Empty; // e.g. "kg"
}

public class InventoryLocation : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g. "Main Kitchen Store"
    public string? Description { get; set; }
}

/// <summary>
/// Section 33: something the hotel stocks - raw ingredients, supplies, or a sellable
/// product tracked at unit level. CurrentStock is a running total maintained exclusively
/// by InventoryService as it applies InventoryTransactions - nothing else should write to
/// it directly, or the stock card (Section 44) stops reconciling.
/// </summary>
public class InventoryItem : BaseEntity
{
    public string? Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int UnitOfMeasureId { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }

    public int InventoryLocationId { get; set; }
    public InventoryLocation? InventoryLocation { get; set; }

    public decimal Cost { get; set; }
    public decimal CurrentStock { get; set; }

    /// <summary>Section 44 "Low Stock" - dashboard/report threshold, configurable per item
    /// rather than one hard-coded system-wide number.</summary>
    public decimal ReorderLevel { get; set; }

    public DateOnly? ExpirationDate { get; set; }
}

public enum InventoryTransactionType
{
    StockIn,
    StockOut,
    Purchase,
    Adjustment,
    Transfer,
    Return,
    Waste
}

/// <summary>
/// Section 33: every change to CurrentStock is one of these rows - never a direct edit.
/// Positive Quantity increases stock (StockIn/Purchase/Return/positive Adjustment),
/// negative decreases it (StockOut/Waste/negative Adjustment/Transfer-out). Once posted, a
/// transaction is corrected with an offsetting Adjustment row, never edited or deleted
/// (Section 33: "Inventory transactions cannot be permanently deleted after affecting stock").
/// </summary>
public class InventoryTransaction
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public InventoryTransactionType Type { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>"POS", "PurchaseReceiving", "Recipe", "Manual", etc. - lets the stock card
    /// report trace every movement back to what caused it.</summary>
    public string ReferenceType { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }

    public string? Notes { get; set; }

    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string RecordedBy { get; set; } = string.Empty;
}

/// <summary>Section 34 (Recipe/BOM): links a sellable Product to the InventoryItems it
/// consumes. A Product with no Recipe and TrackInventory=true is treated as directly
/// stocked (selling it deducts the product's own linked InventoryItem 1-for-1); a Product
/// with a Recipe deducts each component instead.</summary>
public class Recipe : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string? Notes { get; set; }

    public ICollection<RecipeDetail> Components { get; set; } = new List<RecipeDetail>();
}

public class RecipeDetail
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    /// <summary>Quantity of InventoryItem consumed per one unit of the Product sold.</summary>
    public decimal QuantityRequired { get; set; }
}
