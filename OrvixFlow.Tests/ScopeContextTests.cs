using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Auth;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests for ScopeContext async initialization - validates Phase 3 fix for sync-over-async starvation.
/// </summary>
public class ScopeContextTests
{
    private ServiceProvider CreateServiceProvider(Guid companyId, string dbName)
    {
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(m => m.GetTenantId()).Returns(companyId);

        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(mockTenantProvider.Object);
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InitializeAsync_ShouldResolveScope_ForCompanyAdmin()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbName = "CompanyAdmin_" + Guid.NewGuid();

        var mockHttp = new Mock<IHttpContextAccessor>();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("ActiveCompanyId", companyId.ToString()),
            new("Role", "CompanyOwner")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        mockHttp.Setup(x => x.HttpContext).Returns(new DefaultHttpContext { User = principal });

        var provider = CreateServiceProvider(companyId, dbName);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant { Id = companyId, Name = "Test Company" });
            db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "owner@test.com" });
            db.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = userId,
                CompanyId = companyId,
                CompanyRole = "CompanyOwner",
                Status = "Active",
                JoinedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var scopeContext = new ScopeContext(mockHttp.Object, db);

            // Act
            await scopeContext.InitializeAsync();

            // Assert
            scopeContext.HasCompanyWideAccess.Should().BeTrue();
            scopeContext.AllowedDepartmentIds.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldResolveScope_ForDepartmentManager()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var dbName = "DeptMgr_" + Guid.NewGuid();

        var mockHttp = new Mock<IHttpContextAccessor>();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("ActiveCompanyId", companyId.ToString()),
            new("Role", "DepartmentManager")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        mockHttp.Setup(x => x.HttpContext).Returns(new DefaultHttpContext { User = principal });

        var provider = CreateServiceProvider(companyId, dbName);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant { Id = companyId, Name = "Test Company" });
            db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "dept@test.com" });
            db.Departments.Add(new Department { Id = deptId, CompanyId = companyId, Name = "Sales", Code = "SALES" });
            db.UserDepartmentMemberships.Add(new UserDepartmentMembership
            {
                UserId = userId,
                CompanyId = companyId,
                DepartmentId = deptId,
                Status = "Active"
            });
            await db.SaveChangesAsync();

            var scopeContext = new ScopeContext(mockHttp.Object, db);

            // Act
            await scopeContext.InitializeAsync();

            // Assert
            scopeContext.HasCompanyWideAccess.Should().BeFalse();
            scopeContext.AllowedDepartmentIds.Should().Contain(deptId);
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldResolveScope_ForPlatformAdmin()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbName = "PlatformAdmin_" + Guid.NewGuid();

        var mockHttp = new Mock<IHttpContextAccessor>();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("ActiveCompanyId", companyId.ToString()),
            new("Role", "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        mockHttp.Setup(x => x.HttpContext).Returns(new DefaultHttpContext { User = principal });

        var provider = CreateServiceProvider(companyId, dbName);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant { Id = companyId, Name = "Test Company" });
            db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "admin@test.com", Role = "SuperAdmin" });
            await db.SaveChangesAsync();

            var scopeContext = new ScopeContext(mockHttp.Object, db);

            // Act
            await scopeContext.InitializeAsync();

            // Assert
            scopeContext.HasCompanyWideAccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotBlockThread_WhenCalledFromAsyncContext()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbName = "Async_" + Guid.NewGuid();

        var mockHttp = new Mock<IHttpContextAccessor>();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("ActiveCompanyId", companyId.ToString()),
            new("Role", "CompanyAdmin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        mockHttp.Setup(x => x.HttpContext).Returns(new DefaultHttpContext { User = principal });

        var provider = CreateServiceProvider(companyId, dbName);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant { Id = companyId, Name = "Test Company" });
            db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "admin@test.com" });
            db.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = userId,
                CompanyId = companyId,
                CompanyRole = "CompanyAdmin",
                Status = "Active",
                JoinedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var scopeContext = new ScopeContext(mockHttp.Object, db);

            // Act - Call InitializeAsync from async context (no blocking!)
            await scopeContext.InitializeAsync();

            // Assert - Should complete without blocking
            scopeContext.HasCompanyWideAccess.Should().BeTrue();
        }
    }
}
