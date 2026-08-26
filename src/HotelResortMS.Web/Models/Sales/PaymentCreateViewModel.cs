using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Sales;

public class PaymentCreateViewModel
{
    [Required, Display(Name = "Guest")]
    public int GuestId { get; set; }

    [Display(Name = "Apply to Folio (optional)")]
    public int? GuestFolioId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }
    public string? ReferenceNumber { get; set; }

    public List<Guest> Guests { get; set; } = new();
}
