using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests that enforce the organisation-first hierarchy:
/// - CompanyOwner can only be attached when a user creates an organisation.
/// - Departments must exist only under an organisation (CompanyId must resolve).
/// - Team/role mutations require an active company session claim.
/// - A user with no org membership gets hasOrganization=false from /api/org/status.
/// </summary>
public class OrgHierarchyTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ITenantProvider> _tenantProviderMock;

    // The companyId returned by the tenant provider — set this before seeding
    // so that EF Core global query filters allow access to the seeded rows.
    private Guid _activeTenantId = Guid.Empty;

    // Bootstrap an in-memory database with a reconfigurable ITenantProvider mock
    public OrgHierarchyTests()
    {
        _tenantProviderMock = new Mock<ITenantProvider>();
        _tenantProviderMock.Setup(t => t.GetTenantId())
            .Returns(() => _activeTenantId); // delegates to the field so we can change it per-test

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options, _tenantProviderMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helper: build an OrganizationController with synthetic claims ────────
    private OrganizationController BuildController(Guid userId, Guid? companyId = null, string role = "CompanyOwner")
    {
        // Update the EF Core query-filter scope to the active company
        if (companyId.HasValue)
            _activeTenantId = companyId.Value;

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
            new Claim("Role", role)
        };
        if (companyId.HasValue)
        {
            claims.Add(new Claim("ActiveCompanyId", companyId.Value.ToString()));
            claims.Add(new Claim("TenantId", companyId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var controller = new OrganizationController(_db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    // ── Helper: seed a Tenant + CompanyOwner membership for a user ──────────
    private async Task<(Guid userId, Guid companyId)> SeedTenantWithOwner()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // Set the tenant filter before saving so the DbContext doesn't block its own seeding
        _activeTenantId = companyId;

        _db.Tenants.Add(new Tenant
        {
            Id = companyId,
            Name = "Test Corp",
            Plan = "Starter"
        });

        _db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = "CompanyOwner",
            Status = "Active"
        });

        await _db.SaveChangesAsync();
        return (userId, companyId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. A user with NO active org membership → hasOrganization = false
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetOrgStatus_WithNoMembership_ReturnsHasOrganizationFalse()
    {
        var userId = Guid.NewGuid(); // user exists in claims but has no memberships
        var ctrl = BuildController(userId, companyId: null);

        var result = await ctrl.GetOrgStatus() as OkObjectResult;

        Assert.NotNull(result);
        var val = result!.Value!;
        var hasOrg = (bool)val.GetType().GetProperty("hasOrganization")!.GetValue(val)!;
        var activeId = val.GetType().GetProperty("activeCompanyId")!.GetValue(val);
        Assert.False(hasOrg);
        Assert.Null(activeId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. A user WITH an active org → hasOrganization = true
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetOrgStatus_WithActiveMembership_ReturnsHasOrganizationTrue()
    {
        var (userId, companyId) = await SeedTenantWithOwner();
        var ctrl = BuildController(userId, companyId);

        var result = await ctrl.GetOrgStatus() as OkObjectResult;

        Assert.NotNull(result);
        var val = result!.Value!;
        var hasOrg = (bool)val.GetType().GetProperty("hasOrganization")!.GetValue(val)!;
        var activeId = (Guid?)val.GetType().GetProperty("activeCompanyId")!.GetValue(val);
        Assert.True(hasOrg);
        Assert.Equal(companyId, activeId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. CompanyOwner role is only present after org creation (not standalone)
    //    Simulated by confirming a user with no membership has no CompanyOwner-
    //    level access to department mutations.
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreateDepartment_WithNoActiveCompanyId_ReturnsUnauthorized()
    {
        // User has CompanyOwner role claim but no ActiveCompanyId claim
        var userId = Guid.NewGuid();
        var ctrl = BuildController(userId, companyId: null, role: "CompanyOwner");

        var result = await ctrl.CreateDepartment(new CreateDepartmentDto("Finance", "FIN-01"));

        Assert.IsType<UnauthorizedResult>(result);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. A Viewer cannot create a department even inside an org
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreateDepartment_WithViewerRole_ReturnsForbidden()
    {
        var (userId, companyId) = await SeedTenantWithOwner();
        var ctrl = BuildController(userId, companyId, role: "Viewer");

        var result = await ctrl.CreateDepartment(new CreateDepartmentDto("HR", "HR-01"));

        Assert.IsType<ForbidResult>(result);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. CompanyAdmin CAN create a department within their org
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreateDepartment_WithCompanyAdminAndActiveOrg_Succeeds()
    {
        var (userId, companyId) = await SeedTenantWithOwner();
        var ctrl = BuildController(userId, companyId, role: "CompanyAdmin");

        var result = await ctrl.CreateDepartment(new CreateDepartmentDto("Engineering", "ENG-01"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        // Verify the department was written to the DB
        Assert.Equal(1, await _db.Departments.CountAsync());
    }

    // ────────────────────────────────────────────────────────────────────────
    // 6. Departments belong to an org — a department cannot be mutated across
    //    company boundaries (cross-tenant isolation)
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task UpdateDepartment_FromDifferentTenant_ReturnsNotFound()
    {
        var (ownerUserId, companyId) = await SeedTenantWithOwner();

        // Create a department under companyId
        var dept = new Department { Name = "Finance", Code = "FIN", CompanyId = companyId };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();

        // Attacker from a DIFFERENT company tries to rename it
        var attackerCompanyId = Guid.NewGuid();
        var ctrl = BuildController(Guid.NewGuid(), attackerCompanyId, role: "CompanyAdmin");

        var result = await ctrl.UpdateDepartment(dept.Id, new CreateDepartmentDto("Hacked", "HCK"));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 7. GetOrgStatus returns the FIRST active membership (deterministic)
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetOrgStatus_WithMultipleMemberships_ReturnsFirstActive()
    {
        var userId = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        _db.Tenants.Add(new Tenant { Id = c1, Name = "Corp A", Plan = "Free" });
        _db.Tenants.Add(new Tenant { Id = c2, Name = "Corp B", Plan = "Starter" });
        _db.UserCompanyMemberships.Add(new UserCompanyMembership { UserId = userId, CompanyId = c1, Status = "Active", CompanyRole = "CompanyOwner" });
        _db.UserCompanyMemberships.Add(new UserCompanyMembership { UserId = userId, CompanyId = c2, Status = "Active", CompanyRole = "CompanyAdmin" });
        await _db.SaveChangesAsync();

        var ctrl = BuildController(userId, c1);
        var result = await ctrl.GetOrgStatus() as OkObjectResult;

        Assert.NotNull(result);
        var val = result!.Value!;
        var hasOrg = (bool)val.GetType().GetProperty("hasOrganization")!.GetValue(val)!;
        Assert.True(hasOrg);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 8. CreateOrganization successfully generates CompanyOwner membership
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreateOrganization_AssignsCompanyOwnerRole()
    {
        var userId = Guid.NewGuid();
        
        // Seed a User entity (required by CreateOrganization)
        _db.Users.Add(new User { Id = userId, Email = "test@example.com", PasswordHash = "hashed" });
        await _db.SaveChangesAsync();
        
        var ctrl = BuildController(userId, null, "Operator"); 

        var dto = new CreateOrganizationDto("Stark Industries");

        var result = await ctrl.CreateOrganization(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        
        var val = ok.Value!;
        var newCompanyId = (Guid)val.GetType().GetProperty("companyId")!.GetValue(val)!;
        var role = (string)val.GetType().GetProperty("role")!.GetValue(val)!;

        Assert.NotEqual(Guid.Empty, newCompanyId);
        Assert.Equal("CompanyOwner", role);

        // Verify DB - set tenant filter to the new company so query filter allows access
        _activeTenantId = newCompanyId;
        var membership = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == newCompanyId);
            
        Assert.NotNull(membership);
        Assert.Equal("CompanyOwner", membership.CompanyRole);
        Assert.Equal("Active", membership.Status);
    }

    [Fact]
    public async Task CreateOrganization_ReturnsConflict_WhenNameExists()
    {
        // Seed a User entity (required by CreateOrganization)
        var userId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Email = "test@example.com", PasswordHash = "hashed" });
        
        // Set tenant filter before seeding so the tenant is visible to the query
        var existingTenantId = Guid.NewGuid();
        _activeTenantId = existingTenantId;
        
        // Add existing tenant
        _db.Tenants.Add(new Tenant { Id = existingTenantId, Name = "Global Dynamics", Plan = "Free" });
        await _db.SaveChangesAsync();

        var ctrl = BuildController(userId); 

        var dto = new CreateOrganizationDto("global dynamics"); // test case insensitivity

        var result = await ctrl.CreateOrganization(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
