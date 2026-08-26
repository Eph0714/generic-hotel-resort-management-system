using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Amenities;

public class AmenityEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required, Display(Name = "Category")]
    public int AmenityCategoryId { get; set; }

    public string? Description { get; set; }

    [Range(1, 1000)]
    public int Capacity { get; set; } = 10;

    [Range(0, double.MaxValue)] public decimal HourlyRate { get; set; }
    [Range(0, double.MaxValue)] public decimal DailyRate { get; set; }
    [Range(0, double.MaxValue)] public decimal RegularRate { get; set; }
    [Range(0, double.MaxValue)] public decimal WeekendRate { get; set; }
    [Range(0, double.MaxValue)] public decimal HolidayRate { get; set; }
    [Range(0, double.MaxValue)] public decimal SeasonalRate { get; set; }

    public int MinimumHours { get; set; }
    public decimal AdditionalChargePerHour { get; set; }

    public bool IsActive { get; set; } = true;

    public List<AmenityCategory> Categories { get; set; } = new();
}
