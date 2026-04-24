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

    /// <summary>
    /// DepartmentManager gets CanViewLogs=true in the fallback; DepartmentOperator does not.
    /// This verifies the fix for the previously dead isDeptManager variable.
    /// </summary>
    [Fact]
    public async Task CompanyMember_DeptManager_FallbackGrant_GetsCanViewLogs()
    {
        var (db, resolver, companyId, userId) = await CreateFallbackResolverAsync("DepartmentManager");

        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");

        permissions.CanViewLogs.Should().BeTrue("DepartmentManager should get CanViewLogs in the fallback grant");
        db.Dispose();
    }

    /// <summary>
    /// DepartmentOperator does NOT get CanViewLogs in the fallback — only View+Use.
    /// </summary>
    [Fact]
    public async Task CompanyMember_DeptOperator_FallbackGrant_DoesNotGetCanViewLogs()
    {
        var (db, resolver, companyId, userId) = await CreateFallbackResolverAsync("DepartmentOperator");

        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");

        permissions.CanViewLogs.Should().BeFalse("DepartmentOperator should not get CanViewLogs in fallback — only DepartmentManager does");
        db.Dispose();
    }

    /// <summary>
    /// CompanyMember with NO department memberships must be blocked even when company is entitled.
    /// </summary>
    [Fact]
    public async Task CompanyMember_NoDeptMemberships_Returns_Empty_EvenWhenEntitled()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options, Mock.Of<ITenantProvider>(x => x.GetTenantId() == companyId));
        db.Tenants.Add(new Tenant { Id = companyId, Name = "NoDept" });
        db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "nodept@test.com", Role = string.Empty });
        db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = "CompanyMember",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        // NO UserDepartmentMemberships
        db.ModuleDefinitions.Add(new ModuleDefinition { Key = "inbox-guardian", DisplayName = "Inbox", Tier = "Utility", Visibility = "UserFacing", IsActive = true });
        await db.SaveChangesAsync();

        var scope = new Mock<IScopeContext>();
        scope.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        scope.Setup(s => s.HasCompanyWideAccess).Returns(false);
        scope.Setup(s => s.AllowedDepartmentIds).Returns(new List<Guid>().AsReadOnly());

        var entitlements = new Mock<IEntitlementResolver>();
        entitlements.Setup(e => e.CanUseModuleWithOverridesAsync(companyId, "inbox-guardian")).ReturnsAsync(true);

        var resolver = new AccessResolver(db, scope.Object, entitlements.Object);
        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");

        permissions.CanView.Should().BeFalse("CompanyMember with no dept assignments must not get module access");
        permissions.CanUse.Should().BeFalse("CompanyMember with no dept assignments must not get module access");
        db.Dispose();
    }

    /// <summary>
    /// CompanyOwner gets full access (all permissions) without needing explicit grants.
    /// </summary>
    [Fact]
    public async Task CompanyOwner_HasFullAccess_WhenEntitled()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options, Mock.Of<ITenantProvider>(x => x.GetTenantId() == companyId));
        db.Tenants.Add(new Tenant { Id = companyId, Name = "OwnerCo" });
        db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "owner@test.com", Role = string.Empty });
        db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = "CompanyOwner",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        db.ModuleDefinitions.Add(new ModuleDefinition { Key = "inbox-guardian", DisplayName = "Inbox", Tier = "Utility", Visibility = "UserFacing", IsActive = true });
        await db.SaveChangesAsync();

        var scope = new Mock<IScopeContext>();
        scope.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        scope.Setup(s => s.HasCompanyWideAccess).Returns(true);

        var entitlements = new Mock<IEntitlementResolver>();

        var resolver = new AccessResolver(db, scope.Object, entitlements.Object);
        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");

        permissions.CanView.Should().BeTrue();
        permissions.CanUse.Should().BeTrue();
        permissions.CanConfigure.Should().BeTrue();
        permissions.IsAdmin.Should().BeTrue();
        db.Dispose();
    }

    /// <summary>
    /// CompanyAdmin gets full access (all permissions) without needing explicit grants.
    /// </summary>
    [Fact]
    public async Task CompanyAdmin_HasFullAccess_WhenEntitled()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options, Mock.Of<ITenantProvider>(x => x.GetTenantId() == companyId));
        db.Tenants.Add(new Tenant { Id = companyId, Name = "AdminCo" });
        db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "admin@test.com", Role = string.Empty });
        db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = "CompanyAdmin",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        db.ModuleDefinitions.Add(new ModuleDefinition { Key = "inbox-guardian", DisplayName = "Inbox", Tier = "Utility", Visibility = "UserFacing", IsActive = true });
        await db.SaveChangesAsync();

        var scope = new Mock<IScopeContext>();
        scope.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        scope.Setup(s => s.HasCompanyWideAccess).Returns(true);

        var entitlements = new Mock<IEntitlementResolver>();

        var resolver = new AccessResolver(db, scope.Object, entitlements.Object);
        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");

        permissions.CanView.Should().BeTrue();
        permissions.CanUse.Should().BeTrue();
        permissions.CanConfigure.Should().BeTrue();
        permissions.IsAdmin.Should().BeTrue();
        db.Dispose();
    }

    /// <summary>
    /// A user who belongs to a different company must not access modules in this company.
    /// Cross-company isolation guard.
    /// </summary>
    [Fact]
    public async Task AnotherCompanyUser_Returns_Empty()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options, Mock.Of<ITenantProvider>(x => x.GetTenantId() == companyId));
        db.Tenants.Add(new Tenant { Id = companyId, Name = "OurCo" });
        db.Tenants.Add(new Tenant { Id = otherCompanyId, Name = "OtherCo" });
        db.Users.Add(new User { Id = userId, TenantId = otherCompanyId, Email = "foreign@test.com", Role = string.Empty });
        db.Departments.Add(new Department { Id = departmentId, CompanyId = otherCompanyId, Name = "Ext", Code = "EXT" });
        db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = otherCompanyId,
            CompanyRole = "CompanyMember",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        db.UserDepartmentMemberships.Add(new UserDepartmentMembership
        {
            UserId = userId, CompanyId = otherCompanyId, DepartmentId = departmentId,
            DepartmentRole = "DepartmentOperator", Status = "Active"
        });
        db.ModuleDefinitions.Add(new ModuleDefinition { Key = "inbox-guardian", DisplayName = "Inbox", Tier = "Utility", Visibility = "UserFacing", IsActive = true });
        await db.SaveChangesAsync();

        // ScopeContext returns no departments in OUR company for this user
        var scope = new Mock<IScopeContext>();
        scope.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        scope.Setup(s => s.HasCompanyWideAccess).Returns(false);
        scope.Setup(s => s.AllowedDepartmentIds).Returns(new List<Guid>().AsReadOnly());

        var entitlements = new Mock<IEntitlementResolver>();
        entitlements.Setup(e => e.CanUseModuleWithOverridesAsync(companyId, "inbox-guardian")).ReturnsAsync(true);

        var resolver = new AccessResolver(db, scope.Object, entitlements.Object);
        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");

        permissions.CanView.Should().BeFalse("User from another company must not access this company's modules");
        permissions.CanUse.Should().BeFalse("User from another company must not access this company's modules");
        db.Dispose();
    }

    /// <summary>
    /// Inactive/disabled module is blocked even if user has valid dept membership and company is entitled.
    /// </summary>
    [Fact]
    public async Task CompanyMember_WithDept_DisabledModule_Returns_Empty()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options, Mock.Of<ITenantProvider>(x => x.GetTenantId() == companyId));
        db.Tenants.Add(new Tenant { Id = companyId, Name = "DisabledMod" });
        db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "test@test.com", Role = string.Empty });
        db.Departments.Add(new Department { Id = departmentId, CompanyId = companyId, Name = "Ops", Code = "OPS" });
        db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId, CompanyId = companyId, CompanyRole = "CompanyMember", Status = "Active", JoinedAt = DateTime.UtcNow
        });
        db.UserDepartmentMemberships.Add(new UserDepartmentMembership
        {
            UserId = userId, CompanyId = companyId, DepartmentId = departmentId,
            DepartmentRole = "DepartmentOperator", Status = "Active"
        });
        // Module is INACTIVE
        db.ModuleDefinitions.Add(new ModuleDefinition { Key = "disabled-module", DisplayName = "Disabled", Tier = "Utility", Visibility = "UserFacing", IsActive = false });
        await db.SaveChangesAsync();

        var scope = new Mock<IScopeContext>();
        scope.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        scope.Setup(s => s.HasCompanyWideAccess).Returns(false);
        scope.Setup(s => s.AllowedDepartmentIds).Returns(new List<Guid> { departmentId }.AsReadOnly());

        var entitlements = new Mock<IEntitlementResolver>();
        entitlements.Setup(e => e.CanUseModuleWithOverridesAsync(companyId, "disabled-module")).ReturnsAsync(false);

        var resolver = new AccessResolver(db, scope.Object, entitlements.Object);
        var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "disabled-module");

        permissions.CanView.Should().BeFalse("Inactive module must always be blocked");
        permissions.CanUse.Should().BeFalse("Inactive module must always be blocked");
        db.Dispose();
    }

    /// <summary>
    /// GetVisibleModulesAsync returns only entitled modules for a dept-scoped user.
    /// Non-entitled modules must not appear, even if user has dept membership.
    /// </summary>
    [Fact]
    public async Task GetVisibleModules_ForDeptMember_ReturnsOnlyEntitledModules()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options, Mock.Of<ITenantProvider>(x => x.GetTenantId() == companyId));
        db.Tenants.Add(new Tenant { Id = companyId, Name = "VisibleCo" });
        db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "visible@test.com", Role = string.Empty });
        db.Departments.Add(new Department { Id = departmentId, CompanyId = companyId, Name = "Sales", Code = "SALES" });
        db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId, CompanyId = companyId, CompanyRole = "CompanyMember", Status = "Active", JoinedAt = DateTime.UtcNow
        });
        db.UserDepartmentMemberships.Add(new UserDepartmentMembership
        {
            UserId = userId, CompanyId = companyId, DepartmentId = departmentId,
            DepartmentRole = "DepartmentOperator", Status = "Active"
        });
        db.ModuleDefinitions.AddRange(
            new ModuleDefinition { Key = "inbox-guardian", DisplayName = "Inbox", Tier = "Utility", Visibility = "UserFacing", IsActive = true },
            new ModuleDefinition { Key = "knowledge-base", DisplayName = "KB", Tier = "Utility", Visibility = "UserFacing", IsActive = true }
        );
        await db.SaveChangesAsync();

        var scope = new Mock<IScopeContext>();
        scope.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        scope.Setup(s => s.HasCompanyWideAccess).Returns(false);
        scope.Setup(s => s.AllowedDepartmentIds).Returns(new List<Guid> { departmentId }.AsReadOnly());

        var entitlements = new Mock<IEntitlementResolver>();
        // Only inbox-guardian is entitled; knowledge-base is not
        entitlements.Setup(e => e.CanUseModuleWithOverridesAsync(companyId, "inbox-guardian")).ReturnsAsync(true);
        entitlements.Setup(e => e.CanUseModuleWithOverridesAsync(companyId, "knowledge-base")).ReturnsAsync(false);

        var resolver = new AccessResolver(db, scope.Object, entitlements.Object);
        var visible = await resolver.GetVisibleModulesAsync(userId, companyId);

        visible.Should().Contain("inbox-guardian");
        visible.Should().NotContain("knowledge-base", "non-entitled module must not appear in visible list");
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
