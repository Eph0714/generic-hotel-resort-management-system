namespace HotelResortMS.Core.Entities;

/// <summary>
/// Section 16 (Guest Management CRUD): a customer of the hotel/resort. Distinct from
/// ApplicationUser (staff) - guests never log in to this system in Phase 2. Archived
/// rather than hard-deleted once they have any reservation/folio/payment history
/// (Section 8/9/10).
/// </summary>
public class Guest : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Phone { get; set; }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    /// <summary>e.g. Walk-in, Corporate, Travel Agency, VIP - configurable, not hard-coded (Section 16).</summary>
    public string? GuestType { get; set; }

    /// <summary>Corporate/travel-agency account name, when applicable.</summary>
    public string? CompanyName { get; set; }

    // --- Senior Citizen / PWD eligibility (Section 17) ---
    public bool IsSeniorCitizen { get; set; }
    public string? SeniorCitizenIdNumber { get; set; }

    public bool IsPwd { get; set; }
    public string? PwdIdNumber { get; set; }

    public string? Notes { get; set; }

    public ICollection<GuestIdentification> Identifications { get; set; } = new List<GuestIdentification>();
}

/// <summary>Section 16/48: an uploaded/recorded identification document for a guest
/// (passport, driver's license, Senior Citizen/PWD ID, ...). File content itself is stored
/// under wwwroot-external storage in a later phase; this row tracks the metadata and
/// authorization needed to view it (Section 3/57 - protect sensitive guest information).</summary>
public class GuestIdentification : BaseEntity
{
    public int GuestId { get; set; }
    public Guest? Guest { get; set; }

    public string IdType { get; set; } = string.Empty; // Passport, Driver's License, Senior Citizen ID, PWD ID, ...
    public string IdNumber { get; set; } = string.Empty;
    public string? IssuingAuthority { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>Relative path to the stored scan/photo, when uploaded.</summary>
    public string? FilePath { get; set; }
}
