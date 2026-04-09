using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrvixFlow.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAutomationKeyAttribute : Attribute, IAuthorizationFilter
{
    private const string ApiKeyHeaderName = "X-Automation-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            context.Result = new UnauthorizedObjectResult("Automation API Key missing.");
            return;
        }

        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var configuredApiKey = configuration.GetValue<string>("AutomationKey");

        if (string.IsNullOrEmpty(configuredApiKey))
        {
            context.Result = new UnauthorizedObjectResult("Automation API Key not configured.");
            return;
        }

        // F-16 FIX: Use constant-time comparison to prevent timing attacks.
        // String.Equals() with ordinal comparison is not constant-time for variable-length
        // inputs. CryptographicOperations.FixedTimeEquals operates on byte arrays of equal
        // length and runs in constant time regardless of where the first difference occurs.
        var configuredBytes = Encoding.UTF8.GetBytes(configuredApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(extractedApiKey.ToString());

        // Both must be the same length for FixedTimeEquals to not throw.
        // Pad the shorter one with null bytes to make lengths equal (null-byte padding
        // won't match a real key that has actual bytes in those positions).
        var maxLength = Math.Max(configuredBytes.Length, providedBytes.Length);
        var paddedConfigured = new byte[maxLength];
        var paddedProvided = new byte[maxLength];
        Array.Copy(configuredBytes, paddedConfigured, configuredBytes.Length);
        Array.Copy(providedBytes, paddedProvided, providedBytes.Length);

        if (!CryptographicOperations.FixedTimeEquals(paddedConfigured, paddedProvided))
        {
            context.Result = new UnauthorizedObjectResult("Invalid Automation API Key.");
        }
    }
}
