using System;
using System.Security.Claims;

namespace OrvixFlow.Core.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetTenantId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("TenantId")?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var tenantId))
        {
            throw new UnauthorizedAccessException("TenantId claim missing or invalid");
        }
        return tenantId;
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Email)?.Value 
        ?? principal.FindFirst("email")?.Value;

    public static string? GetRole(this ClaimsPrincipal principal)
        => principal.FindFirst("Role")?.Value;
}
