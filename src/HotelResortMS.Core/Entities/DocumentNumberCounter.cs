namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 47 (Document Numbering): backs INumberingService. One row per (DocumentType, Year)
/// holding the last-issued sequence number and the configurable prefix, so numbers such as
/// RES-2026-000001 never duplicate even under concurrent requests (allocated inside a
/// transaction with a row lock in NumberingService).
/// </summary>
public class DocumentNumberCounter
{
    public int Id { get; set; }

    /// <summary>e.g. "RES", "FOL", "POS", "PAY", "EXP", "PUR", "INC", "EVT", "AUD".</summary>
    public string DocumentType { get; set; } = string.Empty;

    public int Year { get; set; }

    public long LastSequence { get; set; }

    public string Prefix { get; set; } = string.Empty;

    /// <summary>Zero-padding width for the sequence portion (6 in RES-2026-000001).</summary>
    public int PaddingWidth { get; set; } = 6;
}
