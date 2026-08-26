using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Inventory;

public class PurchaseOrderLineViewModel
{
    public int InventoryItemId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal UnitCost { get; set; }
}

public class PurchaseOrderCreateViewModel
{
    public int SupplierId { get; set; }
    public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? ExpectedDate { get; set; }

    public List<PurchaseOrderLineViewModel> Lines { get; set; } = new();

    public List<Supplier> Suppliers { get; set; } = new();
    public List<InventoryItem> InventoryItems { get; set; } = new();
}

public class ReceivingLineViewModel
{
    public int PurchaseOrderDetailId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityDamaged { get; set; }
}

public class ReceivingCreateViewModel
{
    public int PurchaseOrderId { get; set; }
    public string? Notes { get; set; }
    public List<ReceivingLineViewModel> Lines { get; set; } = new();
}
