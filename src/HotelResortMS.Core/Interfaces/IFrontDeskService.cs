using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>
/// Section 24/28: Check-In/Check-Out workflow. Owns the Reservation status transition,
/// Room status transition, and GuestFolio creation/closing together so they can never
/// drift out of sync with each other.
/// </summary>
public interface IFrontDeskService
{
    Task<GuestFolio> CheckInAsync(int reservationId, string verifiedBy, string? identificationVerifiedNumber = null);

    /// <summary>Throws if the folio still has a positive balance and no override is
    /// authorized (Section 28 - "Require payment or authorized credit before completing
    /// checkout").</summary>
    Task<CheckOut> CheckOutAsync(int reservationId, string processedBy, bool authorizeOutstandingBalance = false);

    Task<GuestFolio?> GetFolioForReservationAsync(int reservationId);

    /// <summary>Posts one line to an open folio (room charge, POS charge, discount, tax,
    /// payment, etc.) - the single write path every other module's charges eventually flow
    /// through (Section 25/53).</summary>
    Task<FolioDetail> PostFolioChargeAsync(int folioId, FolioDetailType type, string description, decimal amount, string recordedBy);
}
