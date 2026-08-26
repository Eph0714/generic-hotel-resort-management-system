using Microsoft.AspNetCore.Identity;

namespace HotelResortMS.Core.Entities.Identity;

/// <summary>
/// Extends the default Identity user with fields the system needs for staff records
/// (Section 45 - User Management CRUD). Kept separate from Guests: guests are customers,
/// users are staff/system accounts and always require a role.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public DateTime? DeactivatedAt { get; set; }
    public string? DeactivatedBy { get; set; }

    public DateTime? LastLoginAt { get; set; }
}
