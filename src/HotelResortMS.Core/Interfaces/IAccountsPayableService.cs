using HotelResortMS.Core.Entities;

namespace HotelResortMS.Core.Interfaces;

/// <summary>Section 37: tracks and settles what the hotel owes suppliers. Supports
/// partial and full payments against one AccountsPayable balance.</summary>
public interface IAccountsPayableService
{
    Task RecordSupplierPaymentAsync(int accountsPayableId, decimal amount, string paidBy, string? reference = null);
}
