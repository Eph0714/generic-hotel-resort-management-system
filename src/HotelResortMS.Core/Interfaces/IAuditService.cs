using HotelResortMS.Core.Common;

namespace HotelResortMS.Core.Interfaces;

/// <summary>
/// Section 46/56: the single write path for audit rows. Every module that creates, edits,
/// deletes, archives, voids, or refunds something must call this rather than writing its
/// own ad-hoc log - that consistency is what makes the Audit Trail report meaningful.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        string module,
        string action,
        string? recordId = null,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null);
}

/// <summary>
/// Section 55: server-side permission checks. Controllers use [RequirePermission] (which
/// calls this) instead of only hiding buttons in the view.
/// </summary>
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(string userId, string module, PermissionAction action);

    /// <summary>Loads every module's permission flags for the given role - used to render
    /// the Role-Permission matrix screen.</summary>
    Task<Dictionary<string, RolePermissionFlags>> GetRolePermissionsAsync(string roleId);

    Task SetRolePermissionAsync(string roleId, string module, RolePermissionFlags flags);
}

/// <summary>Plain DTO mirroring RolePermission's action flags, kept in Core so the
/// interface doesn't need to reference the Infrastructure-owned EF entity.</summary>
public class RolePermissionFlags
{
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

/// <summary>Wraps the current HTTP request's authenticated user, so services outside the
/// Web project can attribute actions without depending on ASP.NET Core directly.</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }
}

/// <summary>
/// Section 12/41/42/43: the single source of truth for "what day is the hotel operating
/// on right now". Every financial/operational write should stamp BusinessDate from here,
/// never from DateTime.Now directly.
/// </summary>
public interface IBusinessDateService
{
    Task<Entities.BusinessDate> GetCurrentAsync();
    Task<Entities.BusinessDate> OpenNextDayAsync(string userId, decimal? overrideBeginningCash = null);

    /// <summary>Returns the current business date, but throws if it is Closed - the guard
    /// every financial-posting write path (POS, Payments, Expenses, Check-In) calls before
    /// writing, so nothing posts against a day that is waiting on "Open Next Business Day".</summary>
    Task<Entities.BusinessDate> GetCurrentForPostingAsync();
}

/// <summary>Section 47: generates gap-free, never-duplicated document numbers
/// (RES-2026-000001, POS-2026-000001, ...) with a configurable prefix per document type.</summary>
public interface INumberingService
{
    Task<string> GenerateAsync(string documentType);
}
