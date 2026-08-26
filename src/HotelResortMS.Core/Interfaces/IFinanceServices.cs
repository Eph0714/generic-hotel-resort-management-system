using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>Section 38: the single write path for revenue recognition. POSService and
/// FrontDeskService call this at the point a charge is posted, not when it is paid.</summary>
public interface IIncomeService
{
    Task<Income> RecordIncomeAsync(
        IncomeCategory category, string description, decimal grossAmount, decimal discountAmount,
        string referenceType, string? referenceId, string recordedBy);

    Task VoidIncomeAsync(int incomeId, string voidedBy, string reason);
}

/// <summary>Section 39: cash/business outlays. Posted expenses are Voided, never deleted.</summary>
public interface IExpenseService
{
    Task<Expense> RecordExpenseAsync(
        ExpenseCategory category, string description, decimal amount, PaymentMethod paymentMethod,
        string? payee, string? reference, string recordedBy, string? remarks = null);

    Task VoidExpenseAsync(int expenseId, string voidedBy, string reason);
}

/// <summary>Section 36: guest/corporate balances still owed after an authorized
/// outstanding checkout.</summary>
public interface IAccountsReceivableService
{
    Task<AccountsReceivable> CreateAsync(int guestId, int? reservationId, int? guestFolioId, decimal amount, DateOnly? dueDate);
    Task RecordGuestPaymentAsync(int accountsReceivableId, decimal amount, string paidBy, string? reference = null);
}

/// <summary>One flagged condition Night Audit found unresolved for the current business
/// date (Section 42).</summary>
public class NightAuditException
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Section 42: validates the business date is clean before it can be closed - unpaid
/// folios, un-arrived reservations still Pending/Confirmed past their check-in date
/// (candidate No-Shows), pending payments, etc. Night Audit does not itself close the day
/// (Daily Closing does); it only clears (or authorizes overriding) the exceptions that
/// would otherwise block closing.
/// </summary>
public interface INightAuditService
{
    Task<IReadOnlyList<NightAuditException>> FindExceptionsAsync();

    /// <summary>Marks Night Audit run for the current business date. If exceptions exist,
    /// both <paramref name="overrideReason"/> and Approve permission (checked by the
    /// controller) are required - Section 42: "Do not complete Night Audit while critical
    /// errors exist unless an authorized user overrides them."</summary>
    Task RunAsync(string runBy, string? overrideReason = null);
}

/// <summary>Section 43: computes and posts the Daily Closing summary, locks the business
/// date, and carries ending cash forward.</summary>
public interface IDailyClosingService
{
    Task<DailyClosingRecord> PreviewAsync();

    Task<DailyClosingRecord> CloseAsync(decimal actualCashCount, string closedBy, string? remarks = null);

    /// <summary>Section 43: "Reopening requires authorization and complete audit trail."</summary>
    Task ReopenAsync(string reopenedBy, string reason);
}
