using System;
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
        var userIdClaim = user.FindFirst("sub")?.Value;
        var companyIdClaim = user.FindFirst("ActiveCompanyId")?.Value ?? user.FindFirst("TenantId")?.Value;

        // God mode: admins bypass all subscription gates
        if (Roles.IsAdmin(roleClaim)) return;

        if (!Guid.TryParse(userIdClaim, out var userId) || !Guid.TryParse(companyIdClaim, out var companyId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var resolver = context.HttpContext.RequestServices.GetService(typeof(IAccessResolver)) as IAccessResolver;
        if (resolver == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        var permissions = resolver.GetEffectivePermissionsAsync(userId, companyId, _requiredModule).GetAwaiter().GetResult();
        if (!permissions.CanView)
        {
            // Hidden by default: do not reveal module existence.
            context.Result = new NotFoundResult();
            return;
        }

        if (!permissions.CanUse)
        {
            context.Result = new ObjectResult(new 
            { 
                error = "Access Denied",
                message = $"The '{_requiredModule}' module is not executable in your current scope."
            })
            {
                StatusCode = 403
            };
            return;
        }
    }

}
