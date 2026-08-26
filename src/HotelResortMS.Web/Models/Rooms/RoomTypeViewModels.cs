using System.ComponentModel.DataAnnotations;

namespace HotelResortMS.Web.Models.Rooms;

public class RoomTypeEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(1, 50)]
    public int BaseCapacity { get; set; } = 2;

    [Range(0, double.MaxValue)]
    public decimal RegularRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal WeekendRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal HolidayRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SeasonalRate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ExtraPersonRate { get; set; }

    public bool IsActive { get; set; } = true;
}
