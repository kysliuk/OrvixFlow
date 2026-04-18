using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using OrvixFlow.Api.Filters;
using OrvixFlow.Core.Interfaces;
using Xunit;

namespace OrvixFlow.Tests;

public class RequireModuleAttributeTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void RequireModule_ImplementsIAsyncAuthorizationFilter()
    {
        // RED test: Verify the attribute implements IAsyncAuthorizationFilter instead of IAuthorizationFilter
        // This will fail until we convert the attribute to async
        var attr = new RequireModuleAttribute("test-module");
        
        attr.Should().BeAssignableTo<IAsyncAuthorizationFilter>();
    }

    [Fact]
    public void RequireModule_HasAsyncOnAuthorizationMethod()
    {
        // RED test: Verify there's an async authorization method
        var attr = new RequireModuleAttribute("test-module");
        
        var method = attr.GetType().GetMethod("OnAuthorizationAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
    }

    [Fact]
    public async Task CompanyAdmin_CannotAccessModule_WhenCompanyNotEntitled()
    {
        // BUG REPRODUCER: CompanyAdmin on Free plan should NOT be able to access paid modules.
        // Current bug: IsCompanyAdmin() returns early, bypassing CanUseModuleAsync() check.
        // Expected: 403 Module Not Available
        // Bug behavior: Returns without checking entitlements (403 never thrown)
        
        var entitlementResolver = new Mock<IEntitlementResolver>();
        entitlementResolver
            .Setup(x => x.CanUseModuleAsync(_companyId, "inbox-guardian"))
            .ReturnsAsync(false); // Company NOT entitled to this module

        var accessResolver = new Mock<IAccessResolver>();

        var context = CreateAuthorizationFilterContext("CompanyAdmin", _companyId, _userId);
        RegisterServices(context.HttpContext.RequestServices, entitlementResolver.Object, accessResolver.Object);

        var attr = new RequireModuleAttribute("inbox-guardian");
        
        await attr.OnAuthorizationAsync(context);

        context.Result.Should().NotBeNull();
        context.Result.Should().BeAssignableTo<ObjectResult>();
        var objectResult = (ObjectResult)context.Result!;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CompanyOwner_CannotAccessModule_WhenCompanyNotEntitled()
    {
        // BUG REPRODUCER: CompanyOwner on Free plan should NOT be able to access paid modules.
        // Current bug: IsCompanyAdmin() returns early, bypassing CanUseModuleAsync() check.
        // Expected: 403 Module Not Available
        // Bug behavior: Returns without checking entitlements (403 never thrown)
        
        var entitlementResolver = new Mock<IEntitlementResolver>();
        entitlementResolver
            .Setup(x => x.CanUseModuleAsync(_companyId, "automation"))
            .ReturnsAsync(false); // Company NOT entitled to this module

        var accessResolver = new Mock<IAccessResolver>();

        var context = CreateAuthorizationFilterContext("CompanyOwner", _companyId, _userId);
        RegisterServices(context.HttpContext.RequestServices, entitlementResolver.Object, accessResolver.Object);

        var attr = new RequireModuleAttribute("automation");
        
        await attr.OnAuthorizationAsync(context);

        context.Result.Should().NotBeNull();
        context.Result.Should().BeAssignableTo<ObjectResult>();
        var objectResult = (ObjectResult)context.Result!;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CompanyAdmin_CanAccessModule_WhenCompanyIsEntitled()
    {
        // CompanyAdmin SHOULD be able to access modules their company is entitled to.
        // This tests the positive case - entitlements are checked.
        
        var entitlementResolver = new Mock<IEntitlementResolver>();
        entitlementResolver
            .Setup(x => x.CanUseModuleAsync(_companyId, "inbox-guardian"))
            .ReturnsAsync(true); // Company IS entitled to this module

        var accessResolver = new Mock<IAccessResolver>();

        var context = CreateAuthorizationFilterContext("CompanyAdmin", _companyId, _userId);
        RegisterServices(context.HttpContext.RequestServices, entitlementResolver.Object, accessResolver.Object);

        var attr = new RequireModuleAttribute("inbox-guardian");
        
        await attr.OnAuthorizationAsync(context);

        // CompanyAdmin bypasses user-level permission checks after passing billing check
        context.Result.Should().BeNull("CompanyAdmin should pass when company is entitled");
    }

    [Fact]
    public async Task Operator_CannotAccessModule_WhenCompanyNotEntitled()
    {
        // Regular operators should also be blocked when company lacks entitlement.
        
        var entitlementResolver = new Mock<IEntitlementResolver>();
        entitlementResolver
            .Setup(x => x.CanUseModuleAsync(_companyId, "inbox-guardian"))
            .ReturnsAsync(false);

        var accessResolver = new Mock<IAccessResolver>();

        var context = CreateAuthorizationFilterContext("Operator", _companyId, _userId);
        RegisterServices(context.HttpContext.RequestServices, entitlementResolver.Object, accessResolver.Object);

        var attr = new RequireModuleAttribute("inbox-guardian");
        
        await attr.OnAuthorizationAsync(context);

        context.Result.Should().NotBeNull();
        context.Result.Should().BeAssignableTo<ObjectResult>();
        var objectResult = (ObjectResult)context.Result!;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Operator_CannotAccessModule_WhenUserLacksPermission()
    {
        // Even if company is entitled, user-level permission check should block access.
        
        var entitlementResolver = new Mock<IEntitlementResolver>();
        entitlementResolver
            .Setup(x => x.CanUseModuleAsync(_companyId, "inbox-guardian"))
            .ReturnsAsync(true); // Company entitled

        var accessResolver = new Mock<IAccessResolver>();
        accessResolver
            .Setup(x => x.GetEffectivePermissionsAsync(_userId, _companyId, "inbox-guardian"))
            .ReturnsAsync(new ModulePermissionResult(
                CanView: false,
                CanUse: false,
                CanTest: false,
                CanConfigure: false,
                CanManageIntegrations: false,
                CanManagePrompts: false,
                CanViewLogs: false,
                IsAdmin: false
            )); // User lacks permission

        var context = CreateAuthorizationFilterContext("Operator", _companyId, _userId);
        RegisterServices(context.HttpContext.RequestServices, entitlementResolver.Object, accessResolver.Object);

        var attr = new RequireModuleAttribute("inbox-guardian");
        
        await attr.OnAuthorizationAsync(context);

        context.Result.Should().NotBeNull();
        context.Result.Should().BeAssignableTo<ObjectResult>();
        var objectResult = (ObjectResult)context.Result!;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SuperAdmin_BypassesAllChecks()
    {
        // Platform admins should bypass all module checks.
        
        var entitlementResolver = new Mock<IEntitlementResolver>();
        var accessResolver = new Mock<IAccessResolver>();

        var context = CreateAuthorizationFilterContext("SuperAdmin", _companyId, _userId);
        RegisterServices(context.HttpContext.RequestServices, entitlementResolver.Object, accessResolver.Object);

        var attr = new RequireModuleAttribute("any-module");
        
        await attr.OnAuthorizationAsync(context);

        // No checks should have been called
        context.Result.Should().BeNull("SuperAdmin should bypass all checks");
    }

    [Fact]
    public async Task Operator_WithMissingSubClaim_FailsClosed()
    {
        var entitlementResolver = new Mock<IEntitlementResolver>();
        entitlementResolver.Setup(x => x.CanUseModuleAsync(_companyId, "knowledge")).ReturnsAsync(true);

        var accessResolver = new Mock<IAccessResolver>();
        var context = CreateAuthorizationFilterContextWithoutSub("Operator", _companyId);
        RegisterServices(context.HttpContext.RequestServices, entitlementResolver.Object, accessResolver.Object);

        var attr = new RequireModuleAttribute("knowledge");
        await attr.OnAuthorizationAsync(context);

        context.Result.Should().BeAssignableTo<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Operator_WithMissingAccessResolver_FailsClosed()
    {
        var entitlementResolver = new Mock<IEntitlementResolver>();
        entitlementResolver.Setup(x => x.CanUseModuleAsync(_companyId, "knowledge")).ReturnsAsync(true);

        var context = CreateAuthorizationFilterContext("Operator", _companyId, _userId);
        RegisterServices(context.HttpContext.RequestServices, entitlementResolver.Object, accessResolver: null);

        var attr = new RequireModuleAttribute("knowledge");
        await attr.OnAuthorizationAsync(context);

        context.Result.Should().BeAssignableTo<StatusCodeResult>();
        ((StatusCodeResult)context.Result!).StatusCode.Should().Be(500);
    }

    private AuthorizationFilterContext CreateAuthorizationFilterContext(string role, Guid companyId, Guid userId)
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("Role", role),
            new("ActiveCompanyId", companyId.ToString()),
            new("TenantId", companyId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContext.RequestServices = new TestServiceProvider();

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private AuthorizationFilterContext CreateAuthorizationFilterContextWithoutSub(string role, Guid companyId)
    {
        var claims = new List<Claim>
        {
            new("Role", role),
            new("ActiveCompanyId", companyId.ToString()),
            new("TenantId", companyId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContext.RequestServices = new TestServiceProvider();

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static void RegisterServices(IServiceProvider services, IEntitlementResolver entitlementResolver, IAccessResolver? accessResolver)
    {
        var testProvider = services as TestServiceProvider;
        testProvider?.Register<IEntitlementResolver>(entitlementResolver);
        if (accessResolver != null)
            testProvider?.Register<IAccessResolver>(accessResolver);
    }
}

// Test service provider for registering mocks
public class TestServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance;
    }

    public object? GetService(Type serviceType)
    {
        return _services.TryGetValue(serviceType, out var service) ? service : null;
    }
}
