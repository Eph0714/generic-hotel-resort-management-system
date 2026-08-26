using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IDailyClosingService"/>
public class DailyClosingService : IDailyClosingService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDateService _businessDateService;
    private readonly IAuditService _auditService;

    public DailyClosingService(ApplicationDbContext db, IBusinessDateService businessDateService, IAuditService auditService)
    {
        _db = db;
        _businessDateService = businessDateService;
        _auditService = auditService;
    }

    /// <summary>
    /// Section 43 formula: ExpectedEndingCash = BeginningCash + CashReceipts + CashIn -
    /// Expenses(cash) - CashOut. This system has no separate manual Cash In/Out entity yet
    /// (deposits/withdrawals outside of guest payments and expenses), so both are 0 for now
    /// - the formula's shape is intentionally kept exact so adding one later is a pure
    /// addition, not a rewrite.
    /// </summary>
    public async Task<DailyClosingRecord> PreviewAsync()
    {
        var businessDate = await _businessDateService.GetCurrentAsync();
        return await BuildSummaryAsync(businessDate);
    }

    private async Task<DailyClosingRecord> BuildSummaryAsync(Core.Entities.BusinessDate businessDate)
    {
        var incomes = await _db.Incomes
            .Where(i => i.BusinessDate == businessDate.Date && i.Status == IncomeStatus.Posted)
            .ToListAsync();
        var grossRevenue = incomes.Sum(i => i.GrossAmount);
        var discountAmount = incomes.Sum(i => i.DiscountAmount);
        var netRevenue = grossRevenue - discountAmount;

        var payments = await _db.Payments
            .Where(p => p.BusinessDate == businessDate.Date && p.Status == PaymentStatus.Completed)
            .ToListAsync();
        var cashReceipts = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var totalCollected = payments.Sum(p => p.Amount);

        // Section 38/40: whatever revenue was posted but not collected in any form today
        // is this day's credit/receivable sales - never confused with cash on hand.
        var creditSales = Math.Max(0, netRevenue - totalCollected);

        var expenses = await _db.Expenses
            .Where(x => x.BusinessDate == businessDate.Date && x.Status == ExpenseStatus.Posted)
            .ToListAsync();
        var expensesCash = expenses.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Amount);
        var expensesOther = expenses.Where(x => x.PaymentMethod != PaymentMethod.Cash).Sum(x => x.Amount);

        var expectedEndingCash = businessDate.BeginningCash + cashReceipts - expensesCash;

        return new DailyClosingRecord
        {
            BusinessDateId = businessDate.Id,
            BeginningCash = businessDate.BeginningCash,
            GrossRevenue = grossRevenue,
            DiscountAmount = discountAmount,
            NetRevenue = netRevenue,
            CashReceipts = cashReceipts,
            CreditSales = creditSales,
            ExpensesCash = expensesCash,
            ExpensesOther = expensesOther,
            ExpectedEndingCash = expectedEndingCash
        };
    }

    public async Task<DailyClosingRecord> CloseAsync(decimal actualCashCount, string closedBy, string? remarks = null)
    {
        var businessDate = await _businessDateService.GetCurrentAsync();

        if (businessDate.Status != Core.Entities.BusinessDate.DateStatus.NightAuditInProgress)
        {
            // Section 42/43: closing is only reachable after Night Audit has been run for
            // this business date - never skippable.
            throw new InvalidOperationException("Run Night Audit for this business date before closing it.");
        }

        var record = await BuildSummaryAsync(businessDate);
        record.ActualCashCount = actualCashCount;
        record.CashVariance = actualCashCount - record.ExpectedEndingCash;
        record.Remarks = remarks;
        record.ClosedAt = DateTime.UtcNow;
        record.ClosedBy = closedBy;

        _db.DailyClosingRecords.Add(record);

        businessDate.Status = Core.Entities.BusinessDate.DateStatus.Closed;
        businessDate.EndingCash = actualCashCount;
        businessDate.ClosedAt = DateTime.UtcNow;
        businessDate.ClosedBy = closedBy;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.DailyClosing, "Close", businessDate.Id.ToString(),
            newValues: new { record.ExpectedEndingCash, record.ActualCashCount, record.CashVariance });

        return record;
    }

    public async Task ReopenAsync(string reopenedBy, string reason)
    {
        var businessDate = await _db.BusinessDates
            .OrderByDescending(b => b.Date)
            .FirstOrDefaultAsync(b => b.Status == Core.Entities.BusinessDate.DateStatus.Closed)
            ?? throw new InvalidOperationException("No closed business date found to reopen.");

        var laterDateExists = await _db.BusinessDates.AnyAsync(b => b.Date > businessDate.Date);
        if (laterDateExists)
        {
            // Reopening a day that a later day has already opened against would let two
            // business dates be "current" at once - every service in this system assumes
            // exactly one open date.
            throw new InvalidOperationException("Cannot reopen: a later business date has already been opened.");
        }

        businessDate.Status = Core.Entities.BusinessDate.DateStatus.NightAuditInProgress;
        businessDate.ReopenedAt = DateTime.UtcNow;
        businessDate.ReopenedBy = reopenedBy;
        businessDate.ReopenReason = reason;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.DailyClosing, "Reopen", businessDate.Id.ToString(), reason: reason);
    }
}
