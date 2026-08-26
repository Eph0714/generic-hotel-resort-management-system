using System.ComponentModel.DataAnnotations;

namespace HotelResortMS.Web.Areas.Admin.Models;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}

public class UserCreateViewModel
{
    [Required, Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? EmployeeNumber { get; set; }

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Display(Name = "Role")]
    public string RoleId { get; set; } = string.Empty;

    public List<RoleOption> AvailableRoles { get; set; } = new();
}

public class UserEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required, Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? EmployeeNumber { get; set; }

    public bool IsActive { get; set; }

    [Display(Name = "Role")]
    public string RoleId { get; set; } = string.Empty;

    public List<RoleOption> AvailableRoles { get; set; } = new();
}

public class RoleOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
