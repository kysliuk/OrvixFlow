using System;
using System.Linq;
using Hangfire.Dashboard;

namespace OrvixFlow.Api.Filters;

/// <summary>
/// F-22 Fix: Custom Hangfire dashboard authorization filter that requires a valid
/// SuperAdmin JWT. Replaces the insecure LocalRequestsOnlyAuthorizationFilter
/// which only protected against non-local access at the network level.
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Check if the user is authenticated
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
            return false;

        // Check for SuperAdmin role in JWT claims
        var roleClaims = httpContext.User.Claims
            .Where(c => c.Type == "Role" || c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        // Only SuperAdmin may access the Hangfire dashboard
        return roleClaims.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase);
    }
}
