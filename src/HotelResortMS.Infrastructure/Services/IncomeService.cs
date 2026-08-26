using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IIncomeService"/>
public class IncomeService : IIncomeService
{
    private readonly ApplicationDbContext _db;
    private readonly INumberingService _numberingService;
    private readonly IBusinessDateService _businessDateService;
    private readonly IAuditService _auditService;

    public IncomeService(ApplicationDbContext db, INumberingService numberingService, IBusinessDateService businessDateService, IAuditService auditService)
    {
        _db = db;
        _numberingService = numberingService;
        _businessDateService = businessDateService;
        _auditService = auditService;
    }

    public async Task<Income> RecordIncomeAsync(
        IncomeCategory category, string description, decimal grossAmount, decimal discountAmount,
        string referenceType, string? referenceId, string recordedBy)
    {
        var businessDate = await _businessDateService.GetCurrentAsync();

        var income = new Income
        {
            IncomeNumber = await _numberingService.GenerateAsync("Income"),
            Category = category,
            Description = description,
            GrossAmount = grossAmount,
            DiscountAmount = discountAmount,
            NetAmount = grossAmount - discountAmount,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Status = IncomeStatus.Posted,
            ActualDateTime = DateTime.UtcNow,
            BusinessDate = businessDate.Date,
            RecordedBy = recordedBy
        };

        _db.Incomes.Add(income);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Income, "Create", income.Id.ToString(), newValues: new { income.IncomeNumber, income.NetAmount });
        return income;
    }

    public async Task VoidIncomeAsync(int incomeId, string voidedBy, string reason)
    {
        var income = await _db.Incomes.FindAsync(incomeId)
            ?? throw new InvalidOperationException("Income record not found.");

        if (income.Status != IncomeStatus.Posted)
        {
            throw new InvalidOperationException("This income record is already voided.");
        }

        income.Status = IncomeStatus.Voided;
        income.VoidedAt = DateTime.UtcNow;
        income.VoidedBy = voidedBy;
        income.VoidReason = reason;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Income, "Void", incomeId.ToString(), reason: reason);
    }
}
