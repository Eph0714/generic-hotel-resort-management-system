namespace HotelResortMS.Core.Entities;

/// <summary>Section 6/33: groups products for the POS product picker and inventory
/// reports (e.g. Food, Beverage, Retail, Rentals).</summary>
public class ProductCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public enum ProductType
{
    Food,
    Beverage,
    Retail,
    Service,
    Rental,
    Other
}

/// <summary>
/// Section 26/33: something the POS can sell. Inventory linkage (current stock, recipes,
/// deduction) is wired in Phase 4 - TrackInventory simply flags which products will
/// participate once that phase lands, so this entity does not need to change shape later.
/// </summary>
public class Product : BaseEntity
{
    public string? Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int ProductCategoryId { get; set; }
    public ProductCategory? ProductCategory { get; set; }

    public ProductType Type { get; set; } = ProductType.Retail;

    /// <summary>Selling price used by POS - never overwritten retroactively on historical
    /// sales (Section 15); POSTransactionDetail snapshots the price actually charged.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Cost basis, used later by inventory valuation reports (Phase 4/7).</summary>
    public decimal Cost { get; set; }

    public bool TrackInventory { get; set; }

    /// <summary>When TrackInventory is true and the product has no Recipe (Section 34),
    /// selling one unit deducts one unit of this InventoryItem directly - e.g. a bottled
    /// water that is itself the stocked item, not an assembled dish.</summary>
    public int? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    /// <summary>Whether Senior Citizen/PWD discounts may legally apply to this item
    /// (Section 17/18 - some goods/services are not eligible).</summary>
    public bool DiscountEligible { get; set; } = true;
}
