using Microsoft.AspNetCore.Identity;

namespace HotelResortMS.Core.Entities.Identity;

/// <summary>
/// Custom role type so we can attach a description and keep the built-in system roles
/// (Super Admin, Administrator, Front Desk, POS Staff, Inventory Staff, Accountant/Cashier - Section 45)
/// protected from accidental deletion.
/// </summary>
public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }

    /// <summary>System roles seeded at startup cannot be deleted or renamed through the UI.</summary>
    public bool IsSystemRole { get; set; } = false;

    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
