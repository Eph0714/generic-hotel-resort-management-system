using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>Section 31: Events/Function Hall bookings - the single place venue
/// double-booking is checked (mirrors ReservationService's room availability logic) and
/// where Confirming/Completing an event recognizes its revenue via IIncomeService.</summary>
public interface IEventService
{
    Task<bool> IsVenueAvailableAsync(int venueId, DateTime start, DateTime end, int? excludeEventId = null);

    Task<Event> CreateEventAsync(Event ev);

    Task ConfirmAsync(int eventId, string confirmedBy);
    Task CompleteAsync(int eventId, string completedBy);
    Task CancelAsync(int eventId, string reason, string cancelledBy);
}
