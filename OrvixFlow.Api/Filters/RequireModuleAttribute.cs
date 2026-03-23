using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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

        var planClaim = user.FindFirst("Plan")?.Value ?? "Free";
        var roleClaim = user.FindFirst("Role")?.Value;

        // God mode: admins bypass all subscription gates
        if (Roles.IsAdmin(roleClaim)) return;

        if (!HasAccessToModule(planClaim, _requiredModule))
        {
            context.Result = new ObjectResult(new 
            { 
                error = "Subscription Upgrade Required", 
                message = $"The '{_requiredModule}' module requires a higher subscription tier."
            })
            {
                StatusCode = 403
            };
            return;
        }
    }

    private bool HasAccessToModule(string plan, string moduleKey)
    {
        // Simple hierarchy for MVP: plan values represent access levels
        int planLevel = plan switch
        {
            "Free" => 0,
            "Starter" => 1,
            "Pro" => 2,
            "Enterprise" => 3,
            _ => 0
        };

        // Define which level is required for which module
        int requiredLevel = moduleKey.ToLower() switch
        {
            "knowledge-base" => 0,   // Included in Free
            "inbox-guardian" => 1,   // Requires Starter+
            "n8n-automations" => 2,  // Requires Pro+
            "custom-models" => 3,    // Requires Enterprise
            _ => 3 // Default strict
        };

        return planLevel >= requiredLevel;
    }
}
