namespace HotelResortMS.Core.Entities;

/// <summary>Section 24: one row per completed check-in. A reservation can only be checked
/// in once (enforced by FrontDeskService) - re-checking-in after checkout would need a new
/// reservation, keeping history unambiguous.</summary>
public class CheckIn
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string? VerifiedBy { get; set; }
    public string? IdentificationVerifiedNumber { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Section 28: one row per completed check-out.</summary>
public class CheckOut
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string? ProcessedBy { get; set; }
    public decimal FinalBalance { get; set; }
    public string? Notes { get; set; }
}
