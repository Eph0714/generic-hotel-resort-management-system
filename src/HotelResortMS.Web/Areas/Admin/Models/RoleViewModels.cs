using System.ComponentModel.DataAnnotations;
using HotelResortMS.Core.Interfaces;

namespace HotelResortMS.Web.Areas.Admin.Models;

public class RoleListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int UserCount { get; set; }
}

public class RoleCreateViewModel
{
    [Required, Display(Name = "Role Name")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>Backs the module x action permission matrix screen (Section 55).</summary>
public class RolePermissionMatrixViewModel
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }

    public Dictionary<string, RolePermissionFlags> Modules { get; set; } = new();
}
