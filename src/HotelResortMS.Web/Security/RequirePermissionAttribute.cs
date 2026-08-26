using HotelResortMS.Core.Common;
using HotelResortMS.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelResortMS.Web.Security;

/// <summary>
/// Section 55 (CRUD Permission Control): enforces a module/action permission on the server
/// side before the action runs. Views may also hide buttons for a nicer UX, but this filter
/// is the actual authorization boundary - a hidden button is not a security control.
/// Unauthenticated users are redirected to login; authenticated-but-unauthorized users get
/// a 403 Access Denied page rather than a raw exception.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string module, PermissionAction action)
        : base(typeof(RequirePermissionFilter))
    {
        Arguments = new object[] { module, action };
    }

    private class RequirePermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly string _module;
        private readonly PermissionAction _action;
        private readonly IPermissionService _permissionService;

        public RequirePermissionFilter(string module, PermissionAction action, IPermissionService permissionService)
        {
            _module = module;
            _action = action;
            _permissionService = permissionService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity is null || !user.Identity.IsAuthenticated)
            {
                // area = "" clears the ambient route value from the current request - without
                // it, a redirect from an Area-scoped controller (e.g. Admin/Users) resolves
                // to "/Admin/Account/Login", which doesn't exist (AccountController isn't in
                // any area), and 404s instead of showing the login/access-denied page.
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "", returnUrl = context.HttpContext.Request.Path });
                return;
            }

            var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null || !await _permissionService.HasPermissionAsync(userId, _module, _action))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", new { area = "" });
            }
        }
    }
}
