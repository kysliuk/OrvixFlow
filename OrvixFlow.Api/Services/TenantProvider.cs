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
        var tenantIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("TenantId")?.Value;
        
        // For development/testing, fallback to a header if claim is missing, or throw if required.
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            var headerTenant = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-ID"].ToString();
            if (!string.IsNullOrEmpty(headerTenant) && Guid.TryParse(headerTenant, out var headerGuid))
            {
                return headerGuid;
            }
            
            // If strictly required and missing, normally we'd throw an UnauthorizedAccessException.
            throw new UnauthorizedAccessException("Tenant ID is missing from the current request context.");
        }

        if (Guid.TryParse(tenantIdClaim, out var tenantGuid))
        {
            return tenantGuid;
        }

        throw new UnauthorizedAccessException("Invalid Tenant ID format in the current request context.");
    }
}
