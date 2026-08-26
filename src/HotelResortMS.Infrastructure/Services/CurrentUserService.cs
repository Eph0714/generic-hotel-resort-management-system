using HotelResortMS.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HotelResortMS.Infrastructure.Services;

/// <summary>Reads the authenticated user's identity/IP off the current HTTP request. Kept
/// in Infrastructure (not Core) because it depends on ASP.NET Core's HttpContext.</summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?
        .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    public string? UserName => _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
}
