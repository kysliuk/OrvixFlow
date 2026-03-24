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
        var tenantIdClaim = user?.FindFirst("ActiveCompanyId")?.Value
            ?? user?.FindFirst("TenantId")?.Value;
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

        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            throw new UnauthorizedAccessException("Tenant ID is missing from the current request context.");
        }

        if (Guid.TryParse(tenantIdClaim, out var tenantGuid))
        {
            return tenantGuid;
        }

        throw new UnauthorizedAccessException("Invalid Tenant ID format in the current request context.");
    }
}
