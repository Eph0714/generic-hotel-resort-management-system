namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 12 (Business Date) / Section 41 (Daily Opening) / Section 43 (Daily Closing):
/// the hotel's operating date, distinct from the wall-clock calendar date. Operations after
/// midnight still post against the previous BusinessDate until Night Audit + Daily Closing
/// authorize the roll-forward - this is what lets a night auditor keep working past 12 AM.
///
/// One row is created per business day (via Daily Opening) and updated in place as the day
/// proceeds through Night Audit and Daily Closing; it is never deleted.
/// </summary>
public class BusinessDate
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public enum DateStatus
    {
        Open,
        NightAuditInProgress,
        Closed
    }

    public DateStatus Status { get; set; } = DateStatus.Open;

    public DateTime OpenedAt { get; set; }
    public string? OpenedBy { get; set; }

    public DateTime? NightAuditStartedAt { get; set; }
    public string? NightAuditBy { get; set; }

    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    /// <summary>Carried forward from the previous business date's ending cash (Section 41).</summary>
    public decimal BeginningCash { get; set; }

    /// <summary>Populated by Daily Closing (Section 43); feeds next day's BeginningCash.</summary>
    public decimal? EndingCash { get; set; }

    /// <summary>Set if a Super Admin reopened an already-closed business date (Section 43: "Reopening
    /// requires authorization and complete audit trail").</summary>
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenedBy { get; set; }
    public string? ReopenReason { get; set; }
}
