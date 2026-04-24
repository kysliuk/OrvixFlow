using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Auth;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class AccessResolverTests
{
    [Fact]
    public async Task Should_Union_Department_Module_Permissions()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var departmentA = Guid.NewGuid();
        var departmentB = Guid.NewGuid();

        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(m => m.GetTenantId()).Returns(companyId);

        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(mockTenantProvider.Object);
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("AccessResolverDb_" + Guid.NewGuid()));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant { Id = companyId, Name = "C1" });
        db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "u@x.com" });
        db.Departments.AddRange(
            new Department { Id = departmentA, CompanyId = companyId, Name = "A", Code = "a" },
            new Department { Id = departmentB, CompanyId = companyId, Name = "B", Code = "b" }
        );
        db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = "CompanyMember",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        db.UserDepartmentMemberships.AddRange(
            new UserDepartmentMembership { UserId = userId, CompanyId = companyId, DepartmentId = departmentA, Status = "Active", DepartmentRole = "DepartmentOperator" },
            new UserDepartmentMembership { UserId = userId, CompanyId = companyId, DepartmentId = departmentB, Status = "Active", DepartmentRole = "DepartmentOperator" }
        );
        var module = new ModuleDefinition { Key = "inbox-guardian", DisplayName = "Inbox", Tier = "Utility", Visibility = "UserFacing", IsActive = true };
        db.ModuleDefinitions.Add(module);
        await db.SaveChangesAsync();

        var assignmentA = new ModuleAssignment
        {
            CompanyId = companyId,
            ModuleDefinitionId = module.Id,
            DepartmentId = departmentA,
            Scope = "Department",
            IsEnabled = true
        };
        var assignmentB = new ModuleAssignment
        {
            CompanyId = companyId,
            ModuleDefinitionId = module.Id,
            DepartmentId = departmentB,
            Scope = "Department",
            IsEnabled = true
        };
        db.ModuleAssignments.AddRange(assignmentA, assignmentB);
        await db.SaveChangesAsync();

        db.ModulePermissionGrants.AddRange(
            new ModulePermissionGrant { ModuleAssignmentId = assignmentA.Id, CanView = true, CanUse = false, CanViewLogs = true },
            new ModulePermissionGrant { ModuleAssignmentId = assignmentB.Id, CanView = true, CanUse = true, CanViewLogs = false }
        );
        await db.SaveChangesAsync();

        var mockScope = new Mock<IScopeContext>();
        mockScope.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockScope.Setup(s => s.CompanyId).Returns(companyId);
        mockScope.Setup(s => s.HasCompanyWideAccess).Returns(false);
        mockScope.Setup(s => s.AllowedDepartmentIds).Returns(new List<Guid> { departmentA, departmentB }.AsReadOnly());

        var mockEntitlements = new Mock<IEntitlementResolver>();
        mockEntitlements.Setup(e => e.CanUseModuleWithOverridesAsync(companyId, "inbox-guardian")).ReturnsAsync(true);

        var resolver = new AccessResolver(db, mockScope.Object, mockEntitlements.Object);
        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");
        permissions.CanView.Should().BeTrue();
        permissions.CanUse.Should().BeTrue();
        permissions.CanViewLogs.Should().BeTrue();
    }

    [Fact]
    public async Task CompanyMember_DeptOperator_FallbackGrant_AllowsUse_WhenEntitled()
    {
        var (db, resolver, companyId, userId) = await CreateFallbackResolverAsync("DepartmentOperator");

        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");

        permissions.CanView.Should().BeTrue();
        permissions.CanUse.Should().BeTrue();
        db.Dispose();
    }

    [Fact]
    public async Task CompanyMember_DeptManager_FallbackGrant_AllowsUse_WhenEntitled()
    {
        var (db, resolver, companyId, userId) = await CreateFallbackResolverAsync("DepartmentManager");

        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");

        permissions.CanView.Should().BeTrue();
        permissions.CanUse.Should().BeTrue();
        db.Dispose();
    }

    private static async Task<(AppDbContext db, AccessResolver resolver, Guid companyId, Guid userId)> CreateFallbackResolverAsync(string departmentRole)
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options, Mock.Of<ITenantProvider>(x => x.GetTenantId() == companyId));
        db.Tenants.Add(new Tenant { Id = companyId, Name = "Fallback" });
        db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "member@test.com", Role = string.Empty });
        db.Departments.Add(new Department { Id = departmentId, CompanyId = companyId, Name = "Ops", Code = "OPS" });
        db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = "CompanyMember",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        db.UserDepartmentMemberships.Add(new UserDepartmentMembership
        {
            UserId = userId,
            CompanyId = companyId,
            DepartmentId = departmentId,
            DepartmentRole = departmentRole,
            Status = "Active"
        });
        db.ModuleDefinitions.Add(new ModuleDefinition { Key = "inbox-guardian", DisplayName = "Inbox", Tier = "Utility", Visibility = "UserFacing", IsActive = true });
        await db.SaveChangesAsync();

        var scope = new Mock<IScopeContext>();
        scope.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        scope.Setup(s => s.HasCompanyWideAccess).Returns(false);
        scope.Setup(s => s.AllowedDepartmentIds).Returns(new List<Guid> { departmentId }.AsReadOnly());

        var entitlements = new Mock<IEntitlementResolver>();
        entitlements.Setup(e => e.CanUseModuleWithOverridesAsync(companyId, "inbox-guardian")).ReturnsAsync(true);

        return (db, new AccessResolver(db, scope.Object, entitlements.Object), companyId, userId);
    }
}
