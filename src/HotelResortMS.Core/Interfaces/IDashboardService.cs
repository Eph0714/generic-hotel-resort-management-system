using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>Severity levels for a dashboard alert - never conveyed by color alone, always
/// paired with this label/icon so the alert stays meaningful without relying on color
/// perception.</summary>
public enum AlertSeverity
{
    Info,
    Success,
    Warning,
    Critical
}

/// <summary>One actionable item surfaced on the dashboard/notification bell - always links
/// to the real record it concerns (Section 11: "clickable... do not duplicate functionality
/// already available in the main forms").</summary>
public class DashboardAlert
{
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public object? RouteValues { get; set; }
    public string? Area { get; set; }
}

/// <summary>One point in the Income vs Expense trend, at whatever granularity was
/// requested (day/week/month/year).</summary>
public class TrendPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
}

public enum TrendPeriod { Day, Week, Month, Year }

/// <summary>One row of the Dashboard's Recent Activity feed - a thin projection of an
/// existing AuditLog row (Section 46/56), never a separate/duplicate activity-tracking
/// mechanism. Read-only: this feed exists to surface what already gets logged, not to
/// log anything new itself.</summary>
public class RecentActivityItem
{
    public string? UserName { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public DateTime ActualDateTime { get; set; }
}

/// <summary>
/// Section 11: everything the Dashboard (and the header notification bell, which shares
/// the Alerts list) needs in one place, computed from the same live data every other
/// module already reads/writes - no statistic here is hard-coded or duplicated logic.
/// </summary>
public class DashboardSnapshot
{
    public DateOnly BusinessDate { get; set; }
    public string BusinessDateStatus { get; set; } = string.Empty;

    // Room status (Section 4/11/13)
    public int TotalRooms { get; set; }
    public Dictionary<RoomStatus, int> RoomsByStatus { get; set; } = new();
    public decimal OccupancyPercent { get; set; }

    // Guest status
    public int CurrentGuestsCount { get; set; }
    public List<Reservation> ArrivalsToday { get; set; } = new();
    public List<Reservation> DeparturesToday { get; set; } = new();
    public int PendingCheckInsCount { get; set; }

    // Financial (today, business-date scoped)
    public decimal TodayRevenue { get; set; }
    public decimal TodayExpenses { get; set; }
    public decimal TodayNetIncome => TodayRevenue - TodayExpenses;
    public decimal OutstandingBalance { get; set; }

    // Reservation overview (all active + recent)
    public Dictionary<ReservationStatus, int> ReservationsByStatus { get; set; } = new();

    // Amenities (Section 10/14)
    public int TotalAmenities { get; set; }
    public Dictionary<AmenityStatus, int> AmenitiesByStatus { get; set; } = new();

    // Pending operational tasks
    public int PendingHousekeepingCount { get; set; }
    public int PendingMaintenanceCount { get; set; }
    public int PendingReceivablesCount { get; set; }

    public List<TrendPoint> IncomeExpenseTrend { get; set; } = new();
    public TrendPeriod TrendPeriodUsed { get; set; }

    public List<DashboardAlert> Alerts { get; set; } = new();

    // Recent Activity (Section 11) - the latest meaningful actions across the system,
    // reusing the existing Audit Trail rather than a new/duplicate activity log.
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
}

public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(TrendPeriod trendPeriod = TrendPeriod.Day);

    /// <summary>Just the Alerts list - used by the header notification bell so it doesn't
    /// have to pull (and the page doesn't have to render) the full dashboard snapshot.</summary>
    Task<List<DashboardAlert>> GetAlertsAsync();
}
