namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 42: a snapshot of the exceptions found (or overridden) the moment Night Audit
/// was run for a given BusinessDate - kept as its own historical row rather than
/// overwriting BusinessDate fields, so a later report can show exactly what was flagged
/// and who chose to override it.
/// </summary>
public class NightAuditRecord
{
    public int Id { get; set; }

    public int BusinessDateId { get; set; }
    public BusinessDate? BusinessDate { get; set; }

    /// <summary>Newline-separated list of unresolved exceptions found at run time.</summary>
    public string ExceptionsFound { get; set; } = string.Empty;

    public bool WasOverridden { get; set; }
    public string? OverrideReason { get; set; }

    public DateTime RunAt { get; set; }
    public string RunBy { get; set; } = string.Empty;
}
