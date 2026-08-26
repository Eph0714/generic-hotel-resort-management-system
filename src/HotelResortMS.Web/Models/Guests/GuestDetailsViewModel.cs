using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Guests;

/// <summary>Section 16: a guest's profile plus their Reservation History, so front desk
/// staff can see stay/payment history without navigating away.</summary>
public class GuestDetailsViewModel
{
    public Guest Guest { get; set; } = null!;
    public List<Reservation> Reservations { get; set; } = new();
    public decimal OutstandingBalance { get; set; }
}
