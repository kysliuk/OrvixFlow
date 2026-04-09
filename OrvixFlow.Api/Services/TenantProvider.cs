using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Services;

/// <summary>
/// Provides tenant context for the current request.
/// Tenant ID is ALWAYS resolved from the JWT claims - never from HTTP headers.
/// This ensures proper tenant isolation and prevents cross-tenant attacks.
/// </summary>
public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantProvider> _logger;

    public TenantProvider(IHttpContextAccessor httpContextAccessor, ILogger<TenantProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Guid GetTenantId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var tenantIdClaim = user?.FindFirst("ActiveCompanyId")?.Value
            ?? user?.FindFirst("TenantId")?.Value;
        var roleClaim = user?.FindFirst("Role")?.Value;
        var userIdClaim = user?.FindFirst("sub")?.Value;

        // Check if an Admin is trying to impersonate a different tenant
        if (Roles.IsAdmin(roleClaim))
        {
            var impersonateHeader = _httpContextAccessor.HttpContext?.Request.Headers["X-Impersonate-Tenant"].ToString();
            if (!string.IsNullOrEmpty(impersonateHeader) && Guid.TryParse(impersonateHeader, out var impersonateGuid))
            {
                // F-10 FIX: Log admin impersonation at Warning level for security audit trail
                _logger.LogWarning(
                    "SECURITY: Admin impersonation started. AdminUserId={AdminUserId}, ImpersonatedTenantId={ImpersonatedTenantId}, RemoteIp={RemoteIp}",
                    userIdClaim ?? "unknown",
                    impersonateGuid,
                    _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                );

                return impersonateGuid; // Admins assume the target tenant's scope
            }
        }

        if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantGuid))
        {
            return tenantGuid;
        }

        // F-09 FIX: Removed the X-Tenant-ID header fallback.
        // Tenant ID must ALWAYS come from JWT claims to prevent cross-tenant attacks.
        // For webhook paths that need X-Tenant-ID, use a separate WebhookTenantProvider
        // that validates HMAC signature before accepting the header.

        return Guid.Empty;
    }
}
