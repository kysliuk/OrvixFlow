using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireModuleAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _requiredModule;

    public RequireModuleAttribute(string requiredModule)
    {
        _requiredModule = requiredModule;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var roleClaim = user.FindFirst("Role")?.Value;
        var companyIdClaim = user.FindFirst("ActiveCompanyId")?.Value ?? user.FindFirst("TenantId")?.Value;

        // Platform admins bypass module checks
        if (Roles.IsAdmin(roleClaim)) return;

        // Company admins (CompanyOwner, CompanyAdmin) bypass user-level permission checks
        var parsedRole = UserRoleExtensions.ParseRole(roleClaim);
        if (parsedRole.IsCompanyAdmin()) return;

        if (!Guid.TryParse(companyIdClaim, out var companyId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var entitlementResolver = context.HttpContext.RequestServices.GetService(typeof(IEntitlementResolver)) as IEntitlementResolver;
        if (entitlementResolver == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // FIX F-06: Use async instead of .GetAwaiter().GetResult()
        var canUseModule = await entitlementResolver.CanUseModuleAsync(companyId, _requiredModule);
        if (!canUseModule)
        {
            context.Result = new ObjectResult(new 
            { 
                error = "Module Not Available",
                message = $"The '{_requiredModule}' module is not available for your current plan."
            })
            {
                StatusCode = 403
            };
            return;
        }

        // FIX F-07: Also check user-level permissions (unless already admin via Roles.IsAdmin above)
        var userIdClaim = user.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
        {
            var accessResolver = context.HttpContext.RequestServices.GetService(typeof(IAccessResolver)) as IAccessResolver;
            if (accessResolver != null)
            {
                var permissions = await accessResolver.GetEffectivePermissionsAsync(userId, companyId, _requiredModule);
                if (!permissions.CanUse)
                {
                    context.Result = new ObjectResult(new 
                    { 
                        error = "Access Denied",
                        message = $"You do not have permission to use the '{_requiredModule}' module."
                    })
                    {
                        StatusCode = 403
                    };
                    return;
                }
            }
        }
    }
}
