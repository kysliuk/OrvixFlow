using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Services;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlansController : ControllerBase
{
    private readonly IPlanService _planService;

    public PlansController(IPlanService planService)
    {
        _planService = planService;
    }

    private UserRole CurrentUserRole() =>
        UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);

    private bool IsSuperAdmin() => CurrentUserRole() == UserRole.SuperAdmin;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        if (!IsSuperAdmin()) return Forbid();

        var plans = await _planService.GetAllPlansAsync(includeInactive);
        return Ok(plans.Select(MapToAdminDto));
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var plans = await _planService.GetActivePlansAsync();
        return Ok(plans.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var plan = await _planService.GetPlanByIdAsync(id);
        if (plan == null) return NotFound();

        return Ok(MapToAdminDto(plan));
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var plan = await _planService.GetPlanBySlugAsync(slug);
        if (plan == null) return NotFound();

        return Ok(MapToDto(plan));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var billingInterval = !string.IsNullOrEmpty(request.BillingInterval)
            ? BillingIntervalExtensions.ParseInterval(request.BillingInterval)
            : BillingInterval.Monthly;

        var plan = new PlanTemplate
        {
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description ?? string.Empty,
            MonthlyPriceCents = request.MonthlyPriceCents,
            YearlyPriceCents = request.YearlyPriceCents,
            Currency = request.Currency ?? "USD",
            BillingInterval = billingInterval,
            SortOrder = request.SortOrder,
            IsPubliclyVisible = request.IsPubliclyVisible,
            MaxSeats = request.MaxSeats,
            IsActive = true,
            IsFree = request.IsFree,
            IsTrialAllowed = request.IsTrialAllowed,
            TrialDays = request.TrialDays,
            LegacyLocked = request.LegacyLocked,
            StripeMonthlyPriceId = request.StripeMonthlyPriceId,
            StripeYearlyPriceId = request.StripeYearlyPriceId
        };

        try
        {
            var result = await _planService.CreatePlanAsync(plan, request.ModuleIds);
            
            if (request.Entitlements != null)
            {
                var entitlements = new Core.Entities.PlanEntitlements
                {
                    PlanTemplateId = result.Id,
                    MaxMonthlyTokens = request.Entitlements.MaxMonthlyTokens,
                    MaxApiRequestsPerDay = request.Entitlements.MaxApiRequestsPerDay,
                    MaxStorageMb = request.Entitlements.MaxStorageMb,
                    MaxKnowledgeBases = request.Entitlements.MaxKnowledgeBases,
                    MaxInboxMessagesPerMonth = request.Entitlements.MaxInboxMessagesPerMonth,
                    MaxMailboxConnections = request.Entitlements.MaxMailboxConnections,
                    CreatedAt = DateTime.UtcNow
                };
                await _planService.SetEntitlementsAsync(result.Id, entitlements);
                result = await _planService.GetPlanByIdAsync(result.Id);
            }
            
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, MapToAdminDto(result));
        }
        catch (PlanSlugAlreadyExistsException)
        {
            return BadRequest(new { error = "A plan with this slug already exists" });
        }
        catch (ModuleNotFoundException ex)
        {
            return BadRequest(new { error = $"Module not found: {ex.ModuleId}" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var existing = await _planService.GetPlanByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Name = request.Name;
        existing.Description = request.Description ?? existing.Description;
        existing.MonthlyPriceCents = request.MonthlyPriceCents;
        existing.YearlyPriceCents = request.YearlyPriceCents;
        existing.Currency = request.Currency ?? existing.Currency;
        
        if (!string.IsNullOrEmpty(request.BillingInterval))
        {
            existing.BillingInterval = BillingIntervalExtensions.ParseInterval(request.BillingInterval);
        }
        
        existing.SortOrder = request.SortOrder;
        existing.IsPubliclyVisible = request.IsPubliclyVisible;
        existing.MaxSeats = request.MaxSeats;
        existing.IsActive = request.IsActive;
        existing.IsFree = request.IsFree;
        existing.IsTrialAllowed = request.IsTrialAllowed;
        existing.TrialDays = request.TrialDays;
        existing.LegacyLocked = request.LegacyLocked;
        existing.StripeMonthlyPriceId = request.StripeMonthlyPriceId;
        existing.StripeYearlyPriceId = request.StripeYearlyPriceId;

        try
        {
            var result = await _planService.UpdatePlanAsync(existing);
            
            if (request.ModuleIds != null)
            {
                await _planService.SyncModulesForPlanAsync(id, request.ModuleIds);
                result = await _planService.GetPlanByIdAsync(id);
            }
            
            if (request.Entitlements != null)
            {
                var entitlements = new Core.Entities.PlanEntitlements
                {
                    PlanTemplateId = result.Id,
                    MaxMonthlyTokens = request.Entitlements.MaxMonthlyTokens,
                    MaxApiRequestsPerDay = request.Entitlements.MaxApiRequestsPerDay,
                    MaxStorageMb = request.Entitlements.MaxStorageMb,
                    MaxKnowledgeBases = request.Entitlements.MaxKnowledgeBases,
                    MaxInboxMessagesPerMonth = request.Entitlements.MaxInboxMessagesPerMonth,
                    MaxMailboxConnections = request.Entitlements.MaxMailboxConnections,
                    CreatedAt = DateTime.UtcNow
                };
                await _planService.SetEntitlementsAsync(result.Id, entitlements);
                result = await _planService.GetPlanByIdAsync(result.Id);
            }
            
            return Ok(MapToAdminDto(result));

        }
        catch (PlanSlugAlreadyExistsException)
        {
            return BadRequest(new { error = "A plan with this slug already exists" });
        }
    }

    [HttpPost("{id}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            await _planService.ArchivePlanAsync(id);
            return Ok(new { message = "Plan archived successfully" });
        }
        catch (PlanNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            await _planService.ReactivatePlanAsync(id);
            return Ok(new { message = "Plan reactivated successfully" });
        }
        catch (PlanNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/modules/{moduleId}")]
    public async Task<IActionResult> AddModule(Guid id, Guid moduleId)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            await _planService.AddModuleToPlanAsync(id, moduleId);
            return Ok(new { message = "Module added successfully" });
        }
        catch (PlanNotFoundException)
        {
            return NotFound(new { error = "Plan not found" });
        }
        catch (ModuleNotFoundException)
        {
            return NotFound(new { error = "Module not found" });
        }
    }

    [HttpDelete("{id}/modules/{moduleId}")]
    public async Task<IActionResult> RemoveModule(Guid id, Guid moduleId)
    {
        if (!IsSuperAdmin()) return Forbid();

        await _planService.RemoveModuleFromPlanAsync(id, moduleId);
        return Ok(new { message = "Module removed successfully" });
    }

    [HttpPut("{id}/entitlements")]
    public async Task<IActionResult> SetEntitlements(Guid id, [FromBody] SetEntitlementsRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var entitlements = new PlanEntitlements
        {
            MaxMonthlyTokens = request.MaxMonthlyTokens,
            MaxApiRequestsPerDay = request.MaxApiRequestsPerDay,
            MaxStorageMb = request.MaxStorageMb,
            MaxKnowledgeBases = request.MaxKnowledgeBases,
            MaxInboxMessagesPerMonth = request.MaxInboxMessagesPerMonth,
            MaxMailboxConnections = request.MaxMailboxConnections
        };

        try
        {
            await _planService.SetEntitlementsAsync(id, entitlements);
            return Ok(new { message = "Entitlements updated successfully" });
        }
        catch (PlanNotFoundException)
        {
            return NotFound();
        }
    }

    private static PlanDto MapToDto(PlanTemplate plan)
    {
        return new PlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Slug = plan.Slug,
            Description = plan.Description,
            MonthlyPriceCents = plan.MonthlyPriceCents,
            YearlyPriceCents = plan.YearlyPriceCents,
            Currency = plan.Currency,
            BillingInterval = plan.BillingInterval.ToClaimValue(),
            SortOrder = plan.SortOrder,
            IsPubliclyVisible = plan.IsPubliclyVisible,
            MaxSeats = plan.MaxSeats,
            IsActive = plan.IsActive,
            IsFree = plan.IsFree,
            IsTrialAllowed = plan.IsTrialAllowed,
            TrialDays = plan.TrialDays,
            LegacyLocked = plan.LegacyLocked,
            CreatedAt = plan.CreatedAt,
            ArchivedAt = plan.ArchivedAt,
            ModuleIds = plan.ModuleInclusions?.Select(m => m.ModuleDefinitionId).ToList() ?? new List<Guid>(),
            Entitlements = plan.Entitlements != null ? new EntitlementsDto
            {
                MaxMonthlyTokens = plan.Entitlements.MaxMonthlyTokens,
                MaxApiRequestsPerDay = plan.Entitlements.MaxApiRequestsPerDay,
                MaxStorageMb = plan.Entitlements.MaxStorageMb,
                MaxKnowledgeBases = plan.Entitlements.MaxKnowledgeBases,
                MaxInboxMessagesPerMonth = plan.Entitlements.MaxInboxMessagesPerMonth,
                MaxMailboxConnections = plan.Entitlements.MaxMailboxConnections
            } : null
        };
    }

    private static AdminPlanDto MapToAdminDto(PlanTemplate plan)
    {
        return new AdminPlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Slug = plan.Slug,
            Description = plan.Description,
            MonthlyPriceCents = plan.MonthlyPriceCents,
            YearlyPriceCents = plan.YearlyPriceCents,
            Currency = plan.Currency,
            BillingInterval = plan.BillingInterval.ToClaimValue(),
            SortOrder = plan.SortOrder,
            IsPubliclyVisible = plan.IsPubliclyVisible,
            MaxSeats = plan.MaxSeats,
            IsActive = plan.IsActive,
            IsFree = plan.IsFree,
            IsTrialAllowed = plan.IsTrialAllowed,
            TrialDays = plan.TrialDays,
            LegacyLocked = plan.LegacyLocked,
            StripeMonthlyPriceId = plan.StripeMonthlyPriceId,
            StripeYearlyPriceId = plan.StripeYearlyPriceId,
            CreatedAt = plan.CreatedAt,
            ArchivedAt = plan.ArchivedAt,
            ModuleIds = plan.ModuleInclusions?.Select(m => m.ModuleDefinitionId).ToList() ?? new List<Guid>(),
            Entitlements = plan.Entitlements != null ? new EntitlementsDto
            {
                MaxMonthlyTokens = plan.Entitlements.MaxMonthlyTokens,
                MaxApiRequestsPerDay = plan.Entitlements.MaxApiRequestsPerDay,
                MaxStorageMb = plan.Entitlements.MaxStorageMb,
                MaxKnowledgeBases = plan.Entitlements.MaxKnowledgeBases,
                MaxInboxMessagesPerMonth = plan.Entitlements.MaxInboxMessagesPerMonth,
                MaxMailboxConnections = plan.Entitlements.MaxMailboxConnections
            } : null
        };
    }
}

public record CreatePlanRequest(
    string Name,
    string Slug,
    string? Description,
    int MonthlyPriceCents,
    int YearlyPriceCents,
    string? Currency,
    string? BillingInterval,
    int SortOrder,
    bool IsPubliclyVisible,
    int? MaxSeats,
    bool IsFree,
    bool IsTrialAllowed,
    int TrialDays,
    bool LegacyLocked,
    string? StripeMonthlyPriceId,
    string? StripeYearlyPriceId,
    IEnumerable<Guid>? ModuleIds,
    EntitlementsInput? Entitlements
);

public record UpdatePlanRequest(
    string Name,
    string? Description,
    int MonthlyPriceCents,
    int YearlyPriceCents,
    string? Currency,
    string? BillingInterval,
    int SortOrder,
    bool IsPubliclyVisible,
    int? MaxSeats,
    bool IsActive,
    bool IsFree,
    bool IsTrialAllowed,
    int TrialDays,
    bool LegacyLocked,
    string? StripeMonthlyPriceId,
    string? StripeYearlyPriceId,
    IEnumerable<Guid>? ModuleIds,
    EntitlementsInput? Entitlements
);

public record EntitlementsInput(
    int MaxMonthlyTokens,
    int MaxApiRequestsPerDay,
    int MaxStorageMb,
    int MaxKnowledgeBases,
    int MaxInboxMessagesPerMonth,
    int MaxMailboxConnections
);

public record SetEntitlementsRequest(
    int MaxMonthlyTokens,
    int MaxApiRequestsPerDay,
    int MaxStorageMb,
    int MaxKnowledgeBases,
    int MaxInboxMessagesPerMonth,
    int MaxMailboxConnections
);

public record PlanDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int MonthlyPriceCents { get; init; }
    public int YearlyPriceCents { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string BillingInterval { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsPubliclyVisible { get; init; }
    public int? MaxSeats { get; init; }
    public bool IsActive { get; init; }
    public bool IsFree { get; init; }
    public bool IsTrialAllowed { get; init; }
    public int TrialDays { get; init; }
    public bool LegacyLocked { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public List<Guid> ModuleIds { get; init; } = new();
    public EntitlementsDto? Entitlements { get; init; }
}

public record AdminPlanDto : PlanDto
{
    public string? StripeMonthlyPriceId { get; init; }
    public string? StripeYearlyPriceId { get; init; }
}

public record EntitlementsDto
{
    public int MaxMonthlyTokens { get; init; }
    public int MaxApiRequestsPerDay { get; init; }
    public int MaxStorageMb { get; init; }
    public int MaxKnowledgeBases { get; init; }
    public int MaxInboxMessagesPerMonth { get; init; }
    public int MaxMailboxConnections { get; init; }
}
