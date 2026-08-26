using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IEventService"/>
public class EventService : IEventService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IIncomeService _incomeService;
    private readonly IAuditService _auditService;

    public EventService(ApplicationDbContext db, INumberingService numberingService, IIncomeService incomeService, IAuditService auditService)
    {
        _db = db;
        _numberingService = numberingService;
        _incomeService = incomeService;
        _auditService = auditService;
    }

    /// <summary>Section 21's overlap logic, applied to a Venue instead of a Room - the
    /// same "no two confirmed bookings overlap" rule Reservations enforce.</summary>
    public async Task<bool> IsVenueAvailableAsync(int venueId, DateTime start, DateTime end, int? excludeEventId = null)
    {
        var conflicting = await _db.Events
            .Where(e => e.EventVenueId == venueId
                        && (e.Status == EventStatus.Pending || e.Status == EventStatus.Confirmed || e.Status == EventStatus.InProgress)
                        && e.StartDateTime < end && start < e.EndDateTime
                        && (excludeEventId == null || e.Id != excludeEventId))
            .AnyAsync();
        return !conflicting;
    }

    public async Task<Event> CreateEventAsync(Event ev)
    {
        if (ev.EndDateTime <= ev.StartDateTime)
        {
            throw new ArgumentException("End time must be after start time.");
        }
        if (!await IsVenueAvailableAsync(ev.EventVenueId, ev.StartDateTime, ev.EndDateTime))
        {
            throw new InvalidOperationException("This venue is already booked for an overlapping time.");
        }

        ev.EventNumber = await _numberingService.GenerateAsync("Event");
        ev.BalanceDue = ev.TotalAmount - ev.DepositAmount - ev.AmountPaid;
        ev.Status = EventStatus.Pending;

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Events, "Create", ev.Id.ToString(), newValues: new { ev.EventNumber, ev.TotalAmount });
        return ev;
    }

    public async Task ConfirmAsync(int eventId, string confirmedBy)
    {
        var ev = await _db.Events.FindAsync(eventId)
            ?? throw new InvalidOperationException("Event not found.");

        if (ev.Status != EventStatus.Pending)
        {
            throw new InvalidOperationException($"Only a Pending event can be confirmed (current status: {ev.Status}).");
        }

        // Re-check the venue at confirmation time too - a second event could have been
        // confirmed against an overlapping slot while this one still sat Pending.
        if (!await IsVenueAvailableAsync(ev.EventVenueId, ev.StartDateTime, ev.EndDateTime, ev.Id))
        {
            throw new InvalidOperationException("This venue is no longer available for this time slot.");
        }

        ev.Status = EventStatus.Confirmed;
        ev.UpdatedAt = DateTime.UtcNow;
        ev.UpdatedBy = confirmedBy;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Events, "Confirm", eventId.ToString());
    }

    public async Task CompleteAsync(int eventId, string completedBy)
    {
        var ev = await _db.Events.FindAsync(eventId)
            ?? throw new InvalidOperationException("Event not found.");

        if (ev.Status is not (EventStatus.Confirmed or EventStatus.InProgress))
        {
            throw new InvalidOperationException($"Only a Confirmed or InProgress event can be completed (current status: {ev.Status}).");
        }

        ev.Status = EventStatus.Completed;
        ev.UpdatedAt = DateTime.UtcNow;
        ev.UpdatedBy = completedBy;
        await _db.SaveChangesAsync();

        // Section 38: recognize event revenue when the event actually happens, mirroring
        // how room/POS revenue is recognized at the point the charge is realized.
        await _incomeService.RecordIncomeAsync(
            IncomeCategory.Events, $"Event {ev.EventNumber}", ev.TotalAmount, 0, "Event", ev.EventNumber, completedBy);

        await _auditService.LogAsync(SystemModules.Events, "Complete", eventId.ToString());
    }

    public async Task CancelAsync(int eventId, string reason, string cancelledBy)
    {
        var ev = await _db.Events.FindAsync(eventId)
            ?? throw new InvalidOperationException("Event not found.");

        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
        {
            throw new InvalidOperationException($"A {ev.Status} event cannot be cancelled.");
        }

        ev.Status = EventStatus.Cancelled;
        ev.CancelledAt = DateTime.UtcNow;
        ev.CancelledBy = cancelledBy;
        ev.CancellationReason = reason;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Events, "Cancel", eventId.ToString(), reason: reason);
    }
}
