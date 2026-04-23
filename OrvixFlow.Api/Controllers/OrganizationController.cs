using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Entities;
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
    private readonly ICompanyBootstrapService _companyBootstrapService;
    private readonly IAuthService _authService;

    public OrganizationController(
        AppDbContext db,
        ILogger<OrganizationController> logger,
        ICompanyBootstrapService companyBootstrapService,
        IAuthService authService)
    {
        _db = db;
        _logger = logger;
        _companyBootstrapService = companyBootstrapService;
        _authService = authService;
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
                plan = c.Plan,
                lifecycleStatus = c.LifecycleStatus,
                deletionScheduledFor = c.DeletionScheduledFor
            })
            .Where(c => c.lifecycleStatus != "Archived")
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

        var activeCompanyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        var roleClaim = User.FindFirst("Role")?.Value;
        var parsedRole = UserRoleExtensions.ParseRole(roleClaim);

        var memberships = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId.Value && m.Status == "Active")
            .Join(_db.Tenants, m => m.CompanyId, c => c.Id, (m, c) => new
            {
                companyId = c.Id,
                companyName = c.Name,
                role = m.CompanyRole,
                lifecycleStatus = c.LifecycleStatus
            })
            .Where(m => m.lifecycleStatus != "Archived")
            .ToListAsync();

        var currentMembership = activeCompanyId.HasValue
            ? memberships.FirstOrDefault(m => m.companyId == activeCompanyId.Value)
            : memberships.FirstOrDefault();

        string? companyName = currentMembership?.companyName;
        if (companyName == null && parsedRole.IsPlatformAdmin() && activeCompanyId.HasValue)
        {
            companyName = await _db.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == activeCompanyId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync();
        }

        var hasOrganization = currentMembership != null || (parsedRole.IsPlatformAdmin() && activeCompanyId.HasValue && companyName != null);

        return Ok(new
        {
            hasOrganization,
            activeCompanyId = currentMembership?.companyId ?? activeCompanyId,
            companyName,
            role = currentMembership?.role ?? roleClaim
        });
    }

    [HttpGet("companies/{companyId}/deletion-eligibility")]
    public async Task<IActionResult> GetDeletionEligibility(Guid companyId)
    {
        var userId = ParseGuid("sub");
        if (userId == null) return Unauthorized();

        var evaluation = await EvaluateCompanyDeletionAsync(companyId, userId.Value);
        if (evaluation.Company == null)
            return NotFound(new { error = "Company not found." });

        if (evaluation.Membership == null)
            return StatusCode(403, new { error = "You are not a member of this company." });

        return Ok(new
        {
            companyId = evaluation.Company.Id,
            companyName = evaluation.Company.Name,
            plan = evaluation.Company.Plan,
            lifecycleStatus = evaluation.Company.LifecycleStatus,
            canDelete = evaluation.CanDelete,
            blockers = evaluation.Blockers,
            deletionScheduledFor = evaluation.Company.DeletionScheduledFor,
            retentionDays = 60
        });
    }

    [HttpPost("companies/{companyId}/archive")]
    public async Task<IActionResult> ArchiveCompany(Guid companyId, [FromBody] ArchiveCompanyDto dto)
    {
        var userId = ParseGuid("sub");
        if (userId == null) return Unauthorized();

        var evaluation = await EvaluateCompanyDeletionAsync(companyId, userId.Value);
        if (evaluation.Company == null)
            return NotFound(new { error = "Company not found." });

        if (evaluation.Membership == null)
            return StatusCode(403, new { error = "You are not a member of this company." });

        if (!string.Equals(dto.ConfirmationName?.Trim(), evaluation.Company.Name, StringComparison.Ordinal))
            return BadRequest(new { error = "Typed confirmation must exactly match the company name." });

        if (!evaluation.CanDelete)
            return BadRequest(new { error = "Company cannot be archived yet.", blockers = evaluation.Blockers });

        evaluation.Company.LifecycleStatus = "Archived";
        evaluation.Company.ArchivedAt = DateTime.UtcNow;
        evaluation.Company.ArchivedByUserId = userId.Value;
        evaluation.Company.DeletionScheduledFor = DateTime.UtcNow.AddDays(60);
        evaluation.Company.ArchiveReason = dto.Reason?.Trim();

        await _db.SaveChangesAsync();

        var remainingMembershipCompanyId = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId.Value && m.Status == "Active" && m.CompanyId != companyId)
            .Join(_db.Tenants.IgnoreQueryFilters().Where(t => t.LifecycleStatus != "Archived"), m => m.CompanyId, t => t.Id, (m, t) => m.CompanyId)
            .FirstOrDefaultAsync();

        AuthResult? sessionResult = null;
        if (remainingMembershipCompanyId != Guid.Empty)
            sessionResult = await _authService.SwitchCompanyAsync(userId.Value, remainingMembershipCompanyId);

        return Ok(new
        {
            message = "Company archived successfully.",
            companyId = evaluation.Company.Id,
            companyName = evaluation.Company.Name,
            deletionScheduledFor = evaluation.Company.DeletionScheduledFor,
            token = sessionResult?.Token,
            profile = sessionResult?.Profile,
            refreshToken = sessionResult?.RefreshToken
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

        if (!await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId.Value))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Organization name is required." });

        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Name.ToLower() == dto.Name.ToLower()))
            return Conflict(new { error = "An organization with this name already exists." });

        var tenant = new Core.Entities.Tenant
        {
            Name = dto.Name,
            Plan = "Free",
            SubscriptionStatus = "Active"
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(); // generate tenant Id

        await _companyBootstrapService.EnsureOwnerBootstrapAsync(userId.Value, tenant.Id);
        await _companyBootstrapService.EnsureDefaultSubscriptionAsync(tenant.Id);

        var sessionResult = await _authService.SwitchCompanyAsync(userId.Value, tenant.Id);
        if (!sessionResult.IsSuccess)
            return BadRequest(new { error = sessionResult.Error ?? "Organization created but session could not be updated." });

        return Ok(new
        {
            token = sessionResult.Token,
            profile = sessionResult.Profile,
            refreshToken = sessionResult.RefreshToken,
            companyId = tenant.Id,
            companyName = tenant.Name,
            role = UserRole.CompanyOwner.ToClaimValue(),
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
        return StatusCode(410, new { error = "This legacy invite endpoint is retired. Use /api/invite instead." });
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

    private async Task<CompanyDeletionEvaluation> EvaluateCompanyDeletionAsync(Guid companyId, Guid userId)
    {
        var company = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == companyId);

        var membership = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId && m.Status == "Active");

        if (company == null)
            return new CompanyDeletionEvaluation(null, membership, false, []);

        var blockers = new System.Collections.Generic.List<string>();

        if (company.LifecycleStatus == "Archived")
            blockers.Add("This company is already archived.");

        if (membership == null)
            blockers.Add("You are not an active member of this company.");
        else if (membership.CompanyRole != UserRole.CompanyOwner.ToClaimValue())
            blockers.Add("Only the CompanyOwner can archive this company.");

        if (!string.Equals(company.Plan, "Free", StringComparison.OrdinalIgnoreCase))
            blockers.Add("Only companies on the Free plan can be archived.");

        var subscription = await _db.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);

        if (subscription != null)
        {
            if (subscription.PlanTemplateId != PlanCatalog.FreeId && subscription.Status is (SubscriptionState.Active or SubscriptionState.Trialing))
                blockers.Add("The company still has an active or trialing subscription state.");

            if (subscription.PendingPlanId.HasValue)
                blockers.Add("The company has a pending plan change.");

            if (!string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
                blockers.Add("The company still has an external billing subscription attached.");
        }

        return new CompanyDeletionEvaluation(company, membership, blockers.Count == 0, blockers);
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
public record ArchiveCompanyDto(string ConfirmationName, string? Reason);

internal sealed record CompanyDeletionEvaluation(
    OrvixFlow.Core.Entities.Tenant? Company,
    OrvixFlow.Core.Entities.UserCompanyMembership? Membership,
    bool CanDelete,
    System.Collections.Generic.IReadOnlyList<string> Blockers);
