using System.Linq;
using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Api.Security;

namespace OrvixFlow.Tests;

public class Phase0SecurityHardeningTests
{
    [Fact]
    public void Register_UsesDedicatedRegisterRateLimitPolicy()
    {
        var registerMethod = typeof(AuthController).GetMethod(nameof(AuthController.Register));

        registerMethod.Should().NotBeNull();

        var rateLimitAttribute = registerMethod!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>()
            .SingleOrDefault();

        rateLimitAttribute.Should().NotBeNull();
        rateLimitAttribute!.PolicyName.Should().Be(RateLimitPolicyNames.Register);
    }

    [Fact]
    public void RegisterPolicy_UsesExpectedFixedWindowSettings()
    {
        var options = RateLimitPolicies.CreateRegisterOptions();

        options.PermitLimit.Should().Be(10);
        options.Window.Should().Be(TimeSpan.FromHours(1));
        options.QueueLimit.Should().Be(0);
        options.QueueProcessingOrder.Should().Be(QueueProcessingOrder.OldestFirst);
    }

    [Fact]
    public void ApiContentSecurityPolicy_DisablesEmbeddedContentAndInlineScripts()
    {
        SecurityHeaderPolicies.ApiContentSecurityPolicy.Should().Contain("default-src 'self'");
        SecurityHeaderPolicies.ApiContentSecurityPolicy.Should().Contain("script-src 'self'");
        SecurityHeaderPolicies.ApiContentSecurityPolicy.Should().Contain("connect-src 'self'");
        SecurityHeaderPolicies.ApiContentSecurityPolicy.Should().Contain("frame-ancestors 'none'");
    }
}
