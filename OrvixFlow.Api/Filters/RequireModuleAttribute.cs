using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Filters;

/// <summary>
/// Authorization filter that enforces module access based on company billing entitlements.
/// </summary>
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

        // Platform admins bypass ALL module checks (SuperAdmin, InternalOperator)
        if (UserRoleExtensions.ParseRole(roleClaim).IsPlatformAdmin()) return;

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

        // FIX: Check company billing entitlement BEFORE user-level permissions.
        // All roles (including CompanyAdmin and CompanyOwner) must pass this billing check.
        // The company must be entitled to the module before any user can access it.
        // Use CanUseModuleWithOverridesAsync so that CompanyModuleOverride rows are respected
        // at the API gate — consistent with the AccessResolver fallback path.
        var canUseModule = await entitlementResolver.CanUseModuleWithOverridesAsync(companyId, _requiredModule);
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

        // Company admins (CompanyOwner, CompanyAdmin) bypass user-level permission checks
        // after passing the billing entitlement check above.
        var parsedRole = UserRoleExtensions.ParseRole(roleClaim);
        if (parsedRole.IsCompanyAdmin()) return;

        // FIX F-07: Check user-level permissions for non-admin users
        var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "Invalid user context",
                message = "A valid user identifier is required to evaluate module permissions."
            });
            return;
        }

        var accessResolver = context.HttpContext.RequestServices.GetService(typeof(IAccessResolver)) as IAccessResolver;
        if (accessResolver == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

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
        }
    }
}
