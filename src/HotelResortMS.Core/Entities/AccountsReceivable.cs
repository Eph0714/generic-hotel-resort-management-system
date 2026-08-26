namespace HotelResortMS.Core.Entities;

public enum AccountsReceivableStatus
{
    Open,
    PartiallyPaid,
    Paid
}

/// <summary>
/// Section 36: what a guest still owes after Check-Out with an authorized outstanding
/// balance (Section 28 allows this only with authorization) - corporate accounts, group
/// bookings, and credit customers most commonly leave one of these behind.
/// </summary>
public class AccountsReceivable : BaseEntity
{
    public int GuestId { get; set; }
    public Guest? Guest { get; set; }

    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public int? GuestFolioId { get; set; }
    public GuestFolio? GuestFolio { get; set; }

    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    public DateOnly? DueDate { get; set; }
    public AccountsReceivableStatus Status { get; set; } = AccountsReceivableStatus.Open;
}
