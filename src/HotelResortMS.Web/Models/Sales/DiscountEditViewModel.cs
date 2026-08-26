using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Sales;

public class DiscountEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public DiscountType Type { get; set; }
    public DiscountCalculationType CalculationType { get; set; }

    [Range(0, 100)]
    public decimal Percentage { get; set; }

    [Range(0, double.MaxValue)]
    public decimal FixedAmount { get; set; }

    public bool EligibleForRooms { get; set; }
    public bool EligibleForAmenities { get; set; }
    public bool EligibleForProducts { get; set; }
    public bool EligibleForServices { get; set; }

    [Required, DataType(DataType.Date)]
    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [DataType(DataType.Date)]
    public DateOnly? ExpirationDate { get; set; }

    public bool RequiresIdVerification { get; set; }
    public bool IsActive { get; set; } = true;
}
