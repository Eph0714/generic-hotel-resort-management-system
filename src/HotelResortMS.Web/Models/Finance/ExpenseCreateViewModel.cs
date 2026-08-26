using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Finance;

public class ExpenseCreateViewModel
{
    public ExpenseCategory Category { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public string? Payee { get; set; }
    public string? Reference { get; set; }
    public string? Remarks { get; set; }
}
