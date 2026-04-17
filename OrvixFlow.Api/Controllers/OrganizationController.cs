using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Core.Authorization;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/org")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrganizationController> _logger;

    public OrganizationController(AppDbContext db, ILogger<OrganizationController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var userId = ParseGuid("sub");
        if (userId == null) return Unauthorized();

        var companies = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId.Value && m.Status == "Active")
            .Join(_db.Tenants, m => m.CompanyId, c => c.Id, (m, c) => new
            {
                companyId = c.Id,
                companyName = c.Name,
                role = m.CompanyRole,
                plan = c.Plan
            })
            .ToListAsync();

        return Ok(companies);
    }

    /// <summary>
    /// Returns whether the current user belongs to at least one active organisation.
    /// The frontend uses this to conditionally enable Departments, Team and Security tabs.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetOrgStatus()
    {
        var userId = ParseGuid("sub");
        if (userId == null) return Unauthorized();

        var membership = await _db.UserCompanyMemberships
            .Where(m => m.UserId == userId.Value && m.Status == "Active")
            .Join(_db.Tenants, m => m.CompanyId, c => c.Id, (m, c) => new
            {
                companyId = c.Id,
                companyName = c.Name,
                role = m.CompanyRole
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            hasOrganization = membership != null,
            activeCompanyId = membership?.companyId,
            companyName = membership?.companyName,
            role = membership?.role
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationDto dto)
    {
        var claimsDump = string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}"));
        _logger.LogDebug("Claims: {Claims}", claimsDump);

        var userId = ParseGuid("sub");
        if (userId == null) 
        {
            _logger.LogDebug("ParseGuid failed to find valid sub or NameIdentifier");
            return Unauthorized();
        }

        if (!await _db.Users.AnyAsync(u => u.Id == userId.Value))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Organization name is required." });

        if (await _db.Tenants.AnyAsync(t => t.Name.ToLower() == dto.Name.ToLower()))
            return Conflict(new { error = "An organization with this name already exists." });

        var tenant = new Core.Entities.Tenant
        {
            Name = dto.Name,
            Plan = "Trialing",
            SubscriptionStatus = "Trialing" // Default to trialing for freshly created orgs
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(); // generate tenant Id

        var membership = new Core.Entities.UserCompanyMembership
        {
            UserId = userId.Value,
            CompanyId = tenant.Id,
            CompanyRole = OrvixFlow.Core.Authorization.UserRole.CompanyOwner.ToClaimValue(),
            Status = "Active"
        };
        _db.UserCompanyMemberships.Add(membership);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            companyId = tenant.Id,
            companyName = tenant.Name,
            role = membership.CompanyRole,
            plan = tenant.Plan
        });
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var userId = ParseGuid("sub");
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (userId == null || companyId == null) return Unauthorized();

        var role = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);

        // Admins see all company departments; regular users see only their assigned ones
        if (role.IsCompanyAdminOrAbove())
        {
            var allDepts = await _db.Departments
                .Where(d => d.CompanyId == companyId.Value)
                .Select(d => new
                {
                    departmentId = d.Id,
                    name = d.Name,
                    code = d.Code,
                    role = "Admin"
                })
                .ToListAsync();
            return Ok(allDepts);
        }

        var departments = await _db.UserDepartmentMemberships
            .Where(m => m.UserId == userId.Value && m.CompanyId == companyId.Value && m.Status == "Active")
            .Join(_db.Departments, m => m.DepartmentId, d => d.Id, (m, d) => new
            {
                departmentId = d.Id,
                name = d.Name,
                code = d.Code,
                role = m.DepartmentRole
            })
            .ToListAsync();

        return Ok(departments);
    }

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto)
    {
        var role = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!role.IsCompanyAdminOrAbove()) return Forbid();

        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null) return Unauthorized();

        var department = new Core.Entities.Department
        {
            Name = dto.Name,
            Code = dto.Code,
            CompanyId = companyId.Value
        };

        _db.Departments.Add(department);
        await _db.SaveChangesAsync();

        return Ok(new { departmentId = department.Id, name = department.Name, code = department.Code });
    }

    [HttpPut("departments/{id}")]
    public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] CreateDepartmentDto dto)
    {
        var role = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!role.IsCompanyAdminOrAbove()) return Forbid();

        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null) return Unauthorized();

        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == companyId.Value);
        if (department == null) return NotFound(new { error = "Department not found." });

        department.Name = dto.Name;
        department.Code = dto.Code;
        
        await _db.SaveChangesAsync();
        return Ok(new { departmentId = department.Id, name = department.Name, code = department.Code });
    }

    [HttpDelete("departments/{id}")]
    public async Task<IActionResult> DeleteDepartment(Guid id)
    {
        var role = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!role.IsCompanyAdminOrAbove()) return Forbid();

        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null) return Unauthorized();

        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == companyId.Value);
        if (department == null) return NotFound(new { error = "Department not found." });

        _db.Departments.Remove(department);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Department deleted successfully." });
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest req)
    {
        var inviterRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!inviterRole.IsCompanyAdminOrAbove())
        {
            return Forbid();
        }

        var company = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == req.CompanyId);
        if (company == null)
        {
            return NotFound(new { error = "Company not found." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant());
        if (user == null)
        {
            user = new Core.Entities.User
            {
                Email = req.Email.ToLowerInvariant(),
                DisplayName = req.DisplayName,
                OAuthProvider = "invited",
                TenantId = req.CompanyId
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var existing = await _db.UserCompanyMemberships
            .AnyAsync(m => m.UserId == user.Id && m.CompanyId == req.CompanyId);
        if (!existing)
        {
            _db.UserCompanyMemberships.Add(new Core.Entities.UserCompanyMembership
            {
                UserId = user.Id,
                CompanyId = req.CompanyId,
                CompanyRole = req.CompanyRole,
                Status = "Invited",
                InvitedAt = DateTime.UtcNow,
                InvitedByUserId = ParseGuid("sub")
            });
        }

        foreach (var departmentId in req.DepartmentIds.Distinct())
        {
            var depExists = await _db.UserDepartmentMemberships.AnyAsync(m =>
                m.UserId == user.Id && m.CompanyId == req.CompanyId && m.DepartmentId == departmentId);
            if (!depExists)
            {
                _db.UserDepartmentMemberships.Add(new Core.Entities.UserDepartmentMembership
                {
                    UserId = user.Id,
                    CompanyId = req.CompanyId,
                    DepartmentId = departmentId,
                    DepartmentRole = req.DepartmentRole,
                    Status = "Invited"
                });
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { invitedUserId = user.Id });
    }

    [HttpPut("companies/{companyId}")]
    public async Task<IActionResult> UpdateCompanyName(Guid companyId, [FromBody] UpdateCompanyNameDto dto)
    {
        var userId = ParseGuid("sub");
        if (userId == null) return Unauthorized();

        var roleClaim = User.FindFirst("Role")?.Value;
        var role = UserRoleExtensions.ParseRole(roleClaim);
        
        _logger.LogDebug("UpdateCompanyName - UserId: {UserId}, CompanyId: {CompanyId}, Role claim: '{RoleClaim}', Parsed role: {Role}", userId, companyId, roleClaim, role);

        if (role != UserRole.CompanyOwner)
        {
            _logger.LogDebug("UpdateCompanyName - Failed: role {Role} != CompanyOwner", role);
            return StatusCode(403, new { error = $"Access denied: CompanyOwner role required. Your role: {roleClaim}" });
        }

        var membership = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId.Value && m.CompanyId == companyId && m.Status == "Active");
        
        _logger.LogDebug("UpdateCompanyName - Membership found: {Found}, CompanyRole: {CompanyRole}", membership != null, membership?.CompanyRole);
        
        if (membership == null)
        {
            _logger.LogDebug("UpdateCompanyName - Failed: membership not found");
            return StatusCode(403, new { error = "You are not a member of this company." });
        }
            
        if (membership.CompanyRole != UserRole.CompanyOwner.ToClaimValue())
        {
            _logger.LogDebug("UpdateCompanyName - Failed: CompanyRole {CompanyRole} != {Expected}", membership.CompanyRole, UserRole.CompanyOwner.ToClaimValue());
            return StatusCode(403, new { error = $"You are not the owner of this company. Your role: {membership.CompanyRole}" });
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Company name is required." });

        var existingWithName = await _db.Tenants
            .AnyAsync(t => t.Name.ToLower() == dto.Name.ToLower() && t.Id != companyId);
        if (existingWithName)
            return Conflict(new { error = "A company with this name already exists." });

        var company = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == companyId);
        if (company == null) return NotFound(new { error = "Company not found." });

        _logger.LogDebug("UpdateCompanyName - Updating company {CompanyId} name from '{OldName}' to '{NewName}'", companyId, company.Name, dto.Name);
        var oldName = company.Name;
        company.Name = dto.Name;
        await _db.SaveChangesAsync();

        return Ok(new { companyId = company.Id, companyName = company.Name });
    }

    private Guid? ParseGuid(string claimType)
    {
        var value = User.FindFirst(claimType)?.Value;
        if (string.IsNullOrEmpty(value) && claimType == "sub")
        {
            value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}

public record AcceptInviteRequest(string Token);
public record CreateOrganizationDto(string Name);
public record InviteUserRequest(
    Guid CompanyId,
    string Email,
    string DisplayName,
    string CompanyRole,
    string DepartmentRole,
    Guid[] DepartmentIds
);

public record CreateDepartmentDto(string Name, string Code);
public record UpdateCompanyNameDto(string Name);
