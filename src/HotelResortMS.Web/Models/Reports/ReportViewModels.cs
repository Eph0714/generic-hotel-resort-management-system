using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Models.Reports;

/// <summary>Common date-range filter every report accepts (Section 44: "Reports must
/// support ... Date Range"). Defaults to the current month if nothing is supplied.</summary>
public class ReportDateRange
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    public static ReportDateRange Resolve(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new ReportDateRange
        {
            From = from ?? new DateOnly(today.Year, today.Month, 1),
            To = to ?? today
        };
    }
}

public class HotelReportViewModel
{
    public ReportDateRange Range { get; set; } = new();

    // Occupancy / Room Status (Section 44 "Hotel" reports)
    public int TotalRooms { get; set; }
    public Dictionary<RoomStatus, int> RoomsByStatus { get; set; } = new();

    // Room Revenue
    public decimal RoomRevenueGross { get; set; }
    public decimal RoomRevenueDiscount { get; set; }
    public decimal RoomRevenueNet { get; set; }

    // Check-In / Check-Out activity in range
    public List<CheckIn> CheckIns { get; set; } = new();
    public List<CheckOut> CheckOuts { get; set; } = new();
}

public class ReservationReportViewModel
{
    public ReportDateRange Range { get; set; } = new();
    public List<Reservation> Reservations { get; set; } = new();
    public List<Reservation> Upcoming { get; set; } = new();
    public List<Reservation> Cancelled { get; set; } = new();
    public List<Reservation> NoShows { get; set; } = new();
    public decimal ReservationRevenue { get; set; }
}

public class AmenityReportViewModel
{
    public ReportDateRange Range { get; set; } = new();
    public Dictionary<AmenityStatus, int> AmenitiesByStatus { get; set; } = new();
    public List<ReservationAmenity> Usage { get; set; } = new();
    public decimal AmenityRevenue { get; set; }
}

public class POSReportViewModel
{
    public ReportDateRange Range { get; set; } = new();
    public List<POSTransaction> Sales { get; set; } = new();

    public decimal GrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal NetSales { get; set; }
    public int VoidCount { get; set; }
    public int RefundCount { get; set; }

    public List<(string ProductName, int Quantity, decimal Total)> ByProduct { get; set; } = new();
    public List<(string CategoryName, decimal Total)> ByCategory { get; set; } = new();
    public List<(string User, decimal Total)> ByUser { get; set; } = new();
    public List<(string Method, decimal Total)> ByPaymentMethod { get; set; } = new();
}

public class InventoryReportViewModel
{
    public List<InventoryItem> CurrentStock { get; set; } = new();
    public List<InventoryItem> LowStock { get; set; } = new();

    public ReportDateRange Range { get; set; } = new();
    public List<InventoryTransaction> StockIn { get; set; } = new();
    public List<InventoryTransaction> StockOut { get; set; } = new();
    public List<InventoryTransaction> Waste { get; set; } = new();
    public List<PurchaseOrder> Purchases { get; set; } = new();

    public decimal TotalValuation { get; set; }
}

public class FinancialReportViewModel
{
    public ReportDateRange Range { get; set; } = new();

    public decimal GrossRevenue { get; set; }
    public decimal Discounts { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal ProfitLoss { get; set; }

    public decimal CashReceipts { get; set; }
    public decimal CashPaidOut { get; set; }
    public decimal NetCashFlow { get; set; }

    public decimal AccountsReceivableOutstanding { get; set; }
    public decimal AccountsPayableOutstanding { get; set; }

    public decimal BeginningCash { get; set; }
    public decimal? EndingCash { get; set; }
    public decimal? CashVariance { get; set; }

    public List<Income> IncomeByCategory { get; set; } = new();
    public List<Expense> ExpensesByCategory { get; set; } = new();
}

public class DiscountReportViewModel
{
    public ReportDateRange Range { get; set; } = new();
    public List<DiscountTransaction> Transactions { get; set; } = new();

    /// <summary>Sum of EligibleAmount - the portion of each transaction this discount was
    /// allowed to apply to (Section 17/18), not the full sale total.</summary>
    public decimal TotalEligible { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalNetOfEligible => TotalEligible - TotalDiscount;

    public List<(string DiscountName, decimal Total)> ByDiscount { get; set; } = new();
    public List<(string User, decimal Total)> ByUser { get; set; } = new();
    public int ManualOverrideCount { get; set; }
}
