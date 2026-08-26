namespace HotelResortMS.Core.Entities;

public enum ExpenseCategory
{
    Utilities,
    Salaries,
    Supplies,
    Maintenance,
    Purchases,
    Transportation,
    Repairs,
    OfficeExpenses,
    Other
}

public enum ExpenseStatus
{
    Posted,
    Voided
}

/// <summary>
/// Section 39: a cash/business outlay. Once Posted, an expense is never hard-deleted
/// (Section 10) - only Voided with a reason, which is what a Daily Closing's cash formula
/// needs to keep reconciling against the audit trail.
/// </summary>
public class Expense
{
    public int Id { get; set; }

    public string ExpenseNumber { get; set; } = string.Empty;

    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public string? Payee { get; set; }
    public string? Reference { get; set; }
    public string? AttachmentPath { get; set; }
    public string? Remarks { get; set; }

    public ExpenseStatus Status { get; set; } = ExpenseStatus.Posted;

    public DateTime ActualDateTime { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string RecordedBy { get; set; } = string.Empty;

    public DateTime? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}
