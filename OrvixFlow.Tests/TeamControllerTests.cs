using System;
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
    public async Task CompanyAdmin_CanPromoteOperator_ToCompanyAdmin()
    {
        var companyId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        _db.Departments.Add(new Department { Id = departmentId, CompanyId = companyId, Name = "Ops", Code = "OPS" });
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.Operator);
        _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
        {
            UserId = targetId,
            CompanyId = companyId,
            DepartmentId = departmentId,
            DepartmentRole = "Member",
            Status = "Active"
        });
        await _db.SaveChangesAsync();
        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.UpdateRole(targetId, new UpdateRoleDto(UserRole.CompanyAdmin.ToClaimValue()));

        result.Should().BeOfType<OkObjectResult>();
        var membership = await _db.UserCompanyMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId);
        membership.CompanyRole.Should().Be(UserRole.CompanyAdmin.ToClaimValue());
        var departmentMembership = await _db.UserDepartmentMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId && m.DepartmentId == departmentId);
        departmentMembership.DepartmentRole.Should().Be("Manager");
    }

    [Fact]
    public async Task UpdateRole_RejectsPlatformRoles_InCompanyMemberships()
    {
        var companyId = Guid.NewGuid();
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.Operator);
        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.UpdateRole(targetId, new UpdateRoleDto(UserRole.SuperAdmin.ToClaimValue()));

        result.Should().BeOfType<BadRequestObjectResult>();
        var membership = await _db.UserCompanyMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId);
        membership.CompanyRole.Should().Be(UserRole.Operator.ToClaimValue());
    }

    [Fact]
    public async Task CompanyAdmin_CannotModify_CompanyAdminPeer()
    {
        var companyId = Guid.NewGuid();
        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.UpdateRole(targetId, new UpdateRoleDto(UserRole.Operator.ToClaimValue()));

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RemoveMember_DeactivatesCompanyAndDepartmentMemberships()
    {
        var companyId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        _db.Departments.Add(new Department { Id = departmentId, CompanyId = companyId, Name = "Ops", Code = "OPS" });

        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.Operator);
        _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
        {
            UserId = targetId,
            CompanyId = companyId,
            DepartmentId = departmentId,
            DepartmentRole = "Member",
            Status = "Active"
        });
        await _db.SaveChangesAsync();

        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.RemoveMember(targetId);

        result.Should().BeOfType<OkObjectResult>();
        var membership = await _db.UserCompanyMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId);
        membership.Status.Should().Be("Inactive");
        var departmentMembership = await _db.UserDepartmentMemberships.FirstAsync(m => m.UserId == targetId && m.CompanyId == companyId);
        departmentMembership.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task UpdateDepartments_ReconcilesDepartmentMemberships()
    {
        var companyId = Guid.NewGuid();
        var existingDepartmentId = Guid.NewGuid();
        var newDepartmentId = Guid.NewGuid();
        _db.Departments.AddRange(
            new Department { Id = existingDepartmentId, CompanyId = companyId, Name = "Ops", Code = "OPS" },
            new Department { Id = newDepartmentId, CompanyId = companyId, Name = "Sales", Code = "SALES" });

        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.Operator);
        _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
        {
            UserId = targetId,
            CompanyId = companyId,
            DepartmentId = existingDepartmentId,
            DepartmentRole = "Member",
            Status = "Active"
        });
        await _db.SaveChangesAsync();

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
    public async Task UpdateDepartments_RejectsForeignCompanyDepartment()
    {
        var companyId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        var foreignDepartmentId = Guid.NewGuid();
        _db.Departments.Add(new Department { Id = foreignDepartmentId, CompanyId = foreignCompanyId, Name = "Foreign", Code = "FOR" });

        var callerId = await SeedMemberAsync(companyId, UserRole.CompanyAdmin);
        var targetId = await SeedMemberAsync(companyId, UserRole.Operator);
        await _db.SaveChangesAsync();

        var controller = BuildController(callerId, companyId, UserRole.CompanyAdmin);

        var result = await controller.UpdateDepartments(targetId, new UpdateDepartmentsDto([foreignDepartmentId]));

        result.Should().BeOfType<BadRequestObjectResult>();
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
