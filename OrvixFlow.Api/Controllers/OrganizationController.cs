using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Core.Authorization;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/org")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrganizationController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var userId = ParseGuid("sub");
        if (userId == null) return Unauthorized();

        var companies = await _db.UserCompanyMemberships
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
        Console.WriteLine($"[DEBUG] Claims: {claimsDump}");

        var userId = ParseGuid("sub");
        if (userId == null) 
        {
            Console.WriteLine("[DEBUG] ParseGuid failed to find valid sub or NameIdentifier");
            return Unauthorized();
        }

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
        var inviterRole = User.FindFirst("Role")?.Value;
        if (!Roles.IsAdmin(inviterRole))
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
                Role = req.CompanyRole,
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
