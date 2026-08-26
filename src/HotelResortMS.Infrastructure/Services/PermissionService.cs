using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities.Identity;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Services;

/// <inheritdoc cref="IPermissionService"/>
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PermissionService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<bool> HasPermissionAsync(string userId, string module, PermissionAction action)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return false;
        }

        var roleNames = await _userManager.GetRolesAsync(user);

        // Super Admin always has full access, everywhere - it must never be possible to
        // lock every admin account out of a module through a permission-matrix mistake.
        if (roleNames.Contains(SystemRoles.SuperAdmin))
        {
            return true;
        }

        var roleIds = await _db.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        var grants = await _db.RolePermissions
            .Where(p => roleIds.Contains(p.RoleId) && p.Module == module)
            .ToListAsync();

        // A user can hold multiple roles; the effective permission is the union of what
        // any of their roles grants for this module/action.
        return grants.Any(g => action switch
        {
            PermissionAction.View => g.CanView,
            PermissionAction.Add => g.CanAdd,
            PermissionAction.Edit => g.CanEdit,
            PermissionAction.Delete => g.CanDelete,
            PermissionAction.Approve => g.CanApprove,
            PermissionAction.Void => g.CanVoid,
            PermissionAction.Refund => g.CanRefund,
            PermissionAction.Print => g.CanPrint,
            PermissionAction.Export => g.CanExport,
            PermissionAction.Configure => g.CanConfigure,
            _ => false
        });
    }

    public async Task<Dictionary<string, RolePermissionFlags>> GetRolePermissionsAsync(string roleId)
    {
        var grants = await _db.RolePermissions.Where(p => p.RoleId == roleId).ToListAsync();
        var result = new Dictionary<string, RolePermissionFlags>();

        foreach (var module in SystemModules.All)
        {
            var g = grants.FirstOrDefault(x => x.Module == module);
            result[module] = new RolePermissionFlags
            {
                CanView = g?.CanView ?? false,
                CanAdd = g?.CanAdd ?? false,
                CanEdit = g?.CanEdit ?? false,
                CanDelete = g?.CanDelete ?? false,
                CanApprove = g?.CanApprove ?? false,
                CanVoid = g?.CanVoid ?? false,
                CanRefund = g?.CanRefund ?? false,
                CanPrint = g?.CanPrint ?? false,
                CanExport = g?.CanExport ?? false,
                CanConfigure = g?.CanConfigure ?? false
            };
        }

        return result;
    }

    public async Task SetRolePermissionAsync(string roleId, string module, RolePermissionFlags flags)
    {
        var existing = await _db.RolePermissions
            .FirstOrDefaultAsync(p => p.RoleId == roleId && p.Module == module);

        if (existing is null)
        {
            existing = new RolePermission { RoleId = roleId, Module = module };
            _db.RolePermissions.Add(existing);
        }

        existing.CanView = flags.CanView;
        existing.CanAdd = flags.CanAdd;
        existing.CanEdit = flags.CanEdit;
        existing.CanDelete = flags.CanDelete;
        existing.CanApprove = flags.CanApprove;
        existing.CanVoid = flags.CanVoid;
        existing.CanRefund = flags.CanRefund;
        existing.CanPrint = flags.CanPrint;
        existing.CanExport = flags.CanExport;
        existing.CanConfigure = flags.CanConfigure;

        await _db.SaveChangesAsync();
    }
}
