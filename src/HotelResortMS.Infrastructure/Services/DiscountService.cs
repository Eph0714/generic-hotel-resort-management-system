using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IDiscountService"/>
public class DiscountService : IDiscountService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDateService _businessDateService;

    public DiscountService(ApplicationDbContext db, IBusinessDateService businessDateService)
    {
        _db = db;
        _businessDateService = businessDateService;
    }

    public async Task<DiscountCalculationResult> CalculateAsync(
        decimal grossAmount,
        decimal eligibleAmount,
        int? discountId,
        string appliedBy,
        string referenceType,
        string referenceId,
        bool isManualOverride = false,
        string? overrideReason = null,
        string? authorizedBy = null,
        bool recordTransaction = true,
        int? guestId = null)
    {
        // Section 18: never allow negative totals in - a caller passing a bad amount is a
        // bug upstream, and this is the one place that would otherwise silently propagate it.
        if (grossAmount < 0 || eligibleAmount < 0)
        {
            throw new ArgumentException("Gross amount and eligible amount cannot be negative.");
        }
        if (eligibleAmount > grossAmount)
        {
            throw new ArgumentException("Eligible amount cannot exceed the gross amount.");
        }

        decimal discountAmount = 0m;
        Discount? discount = null;

        if (discountId is not null)
        {
            discount = await _db.Discounts.FindAsync(discountId.Value)
                ?? throw new InvalidOperationException("Discount not found.");

            if (!isManualOverride)
            {
                // Section 18: an ordinary (non-override) discount must be active and within
                // its configured effective window - expired/inactive discounts silently
                // computing a number would be worse than refusing outright.
                var today = (await _businessDateService.GetCurrentAsync()).Date;
                if (!discount.IsActive || discount.EffectiveDate > today ||
                    (discount.ExpirationDate is not null && discount.ExpirationDate < today))
                {
                    throw new InvalidOperationException($"Discount '{discount.Name}' is not currently active.");
                }
            }
            else if (string.IsNullOrWhiteSpace(overrideReason) || string.IsNullOrWhiteSpace(authorizedBy))
            {
                // Section 18: "Manual discount overrides require: Authorization, Reason,
                // Audit Trail" - refuse rather than silently apply without either.
                throw new InvalidOperationException("A manual discount override requires both a reason and an authorizing user.");
            }

            // Section 17/58: a Senior Citizen/PWD (or any RequiresIdVerification) discount
            // can never be applied to an unverified guest outside of an authorized manual
            // override - this is the actual eligibility check the law requires, not just a
            // percentage lookup.
            if (discount.RequiresIdVerification && !isManualOverride)
            {
                if (guestId is null)
                {
                    throw new InvalidOperationException($"Discount '{discount.Name}' requires a verified guest - no guest was specified for this transaction.");
                }

                var guest = await _db.Guests.FindAsync(guestId.Value)
                    ?? throw new InvalidOperationException("Guest not found.");

                var verified = discount.Type switch
                {
                    DiscountType.SeniorCitizen => guest.IsSeniorCitizen && !string.IsNullOrWhiteSpace(guest.SeniorCitizenIdNumber),
                    DiscountType.PWD => guest.IsPwd && !string.IsNullOrWhiteSpace(guest.PwdIdNumber),
                    _ => true
                };

                if (!verified)
                {
                    throw new InvalidOperationException(
                        $"Guest is not verified as eligible for '{discount.Name}' (no ID on file). Use a manual override with authorization if this is a genuine exception.");
                }
            }

            // Section 18: "Prevent duplicate discounts" - a given transaction (POS sale,
            // reservation, event) can only have one discount applied against it, ever.
            var alreadyApplied = await _db.DiscountTransactions
                .AnyAsync(t => t.ReferenceType == referenceType && t.ReferenceId == referenceId);
            if (alreadyApplied && recordTransaction)
            {
                throw new InvalidOperationException($"A discount has already been applied to this {referenceType} ({referenceId}).");
            }

            discountAmount = discount.CalculationType == DiscountCalculationType.Percentage
                ? Math.Round(eligibleAmount * discount.Percentage / 100m, 2)
                : discount.FixedAmount;

            // Section 18: "Prevent discount greater than eligible amount" - clamp rather
            // than let a misconfigured fixed-amount discount push the total negative.
            discountAmount = Math.Min(discountAmount, eligibleAmount);
        }

        var taxableAmount = grossAmount - discountAmount;

        var vatRate = await GetSettingDecimalAsync("Finance.VatRate", 12m);
        var serviceChargeRate = await GetSettingDecimalAsync("Finance.ServiceChargeRate", 0m);

        var taxAmount = Math.Round(taxableAmount * vatRate / 100m, 2);
        var serviceChargeAmount = Math.Round(taxableAmount * serviceChargeRate / 100m, 2);
        var netAmount = taxableAmount + taxAmount + serviceChargeAmount;

        if (recordTransaction && discount is not null)
        {
            var businessDate = await _businessDateService.GetCurrentAsync();
            _db.DiscountTransactions.Add(new DiscountTransaction
            {
                DiscountId = discount.Id,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                EligibleAmount = eligibleAmount,
                DiscountAmount = discountAmount,
                IsManualOverride = isManualOverride,
                OverrideReason = overrideReason,
                AuthorizedBy = authorizedBy,
                AppliedBy = appliedBy,
                ActualDateTime = DateTime.UtcNow,
                BusinessDate = businessDate.Date
            });
            await _db.SaveChangesAsync();
        }

        return new DiscountCalculationResult
        {
            GrossAmount = grossAmount,
            EligibleAmount = eligibleAmount,
            DiscountAmount = discountAmount,
            TaxableAmount = taxableAmount,
            TaxAmount = taxAmount,
            ServiceChargeAmount = serviceChargeAmount,
            NetAmount = netAmount
        };
    }

    private async Task<decimal> GetSettingDecimalAsync(string key, decimal fallback)
    {
        var raw = await _db.SystemSettings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();
        return decimal.TryParse(raw, out var value) ? value : fallback;
    }
}
