using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities.Identity;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Web.Areas.Admin.Models;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Areas.Admin.Controllers;

/// <summary>Section 45 (User Management CRUD). Every action enforces the Users module
/// permission server-side (Section 55) via RequirePermission.</summary>
[Area("Admin")]
[RequirePermission(SystemModules.Users, PermissionAction.View)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IAuditService _auditService;

    public UsersController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IAuditService auditService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var users = _userManager.Users.ToList();
        var items = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            if (!string.IsNullOrWhiteSpace(search) &&
                !(user.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                  (user.Email ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            items.Add(new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }

        ViewBag.Search = search;
        return View(items.OrderBy(u => u.FullName).ToList());
    }

    [RequirePermission(SystemModules.Users, PermissionAction.Add)]
    public IActionResult Create()
    {
        return View(new UserCreateViewModel { AvailableRoles = GetRoleOptions() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Users, PermissionAction.Add)]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = GetRoleOptions();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName,
            EmployeeNumber = model.EmployeeNumber,
            IsActive = true,
            CreatedBy = User.Identity?.Name
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            model.AvailableRoles = GetRoleOptions();
            return View(model);
        }

        var role = await _roleManager.FindByIdAsync(model.RoleId);
        if (role?.Name is not null)
        {
            await _userManager.AddToRoleAsync(user, role.Name);
        }

        await _auditService.LogAsync(SystemModules.Users, "Create", user.Id, newValues: new { user.FullName, user.Email, Role = role?.Name });

        TempData["Success"] = "User created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Users, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var currentRole = await _roleManager.Roles.FirstOrDefaultAsync(r => roles.Contains(r.Name!));

        return View(new UserEditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            EmployeeNumber = user.EmployeeNumber,
            IsActive = user.IsActive,
            RoleId = currentRole?.Id ?? string.Empty,
            AvailableRoles = GetRoleOptions()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Users, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.AvailableRoles = GetRoleOptions();
            return View(model);
        }

        var oldValues = new { user.FullName, user.Email, user.IsActive };

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.EmployeeNumber = model.EmployeeNumber;
        user.IsActive = model.IsActive;

        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var newRole = await _roleManager.FindByIdAsync(model.RoleId);
        if (newRole?.Name is not null && !currentRoles.Contains(newRole.Name))
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole.Name);
        }

        await _auditService.LogAsync(SystemModules.Users, "Update", user.Id, oldValues, new { user.FullName, user.Email, user.IsActive });

        TempData["Success"] = "User updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8/10: staff accounts are never hard-deleted (they are attributed on audit
    // logs, transactions, etc.) - only deactivated, which also blocks login.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Users, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.IsActive = false;
        user.DeactivatedAt = DateTime.UtcNow;
        user.DeactivatedBy = User.Identity?.Name;
        await _userManager.UpdateAsync(user);

        await _auditService.LogAsync(SystemModules.Users, "Deactivate", user.Id, reason: "Deactivated by administrator");

        TempData["Success"] = "User deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Users, PermissionAction.Edit)]
    public async Task<IActionResult> Activate(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.IsActive = true;
        user.DeactivatedAt = null;
        user.DeactivatedBy = null;
        await _userManager.UpdateAsync(user);

        await _auditService.LogAsync(SystemModules.Users, "Activate", user.Id);

        TempData["Success"] = "User activated.";
        return RedirectToAction(nameof(Index));
    }

    private List<RoleOption> GetRoleOptions() =>
        _roleManager.Roles.Select(r => new RoleOption { Id = r.Id, Name = r.Name ?? string.Empty }).ToList();
}
