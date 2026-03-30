using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireModuleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _requiredModule;

    public RequireModuleAttribute(string requiredModule)
    {
        _requiredModule = requiredModule;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var roleClaim = user.FindFirst("Role")?.Value;
        var companyIdClaim = user.FindFirst("ActiveCompanyId")?.Value ?? user.FindFirst("TenantId")?.Value;

        if (Roles.IsAdmin(roleClaim)) return;

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

        var canUseModule = entitlementResolver.CanUseModuleAsync(companyId, _requiredModule).GetAwaiter().GetResult();
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
    }
}
