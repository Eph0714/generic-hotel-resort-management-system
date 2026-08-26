using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Sales;

/// <summary>Backs the new-sale screen - the product grid and dropdowns it needs to render.
/// The cart itself is built client-side in JS and posted as parallel arrays on submit;
/// POSService recomputes every total server-side, so nothing here is trusted for money math.</summary>
public class POSSaleViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<ProductCategory> Categories { get; set; } = new();
    public List<Discount> ActiveDiscounts { get; set; } = new();
    public List<Guest> Guests { get; set; } = new();
}

public class POSCompleteRequest
{
    public int[] ProductId { get; set; } = Array.Empty<int>();
    public int[] Quantity { get; set; } = Array.Empty<int>();

    public int? GuestId { get; set; }
    public int? GuestFolioId { get; set; }
    public int? DiscountId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }

    /// <summary>Section 18: an authorized manual discount override - bypasses the normal
    /// active-window/eligibility checks. Only reachable by a user with the Discounts
    /// Approve permission (POSController checks this before honoring the flag).</summary>
    public bool IsManualOverride { get; set; }
    public string? OverrideReason { get; set; }
}
