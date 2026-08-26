namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 43: the finished cash-and-revenue summary for one BusinessDate, computed once
/// at closing time and kept as a permanent historical record (reopening never deletes it -
/// see BusinessDate.ReopenedAt/By/Reason for that audit trail instead).
/// Formula: ExpectedEndingCash = BeginningCash + CashReceipts + CashIn - Expenses(cash) - CashOut.
/// </summary>
public class DailyClosingRecord
{
    public int Id { get; set; }

    public int BusinessDateId { get; set; }
    public BusinessDate? BusinessDate { get; set; }

    public decimal BeginningCash { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal CashReceipts { get; set; }
    public decimal CreditSales { get; set; }
    public decimal ExpensesCash { get; set; }
    public decimal ExpensesOther { get; set; }

    public decimal ExpectedEndingCash { get; set; }
    public decimal ActualCashCount { get; set; }

    /// <summary>ActualCashCount - ExpectedEndingCash. Non-zero requires the closing user
    /// to have explained it via Remarks - the variance itself is never hidden or rounded away.</summary>
    public decimal CashVariance { get; set; }
    public string? Remarks { get; set; }

    public DateTime ClosedAt { get; set; }
    public string ClosedBy { get; set; } = string.Empty;
}
