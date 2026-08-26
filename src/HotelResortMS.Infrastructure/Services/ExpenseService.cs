using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IExpenseService"/>
public class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IBusinessDateService _businessDateService;
    private readonly IAuditService _auditService;

    public ExpenseService(ApplicationDbContext db, INumberingService numberingService, IBusinessDateService businessDateService, IAuditService auditService)
    {
        _db = db;
        _numberingService = numberingService;
        _businessDateService = businessDateService;
        _auditService = auditService;
    }

    public async Task<Expense> RecordExpenseAsync(
        ExpenseCategory category, string description, decimal amount, PaymentMethod paymentMethod,
        string? payee, string? reference, string recordedBy, string? remarks = null)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Expense amount must be greater than zero.");
        }

        var businessDate = await _businessDateService.GetCurrentForPostingAsync();

        var expense = new Expense
        {
            ExpenseNumber = await _numberingService.GenerateAsync("Expense"),
            Category = category,
            Description = description,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Payee = payee,
            Reference = reference,
            Remarks = remarks,
            Status = ExpenseStatus.Posted,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            RecordedBy = recordedBy
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Expenses, "Create", expense.Id.ToString(), newValues: new { expense.ExpenseNumber, expense.Amount });
        return expense;
    }

    public async Task VoidExpenseAsync(int expenseId, string voidedBy, string reason)
    {
        var expense = await _db.Expenses.FindAsync(expenseId)
            ?? throw new InvalidOperationException("Expense not found.");

        if (expense.Status != ExpenseStatus.Posted)
        {
            throw new InvalidOperationException("This expense is already voided.");
        }

        expense.Status = ExpenseStatus.Voided;
        expense.VoidedAt = DateTime.UtcNow;
        expense.VoidedBy = voidedBy;
        expense.VoidReason = reason;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Expenses, "Void", expenseId.ToString(), reason: reason);
    }
}
