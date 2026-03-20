using System;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using OrvixFlow.Api.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class TenantProviderTests
{
    [Fact]
    public void Should_Resolve_Tenant_From_Claims()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var claims = new[] { new Claim("TenantId", tenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(c => c.User).Returns(claimsPrincipal);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        var provider = new TenantProvider(mockHttpContextAccessor.Object);

        // Act
        var result = provider.GetTenantId();

        // Assert
        result.Should().Be(tenantId);
    }
    
    [Fact]
    public void Should_Resolve_Tenant_From_Header_When_Claims_Missing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        
        var mockHttpContext = new DefaultHttpContext();
        mockHttpContext.Request.Headers["X-Tenant-ID"] = tenantId.ToString();

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext);

        var provider = new TenantProvider(mockHttpContextAccessor.Object);

        // Act
        var result = provider.GetTenantId();

        // Assert
        result.Should().Be(tenantId);
    }
}
