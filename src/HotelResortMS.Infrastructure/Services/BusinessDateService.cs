using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IBusinessDateService"/>
public class BusinessDateService : IBusinessDateService
{
    private readonly ApplicationDbContext _db;

    public BusinessDateService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns the current (most recent) business date - Open, NightAuditInProgress, or
    /// Closed - creating the very first one (today, zero beginning cash) only on the
    /// system's first-ever run, when no BusinessDate row exists at all.
    ///
    /// This deliberately does NOT auto-open a fresh day just because the latest one is
    /// Closed: the previous behavior checked only for a non-Closed row and silently
    /// fabricated a brand-new "today" (calendar date, zero beginning cash) the instant
    /// Daily Closing ran, which bypassed OpenNextDayAsync's explicit-authorization step
    /// and threw away the just-closed day's ending cash instead of carrying it forward
    /// (Section 41). Callers that need to post financial activity should check
    /// Status == Open (or NightAuditInProgress) themselves; everything else (audit
    /// logging, dashboards, reports) is fine reading a Closed date's metadata.
    /// </summary>
    public async Task<BusinessDate> GetCurrentAsync()
    {
        var current = await _db.BusinessDates
            .OrderByDescending(b => b.Date)
            .FirstOrDefaultAsync();

        if (current is not null)
        {
            return current;
        }

        // First run of the system: open today as the initial business date.
        var today = new BusinessDate
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = BusinessDate.DateStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpenedBy = "System",
            BeginningCash = 0m
        };

        _db.BusinessDates.Add(today);
        await _db.SaveChangesAsync();
        return today;
    }

    /// <inheritdoc cref="IBusinessDateService.GetCurrentForPostingAsync"/>
    public async Task<BusinessDate> GetCurrentForPostingAsync()
    {
        var current = await GetCurrentAsync();
        if (current.Status == BusinessDate.DateStatus.Closed)
        {
            throw new InvalidOperationException(
                $"Business date {current.Date:yyyy-MM-dd} is closed. Open the next business day before posting new activity.");
        }
        return current;
    }

    /// <summary>
    /// Section 41 (Daily Opening): closes today's date is NOT done here (that is Daily
    /// Closing's job) - this only guards against opening a new business date while one is
    /// still active, and carries the previous day's ending cash forward as the new
    /// beginning cash.
    /// </summary>
    public async Task<BusinessDate> OpenNextDayAsync(string userId, decimal? overrideBeginningCash = null)
    {
        var openDate = await _db.BusinessDates
            .Where(b => b.Status != BusinessDate.DateStatus.Closed)
            .OrderByDescending(b => b.Date)
            .FirstOrDefaultAsync();

        if (openDate is not null)
        {
            // Prevent duplicate opening (Section 41) - a day must be closed before the next one opens.
            throw new InvalidOperationException(
                $"Business date {openDate.Date:yyyy-MM-dd} is still {openDate.Status}. Complete Night Audit and Daily Closing before opening the next day.");
        }

        var previous = await _db.BusinessDates
            .OrderByDescending(b => b.Date)
            .FirstOrDefaultAsync();

        var beginningCash = overrideBeginningCash ?? previous?.EndingCash ?? 0m;

        var next = new BusinessDate
        {
            Date = previous is null
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : previous.Date.AddDays(1),
            Status = BusinessDate.DateStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpenedBy = userId,
            BeginningCash = beginningCash
        };

        _db.BusinessDates.Add(next);
        await _db.SaveChangesAsync();
        return next;
    }
}
