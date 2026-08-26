using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Reservations;

public class ReservationCreateViewModel
{
    [Required, Display(Name = "Guest")]
    public int GuestId { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Check-In")]
    public DateOnly CheckInDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, DataType(DataType.Date), Display(Name = "Check-Out")]
    public DateOnly CheckOutDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

    [Range(1, 50), Display(Name = "Number of Guests")]
    public int NumberOfGuests { get; set; } = 1;

    [Required, Display(Name = "Room(s)")]
    public List<int> RoomIds { get; set; } = new();

    [Range(0, double.MaxValue), Display(Name = "Discount Amount")]
    public decimal DiscountAmount { get; set; }

    [Range(0, double.MaxValue), Display(Name = "Deposit / Amount Paid Now")]
    public decimal AmountPaid { get; set; }

    public string? SpecialRequests { get; set; }
    public string? Notes { get; set; }

    [Display(Name = "Cancellation Policy")]
    public int? CancellationPolicyId { get; set; }

    [Display(Name = "Package (optional)")]
    public int? PackageId { get; set; }

    public List<Guest> Guests { get; set; } = new();
    public List<Room> AvailableRooms { get; set; } = new();
    public List<CancellationPolicy> CancellationPolicies { get; set; } = new();
    public List<Package> Packages { get; set; } = new();
}
