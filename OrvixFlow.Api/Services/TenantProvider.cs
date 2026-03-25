using System;
using Microsoft.AspNetCore.Http;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Services;

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetTenantId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var tenantIdClaim = user?.FindFirst("TenantId")?.Value;
        var roleClaim = user?.FindFirst("Role")?.Value;

        // Check if an Admin is trying to impersonate a different tenant
        if (Roles.IsAdmin(roleClaim))
        {
            var impersonateHeader = _httpContextAccessor.HttpContext?.Request.Headers["X-Impersonate-Tenant"].ToString();
            if (!string.IsNullOrEmpty(impersonateHeader) && Guid.TryParse(impersonateHeader, out var impersonateGuid))
            {
                return impersonateGuid; // Admins assume the target tenant's scope
            }
        }

        if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantGuid))
        {
            return tenantGuid;
        }

        // Fallback to header if no claim
        var tenantHeader = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-ID"].ToString();
        if (!string.IsNullOrEmpty(tenantHeader) && Guid.TryParse(tenantHeader, out var headerGuid))
        {
            return headerGuid;
        }

        throw new UnauthorizedAccessException("Tenant ID is missing from the current request context.");
    }
}
