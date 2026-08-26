using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Rooms;

public class RoomEditViewModel
{
    public int Id { get; set; }

    [Required, Display(Name = "Room Number")]
    public string RoomNumber { get; set; } = string.Empty;

    public string? RoomName { get; set; }

    [Required, Display(Name = "Room Type")]
    public int RoomTypeId { get; set; }

    [Display(Name = "Bed Type")]
    public int? BedTypeId { get; set; }

    [Display(Name = "Floor/Area")]
    public int? FloorAreaId { get; set; }

    [Range(1, 50)]
    public int Capacity { get; set; } = 2;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<RoomType> RoomTypes { get; set; } = new();
    public List<BedType> BedTypes { get; set; } = new();
    public List<FloorArea> FloorAreas { get; set; } = new();
}

public class RoomListItemViewModel
{
    public int Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string? RoomName { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public RoomStatus Status { get; set; }
    public bool IsActive { get; set; }
}
