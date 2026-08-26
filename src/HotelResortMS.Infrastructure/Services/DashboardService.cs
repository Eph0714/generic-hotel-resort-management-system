using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IDashboardService"/>
public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDateService _businessDateService;

    public DashboardService(ApplicationDbContext db, IBusinessDateService businessDateService)
    {
        _db = db;
        _businessDateService = businessDateService;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(TrendPeriod trendPeriod = TrendPeriod.Day)
    {
        var businessDate = await _businessDateService.GetCurrentAsync();
        var today = businessDate.Date;

        var snapshot = new DashboardSnapshot
        {
            BusinessDate = today,
            BusinessDateStatus = businessDate.Status.ToString()
        };

        // --- Rooms ---
        var rooms = await _db.Rooms.Where(r => r.IsActive).ToListAsync();
        snapshot.TotalRooms = rooms.Count;
        snapshot.RoomsByStatus = rooms.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
        var occupied = snapshot.RoomsByStatus.GetValueOrDefault(RoomStatus.Occupied);
        snapshot.OccupancyPercent = snapshot.TotalRooms == 0 ? 0 : Math.Round(occupied * 100m / snapshot.TotalRooms, 1);

        // --- Guests ---
        snapshot.CurrentGuestsCount = await _db.Reservations.CountAsync(r => r.Status == ReservationStatus.CheckedIn);

        snapshot.ArrivalsToday = await _db.Reservations.Include(r => r.Guest)
            .Where(r => r.CheckInDate == today && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .OrderBy(r => r.ReservationDate)
            .ToListAsync();

        snapshot.DeparturesToday = await _db.Reservations.Include(r => r.Guest)
            .Where(r => r.CheckOutDate == today && r.Status == ReservationStatus.CheckedIn)
            .OrderBy(r => r.CheckOutDate)
            .ToListAsync();

        // Overdue arrivals: should have checked in by now but haven't (a live version of
        // what Night Audit would otherwise flag as a No-Show candidate).
        snapshot.PendingCheckInsCount = await _db.Reservations.CountAsync(r =>
            (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed) && r.CheckInDate <= today);

        // --- Financial (today) ---
        snapshot.TodayRevenue = await _db.Incomes
            .Where(i => i.BusinessDate == today && i.Status == IncomeStatus.Posted)
            .SumAsync(i => (decimal?)i.NetAmount) ?? 0m;
        snapshot.TodayExpenses = await _db.Expenses
            .Where(x => x.BusinessDate == today && x.Status == ExpenseStatus.Posted)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        snapshot.OutstandingBalance = await _db.AccountsReceivables
            .Where(a => a.Status != AccountsReceivableStatus.Paid)
            .SumAsync(a => (decimal?)a.Balance) ?? 0m;

        // --- Reservations overview (active/recent, not just today) ---
        snapshot.ReservationsByStatus = (await _db.Reservations
                .Where(r => r.ReservationDate >= today.AddDays(-30).ToDateTime(TimeOnly.MinValue))
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync())
            .ToDictionary(x => x.Status, x => x.Count);

        // --- Amenities ---
        var amenities = await _db.Amenities.Where(a => a.IsActive).ToListAsync();
        snapshot.TotalAmenities = amenities.Count;
        snapshot.AmenitiesByStatus = amenities.GroupBy(a => a.Status).ToDictionary(g => g.Key, g => g.Count());

        // --- Pending operational tasks ---
        snapshot.PendingHousekeepingCount = await _db.HousekeepingTasks.CountAsync(t => t.Status != HousekeepingStatus.Ready);
        snapshot.PendingMaintenanceCount = await _db.MaintenanceRequests.CountAsync(m =>
            m.Status != MaintenanceRequestStatus.Completed && m.Status != MaintenanceRequestStatus.Cancelled);
        snapshot.PendingReceivablesCount = await _db.AccountsReceivables.CountAsync(a => a.Status != AccountsReceivableStatus.Paid);

        snapshot.TrendPeriodUsed = trendPeriod;
        snapshot.IncomeExpenseTrend = await BuildTrendAsync(today, trendPeriod);

        snapshot.Alerts = await BuildAlertsAsync(today);

        // --- Recent Activity: the latest meaningful actions from the existing Audit
        // Trail (Section 46/56) - Login/Logout excluded since they're session noise, not
        // the kind of "what just happened" activity this feed is for.
        snapshot.RecentActivity = await _db.AuditLogs
            .Where(a => a.Action != "Login" && a.Action != "Logout")
            .OrderByDescending(a => a.ActualDateTime)
            .Take(8)
            .Select(a => new RecentActivityItem
            {
                UserName = a.UserName,
                Module = a.Module,
                Action = a.Action,
                RecordId = a.RecordId,
                ActualDateTime = a.ActualDateTime
            })
            .ToListAsync();

        return snapshot;
    }

    public async Task<List<DashboardAlert>> GetAlertsAsync()
    {
        var businessDate = await _businessDateService.GetCurrentAsync();
        return await BuildAlertsAsync(businessDate.Date);
    }

    /// <summary>
    /// Section 10 (Notifications and Alerts): every item here links to the real record
    /// that caused it - clicking an alert should always land the user on the module that
    /// can actually resolve it, never a dead end. Shared by the full Dashboard page and the
    /// header notification bell so the two never disagree about what counts as an alert.
    /// </summary>
    private async Task<List<DashboardAlert>> BuildAlertsAsync(DateOnly today)
    {
        var alerts = new List<DashboardAlert>();

        var overdueArrivals = await _db.Reservations.CountAsync(r =>
            (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed) && r.CheckInDate <= today);
        if (overdueArrivals > 0)
        {
            alerts.Add(new DashboardAlert
            {
                Severity = AlertSeverity.Warning,
                Message = $"{overdueArrivals} reservation(s) awaiting check-in.",
                Controller = "Reservations", Action = "Index", RouteValues = new { status = "Confirmed" }
            });
        }

        var lateCheckouts = await _db.Reservations.CountAsync(r => r.Status == ReservationStatus.CheckedIn && r.CheckOutDate < today);
        if (lateCheckouts > 0)
        {
            alerts.Add(new DashboardAlert
            {
                Severity = AlertSeverity.Critical,
                Message = $"{lateCheckouts} guest(s) past their check-out date.",
                Controller = "Reservations", Action = "Index", RouteValues = new { status = "CheckedIn" }
            });
        }

        var unpaidReceivables = await _db.AccountsReceivables.CountAsync(a => a.Status != AccountsReceivableStatus.Paid);
        if (unpaidReceivables > 0)
        {
            alerts.Add(new DashboardAlert
            {
                Severity = AlertSeverity.Warning,
                Message = $"{unpaidReceivables} outstanding guest balance(s).",
                Controller = "AccountsReceivable", Action = "Index"
            });
        }

        var dirtyRooms = await _db.Rooms.CountAsync(r => r.IsActive && r.Status == RoomStatus.Dirty);
        if (dirtyRooms > 0)
        {
            alerts.Add(new DashboardAlert
            {
                Severity = AlertSeverity.Info,
                Message = $"{dirtyRooms} room(s) need cleaning.",
                Controller = "Housekeeping", Action = "Index"
            });
        }

        var openMaintenance = await _db.MaintenanceRequests.CountAsync(m =>
            m.Status != MaintenanceRequestStatus.Completed && m.Status != MaintenanceRequestStatus.Cancelled);
        if (openMaintenance > 0)
        {
            alerts.Add(new DashboardAlert
            {
                Severity = AlertSeverity.Warning,
                Message = $"{openMaintenance} open maintenance request(s).",
                Controller = "MaintenanceRequests", Action = "Index"
            });
        }

        var lowStock = await _db.InventoryItems.CountAsync(i => i.IsActive && i.CurrentStock <= i.ReorderLevel);
        if (lowStock > 0)
        {
            alerts.Add(new DashboardAlert
            {
                Severity = AlertSeverity.Warning,
                Message = $"{lowStock} inventory item(s) at or below reorder level.",
                Controller = "InventoryItems", Action = "Index", RouteValues = new { lowStockOnly = true }
            });
        }

        var pendingBackupDays = await _db.BackupRecords.AnyAsync(b => b.Status == BackupStatus.Success);
        if (!pendingBackupDays)
        {
            alerts.Add(new DashboardAlert
            {
                Severity = AlertSeverity.Info,
                Message = "No successful backup has been recorded yet.",
                Controller = "Backup", Action = "Index"
            });
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new DashboardAlert { Severity = AlertSeverity.Success, Message = "No pending items - everything is up to date.", Controller = "Dashboard", Action = "Index" });
        }

        return alerts;
    }

    /// <summary>Buckets Income/Expense into the requested granularity for the trend chart -
    /// last 7 days, last 8 weeks, last 12 months, or last 5 years, ending at today.</summary>
    private async Task<List<TrendPoint>> BuildTrendAsync(DateOnly today, TrendPeriod period)
    {
        var points = new List<TrendPoint>();

        (DateOnly from, DateOnly to, string label) BucketFor(int offset) => period switch
        {
            TrendPeriod.Day => (today.AddDays(-offset), today.AddDays(-offset), today.AddDays(-offset).ToString("MMM d")),
            TrendPeriod.Week => (today.AddDays(-7 * (offset + 1) + 1), today.AddDays(-7 * offset), $"Wk {today.AddDays(-7 * offset):MMM d}"),
            TrendPeriod.Month => (new DateOnly(today.AddMonths(-offset).Year, today.AddMonths(-offset).Month, 1),
                                   new DateOnly(today.AddMonths(-offset).Year, today.AddMonths(-offset).Month, DateTime.DaysInMonth(today.AddMonths(-offset).Year, today.AddMonths(-offset).Month)),
                                   today.AddMonths(-offset).ToString("MMM yyyy")),
            TrendPeriod.Year => (new DateOnly(today.Year - offset, 1, 1), new DateOnly(today.Year - offset, 12, 31), (today.Year - offset).ToString()),
            _ => (today, today, today.ToString())
        };

        var bucketCount = period switch { TrendPeriod.Day => 7, TrendPeriod.Week => 8, TrendPeriod.Month => 12, TrendPeriod.Year => 5, _ => 7 };

        for (var i = bucketCount - 1; i >= 0; i--)
        {
            var (from, to, label) = BucketFor(i);
            var income = await _db.Incomes.Where(x => x.Status == IncomeStatus.Posted && x.BusinessDate >= from && x.BusinessDate <= to).SumAsync(x => (decimal?)x.NetAmount) ?? 0m;
            var expense = await _db.Expenses.Where(x => x.Status == ExpenseStatus.Posted && x.BusinessDate >= from && x.BusinessDate <= to).SumAsync(x => (decimal?)x.Amount) ?? 0m;
            points.Add(new TrendPoint { Label = label, Income = income, Expense = expense });
        }

        return points;
    }
}
