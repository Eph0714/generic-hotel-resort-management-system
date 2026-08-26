using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Operations;

public class CancellationPolicyEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CancellationPolicyType Type { get; set; }

    [Range(0, 10000)]
    public int HoursBeforeCheckIn { get; set; } = 24;

    [Range(0, 100)]
    public decimal FeePercentage { get; set; }

    [Range(0, double.MaxValue)]
    public decimal FixedFee { get; set; }

    public bool IsActive { get; set; } = true;
}
