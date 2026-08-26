using System.Text;
using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Reports;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>
/// Section 44: reporting across every module. Each action supports a date-range filter
/// (Section 44: "Reports must support ... Date Range") and an Export=csv query flag for a
/// spreadsheet-friendly download; the browser's own Print (Ctrl+P / a Print button)
/// covers "Print"/"PDF" without a heavyweight PDF-generation dependency - a pragmatic
/// reading of "Print, PDF, Excel" rather than a fragile bespoke PDF renderer.
/// </summary>
[RequirePermission(SystemModules.Reports, PermissionAction.View)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDateService _businessDateService;

    public ReportsController(ApplicationDbContext db, IBusinessDateService businessDateService)
    {
        _db = db;
        _businessDateService = businessDateService;
    }

    public IActionResult Index() => View();

    // ---------------------------------------------------------------- Hotel ----

    public async Task<IActionResult> Hotel(DateOnly? from, DateOnly? to)
    {
        var range = ReportDateRange.Resolve(from, to);
        var model = new HotelReportViewModel { Range = range };

        var rooms = await _db.Rooms.Where(r => r.IsActive).ToListAsync();
        model.TotalRooms = rooms.Count;
        model.RoomsByStatus = rooms.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());

        var roomIncome = await _db.Incomes
            .Where(i => i.Category == IncomeCategory.RoomRevenue && i.Status == IncomeStatus.Posted
                        && i.BusinessDate >= range.From && i.BusinessDate <= range.To)
            .ToListAsync();
        model.RoomRevenueGross = roomIncome.Sum(i => i.GrossAmount);
        model.RoomRevenueDiscount = roomIncome.Sum(i => i.DiscountAmount);
        model.RoomRevenueNet = roomIncome.Sum(i => i.NetAmount);

        model.CheckIns = await _db.CheckIns.Where(c => c.BusinessDate >= range.From && c.BusinessDate <= range.To)
            .OrderByDescending(c => c.ActualDateTime).ToListAsync();
        model.CheckOuts = await _db.CheckOuts.Where(c => c.BusinessDate >= range.From && c.BusinessDate <= range.To)
            .OrderByDescending(c => c.ActualDateTime).ToListAsync();

        if (Request.Query["export"] == "csv")
        {
            var rows = model.CheckIns.Select(c => new[] { "CheckIn", c.ActualDateTime.ToString("yyyy-MM-dd HH:mm"), c.ReservationId.ToString(), c.VerifiedBy })
                .Concat(model.CheckOuts.Select(c => new[] { "CheckOut", c.ActualDateTime.ToString("yyyy-MM-dd HH:mm"), c.ReservationId.ToString(), c.ProcessedBy }));
            return Csv("hotel-report", new[] { "Type", "DateTime", "ReservationId", "By" }, rows);
        }

        return View(model);
    }

    // ------------------------------------------------------------ Reservations ----

    public async Task<IActionResult> Reservations(DateOnly? from, DateOnly? to)
    {
        var range = ReportDateRange.Resolve(from, to);
        var model = new ReservationReportViewModel { Range = range };

        var query = _db.Reservations.Include(r => r.Guest)
            .Where(r => r.ReservationDate >= range.From.ToDateTime(TimeOnly.MinValue) && r.ReservationDate <= range.To.ToDateTime(TimeOnly.MaxValue));

        model.Reservations = await query.OrderByDescending(r => r.ReservationDate).ToListAsync();
        model.Cancelled = model.Reservations.Where(r => r.Status == ReservationStatus.Cancelled).ToList();
        model.NoShows = model.Reservations.Where(r => r.Status == ReservationStatus.NoShow).ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        model.Upcoming = await _db.Reservations.Include(r => r.Guest)
            .Where(r => r.CheckInDate >= today && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .OrderBy(r => r.CheckInDate).ToListAsync();

        model.ReservationRevenue = model.Reservations
            .Where(r => r.Status != ReservationStatus.Cancelled)
            .Sum(r => r.TotalAmount - r.DiscountAmount);

        if (Request.Query["export"] == "csv")
        {
            var rows = model.Reservations.Select(r => new[]
            {
                r.ReservationNumber, r.Guest?.LastName + ", " + r.Guest?.FirstName, r.CheckInDate.ToString("yyyy-MM-dd"),
                r.CheckOutDate.ToString("yyyy-MM-dd"), r.Status.ToString(), r.TotalAmount.ToString("F2")
            });
            return Csv("reservations-report", new[] { "ReservationNumber", "Guest", "CheckIn", "CheckOut", "Status", "Total" }, rows);
        }

        return View(model);
    }

    // -------------------------------------------------------------- Amenities ----

    public async Task<IActionResult> Amenities(DateOnly? from, DateOnly? to)
    {
        var range = ReportDateRange.Resolve(from, to);
        var model = new AmenityReportViewModel { Range = range };

        var amenities = await _db.Amenities.Where(a => a.IsActive).ToListAsync();
        model.AmenitiesByStatus = amenities.GroupBy(a => a.Status).ToDictionary(g => g.Key, g => g.Count());

        model.Usage = await _db.ReservationAmenities
            .Include(ra => ra.Amenity)
            .Include(ra => ra.Reservation)
            .Where(ra => DateOnly.FromDateTime(ra.StartDateTime) >= range.From && DateOnly.FromDateTime(ra.StartDateTime) <= range.To)
            .OrderByDescending(ra => ra.StartDateTime)
            .ToListAsync();

        model.AmenityRevenue = await _db.Incomes
            .Where(i => i.Category == IncomeCategory.AmenityRevenue && i.Status == IncomeStatus.Posted
                        && i.BusinessDate >= range.From && i.BusinessDate <= range.To)
            .SumAsync(i => i.NetAmount);

        return View(model);
    }

    // -------------------------------------------------------------------- POS ----

    public async Task<IActionResult> POS(DateOnly? from, DateOnly? to)
    {
        var range = ReportDateRange.Resolve(from, to);
        var model = new POSReportViewModel { Range = range };

        var sales = await _db.POSTransactions
            .Include(s => s.Details).ThenInclude(d => d.Product).ThenInclude(p => p!.ProductCategory)
            .Where(s => s.BusinessDate >= range.From && s.BusinessDate <= range.To)
            .ToListAsync();
        model.Sales = sales.OrderByDescending(s => s.ActualDateTime).ToList();

        var completed = sales.Where(s => s.Status == POSTransactionStatus.Completed).ToList();
        model.GrossSales = completed.Sum(s => s.GrossAmount);
        model.TotalDiscounts = completed.Sum(s => s.DiscountAmount);
        model.NetSales = completed.Sum(s => s.NetAmount);
        model.VoidCount = sales.Count(s => s.Status == POSTransactionStatus.Voided);
        model.RefundCount = sales.Count(s => s.Status == POSTransactionStatus.Refunded);

        model.ByProduct = completed.SelectMany(s => s.Details)
            .GroupBy(d => d.ProductName)
            .Select(g => (g.Key, g.Sum(d => d.Quantity), g.Sum(d => d.LineTotal)))
            .OrderByDescending(x => x.Item3).ToList();

        model.ByCategory = completed.SelectMany(s => s.Details)
            .GroupBy(d => d.Product?.ProductCategory?.Name ?? "Uncategorized")
            .Select(g => (g.Key, g.Sum(d => d.LineTotal)))
            .OrderByDescending(x => x.Item2).ToList();

        model.ByUser = completed.GroupBy(s => s.ProcessedBy)
            .Select(g => (g.Key, g.Sum(s => s.NetAmount)))
            .OrderByDescending(x => x.Item2).ToList();

        var payments = await _db.Payments
            .Where(p => p.BusinessDate >= range.From && p.BusinessDate <= range.To && p.Status == PaymentStatus.Completed && p.PosTransactionId != null)
            .ToListAsync();
        model.ByPaymentMethod = payments.GroupBy(p => p.Method.ToString())
            .Select(g => (g.Key, g.Sum(p => p.Amount)))
            .OrderByDescending(x => x.Item2).ToList();

        if (Request.Query["export"] == "csv")
        {
            var rows = model.Sales.Select(s => new[] { s.PosNumber, s.ActualDateTime.ToString("yyyy-MM-dd HH:mm"), s.ProcessedBy, s.Status.ToString(), s.NetAmount.ToString("F2") });
            return Csv("pos-report", new[] { "PosNumber", "DateTime", "ProcessedBy", "Status", "NetAmount" }, rows);
        }

        return View(model);
    }

    // -------------------------------------------------------------- Inventory ----

    public async Task<IActionResult> Inventory(DateOnly? from, DateOnly? to)
    {
        var range = ReportDateRange.Resolve(from, to);
        var model = new InventoryReportViewModel { Range = range };

        model.CurrentStock = await _db.InventoryItems.Where(i => i.IsActive).Include(i => i.UnitOfMeasure).OrderBy(i => i.Name).ToListAsync();
        model.LowStock = model.CurrentStock.Where(i => i.CurrentStock <= i.ReorderLevel).ToList();
        model.TotalValuation = model.CurrentStock.Sum(i => i.CurrentStock * i.Cost);

        var transactions = await _db.InventoryTransactions
            .Include(t => t.InventoryItem)
            .Where(t => t.BusinessDate >= range.From && t.BusinessDate <= range.To)
            .ToListAsync();
        model.StockIn = transactions.Where(t => t.Type is InventoryTransactionType.StockIn or InventoryTransactionType.Purchase or InventoryTransactionType.Return).ToList();
        model.StockOut = transactions.Where(t => t.Type == InventoryTransactionType.StockOut).ToList();
        model.Waste = transactions.Where(t => t.Type == InventoryTransactionType.Waste).ToList();

        model.Purchases = await _db.PurchaseOrders.Include(p => p.Supplier)
            .Where(p => p.OrderDate >= range.From && p.OrderDate <= range.To)
            .OrderByDescending(p => p.OrderDate).ToListAsync();

        if (Request.Query["export"] == "csv")
        {
            var rows = model.CurrentStock.Select(i => new[] { i.Sku, i.Name, i.CurrentStock.ToString("F2"), i.UnitOfMeasure?.Abbreviation, (i.CurrentStock * i.Cost).ToString("F2") });
            return Csv("inventory-report", new[] { "SKU", "Name", "CurrentStock", "Unit", "Value" }, rows);
        }

        return View(model);
    }

    // -------------------------------------------------------------- Financial ----

    public async Task<IActionResult> Financial(DateOnly? from, DateOnly? to)
    {
        var range = ReportDateRange.Resolve(from, to);
        var model = new FinancialReportViewModel { Range = range };

        var incomes = await _db.Incomes.Where(i => i.BusinessDate >= range.From && i.BusinessDate <= range.To && i.Status == IncomeStatus.Posted).ToListAsync();
        model.GrossRevenue = incomes.Sum(i => i.GrossAmount);
        model.Discounts = incomes.Sum(i => i.DiscountAmount);
        model.NetRevenue = incomes.Sum(i => i.NetAmount);
        model.TotalIncome = model.NetRevenue;
        model.IncomeByCategory = incomes.ToList();

        var expenses = await _db.Expenses.Where(x => x.BusinessDate >= range.From && x.BusinessDate <= range.To && x.Status == ExpenseStatus.Posted).ToListAsync();
        model.TotalExpenses = expenses.Sum(x => x.Amount);
        model.ExpensesByCategory = expenses.ToList();

        model.ProfitLoss = model.TotalIncome - model.TotalExpenses;

        var payments = await _db.Payments.Where(p => p.BusinessDate >= range.From && p.BusinessDate <= range.To && p.Status == PaymentStatus.Completed).ToListAsync();
        model.CashReceipts = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        model.CashPaidOut = expenses.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Amount);
        model.NetCashFlow = model.CashReceipts - model.CashPaidOut;

        model.AccountsReceivableOutstanding = await _db.AccountsReceivables.Where(a => a.Status != AccountsReceivableStatus.Paid).SumAsync(a => a.Balance);
        model.AccountsPayableOutstanding = await _db.AccountsPayables.Where(a => a.Status != AccountsPayableStatus.Paid).SumAsync(a => a.Balance);

        var businessDate = await _businessDateService.GetCurrentAsync();
        model.BeginningCash = businessDate.BeginningCash;
        model.EndingCash = businessDate.EndingCash;
        model.CashVariance = businessDate.EndingCash is null ? null : businessDate.EndingCash - (businessDate.BeginningCash + model.CashReceipts - model.CashPaidOut);

        return View(model);
    }

    // --------------------------------------------------------------- Discount ----

    public async Task<IActionResult> Discounts(DateOnly? from, DateOnly? to)
    {
        var range = ReportDateRange.Resolve(from, to);
        var model = new DiscountReportViewModel { Range = range };

        model.Transactions = await _db.DiscountTransactions
            .Include(t => t.Discount)
            .Where(t => t.BusinessDate >= range.From && t.BusinessDate <= range.To)
            .OrderByDescending(t => t.ActualDateTime)
            .ToListAsync();

        model.TotalEligible = model.Transactions.Sum(t => t.EligibleAmount);
        model.TotalDiscount = model.Transactions.Sum(t => t.DiscountAmount);
        model.ManualOverrideCount = model.Transactions.Count(t => t.IsManualOverride);

        model.ByDiscount = model.Transactions.GroupBy(t => t.Discount?.Name ?? "Unknown")
            .Select(g => (g.Key, g.Sum(t => t.DiscountAmount))).OrderByDescending(x => x.Item2).ToList();

        model.ByUser = model.Transactions.GroupBy(t => t.AppliedBy)
            .Select(g => (g.Key, g.Sum(t => t.DiscountAmount))).OrderByDescending(x => x.Item2).ToList();

        if (Request.Query["export"] == "csv")
        {
            var rows = model.Transactions.Select(t => new[]
            {
                t.Discount?.Name, t.ReferenceType, t.ReferenceId, t.EligibleAmount.ToString("F2"),
                t.DiscountAmount.ToString("F2"), t.IsManualOverride.ToString(), t.AppliedBy
            });
            return Csv("discount-report", new[] { "Discount", "ReferenceType", "ReferenceId", "Eligible", "DiscountAmount", "ManualOverride", "AppliedBy" }, rows);
        }

        return View(model);
    }

    /// <summary>Renders a set of rows as a downloadable CSV - Excel opens this natively,
    /// which is the pragmatic reading of Section 44's "Excel" export requirement.</summary>
    private FileContentResult Csv(string fileName, string[] headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"{fileName}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
