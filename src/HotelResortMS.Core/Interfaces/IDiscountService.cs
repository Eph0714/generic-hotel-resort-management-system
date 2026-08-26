namespace HotelResortMS.Core.Interfaces;

/// <summary>Section 18/19: the full gross-to-net breakdown for one transaction. Every
/// module that charges money (Reservations, POS, Events) builds this via DiscountService
/// rather than computing its own totals, so the math is guaranteed identical everywhere.</summary>
public class DiscountCalculationResult
{
    public decimal GrossAmount { get; set; }
    public decimal EligibleAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal NetAmount { get; set; }
}

/// <summary>
/// Section 18 (Centralized Discount Calculation): the single place discount/tax/service-
/// charge math happens. Reservations, POS, and (later) Events all call this instead of
/// hand-rolling their own percentage math, which is what keeps Senior Citizen/PWD rules
/// consistent everywhere they are honored.
/// </summary>
public interface IDiscountService
{
    /// <summary>
    /// Computes gross -> eligible -> discount -> taxable -> tax -> service charge -> net
    /// for one transaction and (unless <paramref name="recordTransaction"/> is false)
    /// writes a DiscountTransaction row for audit/reporting.
    /// </summary>
    /// <param name="grossAmount">Total charge before any discount.</param>
    /// <param name="eligibleAmount">Portion of grossAmount this discount is allowed to
    /// apply to - e.g. some products are not Senior Citizen/PWD-eligible (Section 17/18).</param>
    /// <param name="discountId">Null applies tax/service-charge only, no discount.</param>
    /// <param name="isManualOverride">True only for an administrator-authorized manual
    /// discount outside the normal eligibility rules (Section 18) - requires both
    /// <paramref name="overrideReason"/> and <paramref name="authorizedBy"/>.</param>
    /// <param name="guestId">Section 17/58: required (and checked against the guest's
    /// IsSeniorCitizen/IsPwd + ID-on-file) whenever the discount has
    /// RequiresIdVerification set and this is not a manual override - a Senior
    /// Citizen/PWD discount can never be applied to a guest who isn't verified as one.</param>
    Task<DiscountCalculationResult> CalculateAsync(
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
        int? guestId = null);
}
