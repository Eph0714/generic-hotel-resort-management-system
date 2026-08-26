namespace HotelResortMS.Core.Entities.Identity;

/// <summary>
/// Section 55 (CRUD Permission Control): grants a role a specific action on a specific module.
/// One row per (Role, Module) pair holding every possible action flag. Authorization must be
/// enforced server-side (via PermissionService/RequirePermission), never just by hiding buttons.
/// </summary>
public class RolePermission
{
    public int Id { get; set; }

    public string RoleId { get; set; } = string.Empty;
    public ApplicationRole? Role { get; set; }

    /// <summary>Module key, e.g. "Rooms", "Reservations", "POS" - see SystemModules for the canonical list.</summary>
    public string Module { get; set; } = string.Empty;

    public bool CanView { get; set; }
    public bool CanAdd { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }
    public bool CanVoid { get; set; }
    public bool CanRefund { get; set; }
    public bool CanPrint { get; set; }
    public bool CanExport { get; set; }
    public bool CanConfigure { get; set; }
}
