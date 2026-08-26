using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities.Identity;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Web.Areas.Admin.Models;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HotelResortMS.Web.Areas.Admin.Controllers;

/// <summary>Section 45/55: Roles CRUD plus the per-role permission matrix. System roles
/// (Section 45's built-in six) can be edited but never deleted or renamed (Section 8/9 -
/// deleting a role that is in active use would silently strip everyone's access).</summary>
[Area("Admin")]
[RequirePermission(SystemModules.Roles, PermissionAction.View)]
public class RolesController : Controller
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _permissionService;
    private readonly IAuditService _auditService;

    public RolesController(RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager, IPermissionService permissionService, IAuditService auditService)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _permissionService = permissionService;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var roles = _roleManager.Roles.ToList();
        var items = new List<RoleListItemViewModel>();

        foreach (var role in roles)
        {
            items.Add(new RoleListItemViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                Description = role.Description,
                IsSystemRole = role.IsSystemRole,
                UserCount = (await _userManager.GetUsersInRoleAsync(role.Name ?? string.Empty)).Count
            });
        }

        return View(items.OrderBy(r => r.Name).ToList());
    }

    [RequirePermission(SystemModules.Roles, PermissionAction.Add)]
    public IActionResult Create() => View(new RoleCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Roles, PermissionAction.Add)]
    public async Task<IActionResult> Create(RoleCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (await _roleManager.RoleExistsAsync(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "A role with this name already exists.");
            return View(model);
        }

        var role = new ApplicationRole(model.Name) { Description = model.Description, IsSystemRole = false };
        await _roleManager.CreateAsync(role);

        await _auditService.LogAsync(SystemModules.Roles, "Create", role.Id, newValues: new { role.Name, role.Description });

        TempData["Success"] = "Role created.";
        return RedirectToAction(nameof(Index));
    }

    // Section 8: a delete is blocked outright if the role is a protected system role or
    // still has members - deleting it would silently strip those users of access with no
    // way to trace what happened.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Roles, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        if (role.IsSystemRole)
        {
            TempData["Error"] = "This is a built-in system role and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        var members = await _userManager.GetUsersInRoleAsync(role.Name ?? string.Empty);
        if (members.Count > 0)
        {
            TempData["Error"] = $"This role cannot be deleted because {members.Count} user(s) are still assigned to it. Reassign them first.";
            return RedirectToAction(nameof(Index));
        }

        await _roleManager.DeleteAsync(role);
        await _auditService.LogAsync(SystemModules.Roles, "Delete", id, oldValues: new { role.Name });

        TempData["Success"] = "Role deleted.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Roles, PermissionAction.Configure)]
    public async Task<IActionResult> Permissions(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        var modules = await _permissionService.GetRolePermissionsAsync(id);

        return View(new RolePermissionMatrixViewModel
        {
            RoleId = role.Id,
            RoleName = role.Name ?? string.Empty,
            IsSystemRole = role.IsSystemRole,
            Modules = modules
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Roles, PermissionAction.Configure)]
    public async Task<IActionResult> Permissions(string id, [FromForm] Dictionary<string, string[]> module)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        // Super Admin's grants are seeded fixed-full and are not editable through the UI -
        // it must always retain complete access (see DbSeeder).
        if (role.Name == SystemRoles.SuperAdmin)
        {
            TempData["Error"] = "Super Admin permissions cannot be modified.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var moduleKey in SystemModules.All)
        {
            var checkedActions = module.TryGetValue(moduleKey, out var actions) ? actions : Array.Empty<string>();
            var flags = new RolePermissionFlags
            {
                CanView = checkedActions.Contains("View"),
                CanAdd = checkedActions.Contains("Add"),
                CanEdit = checkedActions.Contains("Edit"),
                CanDelete = checkedActions.Contains("Delete"),
                CanApprove = checkedActions.Contains("Approve"),
                CanVoid = checkedActions.Contains("Void"),
                CanRefund = checkedActions.Contains("Refund"),
                CanPrint = checkedActions.Contains("Print"),
                CanExport = checkedActions.Contains("Export"),
                CanConfigure = checkedActions.Contains("Configure")
            };

            await _permissionService.SetRolePermissionAsync(id, moduleKey, flags);
        }

        await _auditService.LogAsync(SystemModules.Roles, "PermissionsUpdated", id);

        TempData["Success"] = "Permissions updated.";
        return RedirectToAction(nameof(Permissions), new { id });
    }
}
