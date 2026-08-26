using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IAccountsReceivableService"/>
public class AccountsReceivableService : IAccountsReceivableService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public AccountsReceivableService(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<AccountsReceivable> CreateAsync(int guestId, int? reservationId, int? guestFolioId, decimal amount, DateOnly? dueDate)
    {
        var ar = new AccountsReceivable
        {
            GuestId = guestId,
            ReservationId = reservationId,
            GuestFolioId = guestFolioId,
            Amount = amount,
            AmountPaid = 0,
            Balance = amount,
            DueDate = dueDate,
            Status = AccountsReceivableStatus.Open
        };

        _db.AccountsReceivables.Add(ar);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.AccountsReceivable, "Create", ar.Id.ToString(), newValues: new { guestId, amount });
        return ar;
    }

    public async Task RecordGuestPaymentAsync(int accountsReceivableId, decimal amount, string paidBy, string? reference = null)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Payment amount must be greater than zero.");
        }

        var ar = await _db.AccountsReceivables.FindAsync(accountsReceivableId)
            ?? throw new InvalidOperationException("Accounts receivable record not found.");

        if (ar.Status == AccountsReceivableStatus.Paid)
        {
            throw new InvalidOperationException("This receivable is already fully paid.");
        }
        if (amount > ar.Balance)
        {
            throw new ArgumentException($"Payment of {amount:N2} exceeds the remaining balance of {ar.Balance:N2}.");
        }

        ar.AmountPaid += amount;
        ar.Balance -= amount;
        ar.Status = ar.Balance == 0 ? AccountsReceivableStatus.Paid : AccountsReceivableStatus.PartiallyPaid;
        ar.UpdatedAt = DateTime.UtcNow;
        ar.UpdatedBy = paidBy;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.AccountsReceivable, "Payment", ar.Id.ToString(), newValues: new { amount, ar.Balance, reference });
    }
}
