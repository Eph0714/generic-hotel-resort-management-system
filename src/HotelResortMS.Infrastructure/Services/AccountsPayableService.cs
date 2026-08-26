using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IAccountsPayableService"/>
public class AccountsPayableService : IAccountsPayableService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public AccountsPayableService(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task RecordSupplierPaymentAsync(int accountsPayableId, decimal amount, string paidBy, string? reference = null)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Payment amount must be greater than zero.");
        }

        var payable = await _db.AccountsPayables.FindAsync(accountsPayableId)
            ?? throw new InvalidOperationException("Accounts payable record not found.");

        if (payable.Status == AccountsPayableStatus.Paid)
        {
            throw new InvalidOperationException("This payable is already fully paid.");
        }
        if (amount > payable.Balance)
        {
            throw new ArgumentException($"Payment of {amount:N2} exceeds the remaining balance of {payable.Balance:N2}.");
        }

        payable.AmountPaid += amount;
        payable.Balance -= amount;
        payable.Status = payable.Balance == 0 ? AccountsPayableStatus.Paid : AccountsPayableStatus.PartiallyPaid;
        payable.UpdatedAt = DateTime.UtcNow;
        payable.UpdatedBy = paidBy;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.AccountsPayable, "Payment", payable.Id.ToString(),
            newValues: new { amount, payable.Balance, reference });
    }
}
