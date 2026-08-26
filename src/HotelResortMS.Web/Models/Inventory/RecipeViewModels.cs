using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Inventory;

public class RecipeComponentInput
{
    public int InventoryItemId { get; set; }
    public decimal QuantityRequired { get; set; }
}

public class RecipeEditViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public List<RecipeComponentInput> Components { get; set; } = new();
    public List<InventoryItem> AvailableItems { get; set; } = new();
}
