using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Operations;

public class MaintenanceCategoryEditViewModel
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EquipmentEditViewModel
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? RoomId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Room> Rooms { get; set; } = new();
}

public class MaintenanceRequestCreateViewModel
{
    [Required, Display(Name = "Category")]
    public int MaintenanceCategoryId { get; set; }

    public int? RoomId { get; set; }
    public int? EquipmentId { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    public List<MaintenanceCategory> Categories { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
    public List<Equipment> EquipmentList { get; set; } = new();
}
