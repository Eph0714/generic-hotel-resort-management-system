using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Inventory;

public class InventoryItemEditViewModel
{
    public int Id { get; set; }

    public string? Sku { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, Display(Name = "Unit of Measure")]
    public int UnitOfMeasureId { get; set; }

    [Required, Display(Name = "Location")]
    public int InventoryLocationId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Cost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ReorderLevel { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? ExpirationDate { get; set; }

    public bool IsActive { get; set; } = true;

    public List<UnitOfMeasure> Units { get; set; } = new();
    public List<InventoryLocation> Locations { get; set; } = new();
}

public class StockAdjustmentViewModel
{
    public int InventoryItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }

    public InventoryTransactionType Type { get; set; } = InventoryTransactionType.Adjustment;

    [Required]
    public decimal Quantity { get; set; }

    public string? Notes { get; set; }
}
