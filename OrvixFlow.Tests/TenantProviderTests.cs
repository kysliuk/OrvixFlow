using System;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class TenantProviderTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();

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

        var mockLogger = new Mock<ILogger<TenantProvider>>();
        var provider = new TenantProvider(mockHttpContextAccessor.Object, mockLogger.Object);

        // Act
        var result = provider.GetTenantId();

        // Assert
        result.Should().Be(tenantId);
    }
    
    [Fact]
    public void Should_Prefer_ActiveCompanyId_Claim_When_Present()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var activeCompanyId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("TenantId", tenantId.ToString()),
            new Claim("ActiveCompanyId", activeCompanyId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(c => c.User).Returns(claimsPrincipal);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        var mockLogger = new Mock<ILogger<TenantProvider>>();
        var provider = new TenantProvider(mockHttpContextAccessor.Object, mockLogger.Object);

        // Act
        var result = provider.GetTenantId();

        // Assert
        result.Should().Be(activeCompanyId);
    }

    // F-09: Test the vulnerability - when there's NO JWT claim but X-Tenant-ID header is present
    // This test should FAIL before the fix (vulnerability exists)
    // After the fix, this test should PASS (header is ignored)
    [Fact]
    public void Should_Reject_X_Tenant_ID_Header_When_No_JWT_Claim()
    {
        // Arrange
        var attackerSpoofedTenantId = Guid.NewGuid();
        
        // User has NO tenant claims in JWT (but is authenticated)
        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),  // User ID only
            new Claim("email", "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(c => c.User).Returns(claimsPrincipal);
        
        // Attacker tries to use X-Tenant-ID header to access another tenant's data
        var headers = new HeaderDictionary
        {
            { "X-Tenant-ID", attackerSpoofedTenantId.ToString() }
        };
        mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        var mockLogger = new Mock<ILogger<TenantProvider>>();
        var provider = new TenantProvider(mockHttpContextAccessor.Object, mockLogger.Object);

        // Act
        var result = provider.GetTenantId();

        // Assert: Should return Guid.Empty (no tenant), NOT the attacker-supplied header value
        // Before fix: result == attackerSpoofedTenantId (VULNERABLE!)
        // After fix: result == Guid.Empty (SECURE)
        result.Should().Be(Guid.Empty, 
            "X-Tenant-ID header should be ignored when no JWT tenant claim exists - this is a security vulnerability (F-09)");
    }

    // F-09: Test that X-Tenant-ID header is NOT accepted from regular authenticated users with valid claims
    [Fact]
    public void Should_Reject_X_Tenant_ID_Header_Even_When_Claim_Exists()
    {
        // Arrange
        var legitimateTenantId = Guid.NewGuid();
        var attackerSpoofedTenantId = Guid.NewGuid();
        
        // User is authenticated with a legitimate tenant
        var claims = new[]
        {
            new Claim("TenantId", legitimateTenantId.ToString()),
            new Claim("ActiveCompanyId", legitimateTenantId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(c => c.User).Returns(claimsPrincipal);
        
        // Attacker tries to use X-Tenant-ID header to access another tenant's data
        var headers = new HeaderDictionary
        {
            { "X-Tenant-ID", attackerSpoofedTenantId.ToString() }
        };
        mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        var mockLogger = new Mock<ILogger<TenantProvider>>();
        var provider = new TenantProvider(mockHttpContextAccessor.Object, mockLogger.Object);

        // Act
        var result = provider.GetTenantId();

        // Assert: The provider should return the legitimate tenant from JWT claims,
        // NOT the attacker-supplied X-Tenant-ID header
        result.Should().Be(legitimateTenantId);
    }

    // F-09: Test that impersonation still works for admins
    [Fact]
    public void Should_Allow_Impersonation_For_SuperAdmin()
    {
        // Arrange
        var adminTenantId = Guid.NewGuid();
        var targetTenantId = Guid.NewGuid();
        
        var claims = new[]
        {
            new Claim("TenantId", adminTenantId.ToString()),
            new Claim("ActiveCompanyId", adminTenantId.ToString()),
            new Claim("Role", "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(c => c.User).Returns(claimsPrincipal);
        
        var headers = new HeaderDictionary
        {
            { "X-Impersonate-Tenant", targetTenantId.ToString() }
        };
        mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        var mockLogger = new Mock<ILogger<TenantProvider>>();
        var provider = new TenantProvider(mockHttpContextAccessor.Object, mockLogger.Object);

        // Act
        var result = provider.GetTenantId();

        // Assert: Admin impersonation should work
        result.Should().Be(targetTenantId);
    }

    // F-09: Test that X-Tenant-ID is still accepted for webhook paths (if applicable)
    // Note: This would require the TenantProvider to be path-aware, which is a Phase 2 change.
    // For Phase 1, we remove the fallback entirely.

    public void Dispose()
    {
        // Cleanup if needed
    }
}
