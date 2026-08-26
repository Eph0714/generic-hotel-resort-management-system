using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Sales;

public class ProductCategoryEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProductEditViewModel
{
    public int Id { get; set; }

    public string? Sku { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, Display(Name = "Category")]
    public int ProductCategoryId { get; set; }

    public ProductType Type { get; set; } = ProductType.Retail;

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Cost { get; set; }

    public bool TrackInventory { get; set; }

    /// <summary>Section 34: only used when TrackInventory is true and the product has no
    /// Recipe - selling it then deducts this InventoryItem directly (e.g. a bottled drink
    /// that is itself the stocked item, not an assembled dish).</summary>
    [Display(Name = "Directly-stocked inventory item (if no recipe)")]
    public int? InventoryItemId { get; set; }

    public bool DiscountEligible { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public List<ProductCategory> Categories { get; set; } = new();
    public List<InventoryItem> InventoryItems { get; set; } = new();
}
