using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Tests;

public class TeamControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly MockTenantProvider _tenantProvider;

    public TeamControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = new MockTenantProvider();
        _db = new AppDbContext(options, _tenantProvider);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetTeam_CompanyMember_NoDeptManager_Returns403()
    {
        var companyId = Guid.NewGuid();
        var departmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(callerId, companyId, departmentId, "DepartmentOperator");

        var controller = BuildController(callerId, companyId, UserRole.CompanyMember);

        var result = await controller.GetTeamMembers();

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetTeam_CompanyMember_IsDeptManager_ReturnsOwnDeptMembers()
    {
        var companyId = Guid.NewGuid();
        var managedDepartmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");
        var otherDepartmentId = await SeedDepartmentAsync(companyId, "Sales", "SLS");
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(callerId, companyId, managedDepartmentId, "DepartmentManager");

        var visibleUserId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(visibleUserId, companyId, managedDepartmentId, "DepartmentOperator");

        var hiddenUserId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(hiddenUserId, companyId, otherDepartmentId, "DepartmentOperator");

        var controller = BuildController(callerId, companyId, UserRole.CompanyMember);

        var result = await controller.GetTeamMembers();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var members = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();
        members.Should().HaveCount(2);
        members.Should().Contain(x => x.ToString()!.Contains(visibleUserId.ToString(), StringComparison.Ordinal));
        members.Should().NotContain(x => x.ToString()!.Contains(hiddenUserId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompanyAdmin_CanPromoteCompanyMember_ToCompanyAdmin_WithoutOverwritingDeptRole()
    {
        var companyId = Guid.NewGuid();
        var departmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(targetId, companyId, departmentId, "DepartmentOperator");
        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.UpdateRole(targetId, new UpdateRoleDto(UserRole.CompanyAdmin.ToClaimValue()));

        result.Should().BeOfType<OkObjectResult>();
        var membership = await _db.UserCompanyMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId);
        membership.CompanyRole.Should().Be(UserRole.CompanyAdmin.ToClaimValue());
        var departmentMembership = await _db.UserDepartmentMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId && m.DepartmentId == departmentId);
        departmentMembership.DepartmentRole.Should().Be("DepartmentOperator");
    }

    [Fact]
    public async Task UpdateRole_RejectsPlatformRoles_InCompanyMemberships()
    {
        var companyId = Guid.NewGuid();
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.UpdateRole(targetId, new UpdateRoleDto(UserRole.SuperAdmin.ToClaimValue()));

        result.Should().BeOfType<BadRequestObjectResult>();
        var membership = await _db.UserCompanyMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId);
        membership.CompanyRole.Should().Be(UserRole.CompanyMember.ToClaimValue());
    }

    [Fact]
    public async Task CompanyAdmin_CannotModify_CompanyAdminPeer()
    {
        var companyId = Guid.NewGuid();
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.UpdateRole(targetId, new UpdateRoleDto(UserRole.CompanyMember.ToClaimValue()));

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RemoveMember_DeactivatesCompanyAndDepartmentMemberships()
    {
        var companyId = Guid.NewGuid();
        var departmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");

        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(targetId, companyId, departmentId, "DepartmentOperator");

        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.RemoveMember(targetId);

        result.Should().BeOfType<OkObjectResult>();
        var membership = await _db.UserCompanyMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId);
        membership.Status.Should().Be("Inactive");
        var departmentMembership = await _db.UserDepartmentMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId);
        departmentMembership.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task UpdateDepartments_ReconcilesDepartmentMemberships_ForCompanyAdmins()
    {
        var companyId = Guid.NewGuid();
        var existingDepartmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");
        var newDepartmentId = await SeedDepartmentAsync(companyId, "Sales", "SALES");

        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(targetId, companyId, existingDepartmentId, "DepartmentOperator");

        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.UpdateDepartments(targetId, new UpdateDepartmentsDto([newDepartmentId]));

        result.Should().BeOfType<OkObjectResult>();
        var memberships = await _db.UserDepartmentMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == targetId && m.CompanyId == companyId)
            .ToListAsync();
        memberships.Should().ContainSingle(m => m.DepartmentId == newDepartmentId && m.Status == "Active");
        memberships.Should().ContainSingle(m => m.DepartmentId == existingDepartmentId && m.Status == "Inactive");
    }

    [Fact]
    public async Task UpdateDepartmentRole_CompanyMember_DeptManager_OwnDept_CanAssignOperatorOnly()
    {
        var companyId = Guid.NewGuid();
        var departmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(callerId, companyId, departmentId, "DepartmentManager");
        await SeedDepartmentMembershipAsync(targetId, companyId, departmentId, "DepartmentOperator");

        var controller = BuildController(callerId, companyId, UserRole.CompanyMember);

        var result = await controller.UpdateDepartmentRole(targetId, new UpdateDepartmentRoleDto(departmentId, "DepartmentOperator"));

        result.Should().BeOfType<OkObjectResult>();
        var membership = await _db.UserDepartmentMemberships.FirstAsync(m => m.UserId == targetId && m.DepartmentId == departmentId);
        membership.DepartmentRole.Should().Be("DepartmentOperator");
    }

    [Fact]
    public async Task UpdateDepartmentRole_CompanyMember_DeptManager_OtherDept_Returns403()
    {
        var companyId = Guid.NewGuid();
        var managedDepartmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");
        var otherDepartmentId = await SeedDepartmentAsync(companyId, "Sales", "SLS");
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(callerId, companyId, managedDepartmentId, "DepartmentManager");
        await SeedDepartmentMembershipAsync(targetId, companyId, otherDepartmentId, "DepartmentOperator");

        var controller = BuildController(callerId, companyId, UserRole.CompanyMember);

        var result = await controller.UpdateDepartmentRole(targetId, new UpdateDepartmentRoleDto(otherDepartmentId, "DepartmentManager"));

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdateDepartmentRole_CompanyMember_DeptManager_CannotPromoteToManager()
    {
        var companyId = Guid.NewGuid();
        var departmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(callerId, companyId, departmentId, "DepartmentManager");
        await SeedDepartmentMembershipAsync(targetId, companyId, departmentId, "DepartmentOperator");

        var controller = BuildController(callerId, companyId, UserRole.CompanyMember);

        var result = await controller.UpdateDepartmentRole(targetId, new UpdateDepartmentRoleDto(departmentId, "DepartmentManager"));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
        var membership = await _db.UserDepartmentMemberships.FirstAsync(m => m.UserId == targetId && m.DepartmentId == departmentId);
        membership.DepartmentRole.Should().Be("DepartmentOperator");
    }

    [Fact]
    public async Task UpdateDepartmentRole_CompanyMember_DeptManager_CannotDemoteExistingManager()
    {
        var companyId = Guid.NewGuid();
        var departmentId = await SeedDepartmentAsync(companyId, "Ops", "OPS");
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyMember);
        await SeedDepartmentMembershipAsync(callerId, companyId, departmentId, "DepartmentManager");
        await SeedDepartmentMembershipAsync(targetId, companyId, departmentId, "DepartmentManager");

        var controller = BuildController(callerId, companyId, UserRole.CompanyMember);

        var result = await controller.UpdateDepartmentRole(targetId, new UpdateDepartmentRoleDto(departmentId, "DepartmentOperator"));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
        var membership = await _db.UserDepartmentMemberships.FirstAsync(m => m.UserId == targetId && m.DepartmentId == departmentId);
        membership.DepartmentRole.Should().Be("DepartmentManager");
    }

    private TeamController BuildController(Guid userId, Guid companyId, UserRole role)
    {
        _tenantProvider.SetTenantId(companyId);

        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("Role", role.ToClaimValue()),
            new Claim("ActiveCompanyId", companyId.ToString()),
            new Claim("TenantId", companyId.ToString())
        };

        var controller = new TeamController(_db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };

        return controller;
    }

    private async Task<Guid> SeedMemberAsync(Guid companyId, UserRole role)
    {
        var userId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = userId,
            Email = $"{Guid.NewGuid()}@test.com",
            DisplayName = role.ToClaimValue(),
            TenantId = companyId,
            Role = string.Empty,
            EmailVerified = true
        });
        _db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = role.ToClaimValue(),
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return userId;
    }

    private async Task<Guid> SeedDepartmentAsync(Guid companyId, string name, string code)
    {
        var departmentId = Guid.NewGuid();
        _db.Departments.Add(new Department { Id = departmentId, CompanyId = companyId, Name = name, Code = code });
        await _db.SaveChangesAsync();
        return departmentId;
    }

    private async Task SeedDepartmentMembershipAsync(Guid userId, Guid companyId, Guid departmentId, string role)
    {
        _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
        {
            UserId = userId,
            CompanyId = companyId,
            DepartmentId = departmentId,
            DepartmentRole = role,
            Status = "Active"
        });
        await _db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private Guid _tenantId;

        public void SetTenantId(Guid tenantId) => _tenantId = tenantId;

        public Guid GetTenantId() => _tenantId;
    }
}
