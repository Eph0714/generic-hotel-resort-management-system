namespace HotelResortMS.Core.Entities;

/// <summary>Section 23: the configurable outcome a policy applies when a reservation is
/// cancelled - never hard-coded, so different room types/rate plans can carry different
/// policies without a code change.</summary>
public enum CancellationPolicyType
{
    FreeCancellation,
    PartialRefund,
    CancellationFee,
    DepositForfeiture,
    NoShowCharge
}

/// <summary>
/// Section 23: applied by ReservationService at cancellation/no-show time.
/// HoursBeforeCheckIn is the free-cancellation window - cancelling at or before that many
/// hours ahead of CheckInDate costs nothing regardless of PolicyType; cancelling later
/// triggers whatever this policy specifies (a percentage fee, a fixed fee, or forfeiting
/// the deposit already paid).
/// </summary>
public class CancellationPolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public CancellationPolicyType Type { get; set; }
    public int HoursBeforeCheckIn { get; set; }

    public decimal FeePercentage { get; set; }
    public decimal FixedFee { get; set; }
}
