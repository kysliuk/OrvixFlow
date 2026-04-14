using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using OrvixFlow.Api.Filters;
using Xunit;

namespace OrvixFlow.Tests;

public class RequireModuleAttributeTests
{
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
}
