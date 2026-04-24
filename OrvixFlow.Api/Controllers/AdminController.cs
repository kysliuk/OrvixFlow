using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICompanySubscriptionService _subscriptionService;
    private readonly IEntitlementResolver _entitlementResolver;

    public AdminController(
        AppDbContext db, 
        ICompanySubscriptionService subscriptionService,
        IEntitlementResolver entitlementResolver)
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _entitlementResolver = entitlementResolver;
    }

    private UserRole CurrentUserRole() =>
        UserRoleExtensions.ParseRole(HttpContext.User.FindFirst("Role")?.Value);

    private bool IsSuperAdmin() => CurrentUserRole() == UserRole.SuperAdmin;
    private bool IsGlobalAdmin() => CurrentUserRole().IsPlatformAdmin();
    private bool IsAdmin() => CurrentUserRole().IsCompanyAdminOrAbove();

    [HttpGet("metrics")]
    public async Task<IActionResult> GetGlobalMetrics()
    {
        if (!IsGlobalAdmin()) return Forbid();

        var totalTenants = await _db.Tenants.IgnoreQueryFilters().CountAsync();
        var totalUsers = await _db.Users.IgnoreQueryFilters().CountAsync();
        var totalMemoryChunks = await _db.KnowledgeBases.IgnoreQueryFilters().CountAsync();
        
        var premiumTenants = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Plan == "Starter" || t.Plan == "Pro" || t.Plan == "Enterprise")
            .CountAsync();

        var activeSubscriptions = await _db.CompanySubscriptions
            .IgnoreQueryFilters()
            .CountAsync(s => s.Status == SubscriptionState.Active || s.Status == SubscriptionState.Trialing);

        return Ok(new
        {
            totalTenants,
            totalUsers,
            totalMemoryChunks,
            premiumTenants,
            activeSubscriptions
        });
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants()
    {
        if (!IsGlobalAdmin()) return Forbid();

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Plan,
                t.SubscriptionStatus,
                t.LifecycleStatus,
                t.CreatedAt,
                UserCount = t.Users.Count
            })
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tenants);
    }

    [HttpGet("companies")]
    public async Task<IActionResult> ListCompanies()
    {
        if (!IsGlobalAdmin()) return Forbid();

        var companies = await _db.Tenants
            .IgnoreQueryFilters()
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Plan,
                t.SubscriptionStatus,
                t.LifecycleStatus,
                t.DeletionScheduledFor,
                t.CreatedAt,
                UserCount = t.Users.Count,
                HasSubscription = _db.CompanySubscriptions
                    .IgnoreQueryFilters()
                    .Any(s => s.CompanyId == t.Id)
            })
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(companies);
    }

    [HttpGet("companies/{id}")]
    public async Task<IActionResult> GetCompany(Guid id)
    {
        if (!IsGlobalAdmin()) return Forbid();

        var company = await _db.Tenants // F-19 FIX: WebhookSecret intentionally omitted — treat as password, never expose after creation
            .IgnoreQueryFilters()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Plan,
                t.SubscriptionStatus,
                t.LifecycleStatus,
                t.ArchivedAt,
                t.DeletionScheduledFor,
                t.CreatedAt,
                UserCount = t.Users.Count,
                Members = t.Users.Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Role,
                    u.CreatedAt
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (company == null) return NotFound();

        var subscription = await _subscriptionService.GetSubscriptionAsync(id);
        var entitlements = await _entitlementResolver.GetEntitlementsAsync(id);

        return Ok(new
        {
            company,
            subscription = subscription != null ? new
            {
                subscription.Id,
                Status = subscription.Status.ToClaimValue(),
                BillingInterval = subscription.BillingInterval.ToClaimValue(),
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd,
                subscription.TrialEndsAt,
                Plan = subscription.PlanTemplate != null ? new
                {
                    subscription.PlanTemplate.Id,
                    subscription.PlanTemplate.Name,
                    subscription.PlanTemplate.Slug,
                    subscription.PlanTemplate.MonthlyPriceCents,
                    subscription.PlanTemplate.MaxSeats
                } : null
            } : null,
            entitlements
        });
    }

    /// <summary>
    /// T4-5: Admin subscription view endpoint.
    /// Returns full CompanySubscription + current usage + EntitlementOverride.
    /// </summary>
    [HttpGet("companies/{id}/subscription")]
    public async Task<IActionResult> GetCompanySubscription(Guid id)
    {
        if (!IsGlobalAdmin()) return Forbid();

        var subscription = await _subscriptionService.GetSubscriptionAsync(id);
        if (subscription == null)
        {
            return NotFound(new { error = "Subscription not found" });
        }

        // Get effective entitlements (includes overrides)
        var entitlements = await _entitlementResolver.GetEffectiveEntitlementsAsync(id);
        
        // Get entitlement override if exists
        var overrideEntity = await _entitlementResolver.GetEntitlementOverrideAsync(id);
        
        // Get current usage counts
        var memberCount = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .CountAsync(m => m.CompanyId == id && m.Status == "Active");

        var kbCount = await _db.KnowledgeBaseDocuments
            .IgnoreQueryFilters()
            .CountAsync(k => k.TenantId == id);

        var mailboxCount = await _db.MailboxConnections
            .IgnoreQueryFilters()
            .CountAsync(m => m.TenantId == id);

        return Ok(new
        {
            subscription = new
            {
                subscription.Id,
                subscription.CompanyId,
                Status = subscription.Status.ToClaimValue(),
                BillingInterval = subscription.BillingInterval.ToClaimValue(),
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd,
                subscription.TrialEndsAt,
                subscription.PendingPlanId,
                subscription.PendingChangeAt,
                Plan = subscription.PlanTemplate != null ? new
                {
                    subscription.PlanTemplate.Id,
                    subscription.PlanTemplate.Name,
                    subscription.PlanTemplate.Slug,
                    subscription.PlanTemplate.MonthlyPriceCents,
                    subscription.PlanTemplate.YearlyPriceCents,
                    subscription.PlanTemplate.MaxSeats,
                    subscription.PlanTemplate.IsFree,
                    PlanEntitlements = subscription.PlanTemplate.Entitlements != null ? new
                    {
                        subscription.PlanTemplate.Entitlements.MaxMonthlyTokens,
                        subscription.PlanTemplate.Entitlements.MaxApiRequestsPerDay,
                        subscription.PlanTemplate.Entitlements.MaxStorageMb,
                        subscription.PlanTemplate.Entitlements.MaxKnowledgeBases,
                        subscription.PlanTemplate.Entitlements.MaxInboxMessagesPerMonth,
                        subscription.PlanTemplate.Entitlements.MaxMailboxConnections
                    } : null
                } : null
            },
            entitlements = new
            {
                // Effective limits (base + override)
                entitlements.MaxSeats,
                entitlements.MaxMonthlyTokens,
                entitlements.MaxApiRequestsPerDay,
                entitlements.MaxStorageMb,
                entitlements.MaxKnowledgeBases,
                entitlements.MaxInboxMessagesPerMonth,
                entitlements.MaxMailboxConnections,
                // Current usage
                entitlements.TokensUsedThisPeriod,
                entitlements.ApiRequestsUsedToday,
                entitlements.StorageUsedMb,
                entitlements.KnowledgeBasesCount,
                entitlements.InboxMessagesUsedThisMonth,
                // Override info
                entitlements.HasEntitlementOverride,
                entitlements.OverrideNote,
                // Current counts
                currentSeats = memberCount,
                currentKnowledgeBases = kbCount,
                currentMailboxConnections = mailboxCount
            },
            entitlementOverride = overrideEntity != null ? new
            {
                overrideEntity.Id,
                overrideEntity.MaxSeats,
                overrideEntity.MaxMonthlyTokens,
                overrideEntity.MaxApiRequestsPerDay,
                overrideEntity.MaxStorageMb,
                overrideEntity.MaxKnowledgeBases,
                overrideEntity.MaxInboxMessages,
                overrideEntity.MaxMailboxConnections,
                overrideEntity.Note,
                overrideEntity.CreatedAt,
                overrideEntity.UpdatedAt
            } : null
        });
    }

    /// <summary>
    /// T4-2: Updated to pass targetStatus to AssignPlanAsync for post-payment scenarios.
    /// </summary>
    [HttpPut("companies/{id}/plan")]
    public async Task<IActionResult> AssignPlan(Guid id, [FromBody] AssignPlanRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            // T4-2: Pass targetStatus for post-payment scenarios
            var subscription = await _subscriptionService.AssignPlanAsync(
                id, 
                request.PlanTemplateId, 
                request.BillingInterval,
                request.TargetStatus);
            
            return Ok(new
            {
                subscription.Id,
                Status = subscription.Status.ToClaimValue(),
                subscription.PlanTemplateId,
                subscription.CurrentPeriodEnd
            });
        }
        catch (PlanNotFoundException)
        {
            return NotFound(new { error = "Plan not found" });
        }
        catch (PlanNotActiveException)
        {
            return BadRequest(new { error = "Plan is not active" });
        }
        catch (SeatLimitExceededException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("companies/{id}/cancel")]
    public async Task<IActionResult> CancelCompany(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            await _subscriptionService.CancelSubscriptionAsync(id);
            return Ok(new { message = "Company subscription cancelled" });
        }
        catch (SubscriptionNotFoundException)
        {
            return NotFound(new { error = "Subscription not found" });
        }
    }

    public class ChangePlanRequest
    {
        public Guid NewPlanTemplateId { get; set; }
        public bool Immediate { get; set; } = true;
    }

    /// <summary>
    /// T4-3: Updated to handle DowngradeNotAllowedException with 409 Conflict.
    /// </summary>
    [HttpPost("companies/{id}/change-plan")]
    public async Task<IActionResult> ChangeCompanyPlan(Guid id, [FromBody] ChangePlanRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            var subscription = await _subscriptionService.ChangePlanAsync(id, request.NewPlanTemplateId, request.Immediate);
            return Ok(new
            {
                subscription.Id,
                Status = subscription.Status.ToClaimValue(),
                subscription.PlanTemplateId,
                subscription.CurrentPeriodEnd
            });
        }
        catch (SubscriptionNotFoundException)
        {
            return NotFound(new { error = "Subscription not found" });
        }
        catch (PlanNotFoundException)
        {
            return NotFound(new { error = "Plan not found" });
        }
        catch (PlanNotActiveException)
        {
            return BadRequest(new { error = "Plan is not active" });
        }
        catch (SeatLimitExceededException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DowngradeNotAllowedException ex)
        {
            // T4-3: Return 409 Conflict with downgrade blocker info
            return Conflict(new
            {
                error = "DOWNGRADE_BLOCKED",
                message = ex.Message,
                exceededLimit = ex.ExceededLimit,
                currentValue = ex.CurrentValue,
                maxAllowed = ex.MaxAllowed,
                blockers = new[]
                {
                    new { limit = ex.ExceededLimit, current = ex.CurrentValue, maxAllowed = ex.MaxAllowed }
                },
                actionRequired = $"Reduce {ex.ExceededLimit} before downgrading. Current: {ex.CurrentValue}, New limit: {ex.MaxAllowed}"
            });
        }
    }

    [HttpPost("companies/{id}/suspend")]
    public async Task<IActionResult> SuspendCompany(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            await _subscriptionService.SuspendSubscriptionAsync(id);
            return Ok(new { message = "Company suspended" });
        }
        catch (SubscriptionNotFoundException)
        {
            return NotFound(new { error = "Subscription not found" });
        }
    }

    [HttpPost("companies/{id}/reactivate")]
    public async Task<IActionResult> ReactivateCompany(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            await _subscriptionService.ReactivateSubscriptionAsync(id);
            return Ok(new { message = "Company reactivated" });
        }
        catch (SubscriptionNotFoundException)
        {
            return NotFound(new { error = "Subscription not found" });
        }
    }

    [HttpPost("companies/{id}/restore")]
    public async Task<IActionResult> RestoreArchivedCompany(Guid id)
    {
        if (!IsGlobalAdmin()) return Forbid();

        var company = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (company == null)
            return NotFound(new { error = "Company not found" });

        if (company.LifecycleStatus != "Archived")
            return BadRequest(new { error = "Company is not archived" });

        if (company.DeletionScheduledFor.HasValue && company.DeletionScheduledFor.Value <= DateTime.UtcNow)
            return BadRequest(new { error = "Archived company can no longer be restored after retention expiry" });

        company.LifecycleStatus = "Active";
        company.ArchivedAt = null;
        company.ArchivedByUserId = null;
        company.DeletionScheduledFor = null;
        company.ArchiveReason = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Company restored", companyId = company.Id, companyName = company.Name });
    }

    [HttpGet("companies/{id}/usage")]
    public async Task<IActionResult> GetCompanyUsage(Guid id)
    {
        if (!IsGlobalAdmin()) return Forbid();

        var entitlements = await _entitlementResolver.GetEffectiveEntitlementsAsync(id);

        return Ok(new
        {
            entitlements.MaxSeats,
            entitlements.MaxMonthlyTokens,
            entitlements.MaxApiRequestsPerDay,
            entitlements.MaxStorageMb,
            entitlements.MaxKnowledgeBases,
            entitlements.MaxInboxMessagesPerMonth,
            entitlements.MaxMailboxConnections,
            entitlements.TokensUsedThisPeriod,
            entitlements.ApiRequestsUsedToday,
            entitlements.StorageUsedMb,
            entitlements.KnowledgeBasesCount,
            entitlements.HasEntitlementOverride,
            entitlements.OverrideNote
        });
    }

    // --- Entitlement Override Endpoints ---

    public class EntitlementOverrideRequest
    {
        public int? MaxSeats { get; set; }
        public int? MaxMonthlyTokens { get; set; }
        public int? MaxApiRequestsPerDay { get; set; }
        public int? MaxStorageMb { get; set; }
        public int? MaxKnowledgeBases { get; set; }
        public int? MaxInboxMessages { get; set; }
        public int? MaxMailboxConnections { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    [HttpGet("companies/{id}/entitlements")]
    public async Task<IActionResult> GetEntitlementOverride(Guid id)
    {
        if (!IsGlobalAdmin()) return Forbid();

        var overrideEntity = await _entitlementResolver.GetEntitlementOverrideAsync(id);
        if (overrideEntity == null)
        {
            return Ok(new { hasOverride = false });
        }

        return Ok(new
        {
            hasOverride = true,
            overrideEntity.Id,
            overrideEntity.MaxSeats,
            overrideEntity.MaxMonthlyTokens,
            overrideEntity.MaxApiRequestsPerDay,
            overrideEntity.MaxStorageMb,
            overrideEntity.MaxKnowledgeBases,
            overrideEntity.MaxInboxMessages,
            overrideEntity.MaxMailboxConnections,
            overrideEntity.Note,
            overrideEntity.CreatedAt,
            overrideEntity.UpdatedAt
        });
    }

    [HttpPut("companies/{id}/entitlements")]
    public async Task<IActionResult> SetEntitlementOverride(Guid id, [FromBody] EntitlementOverrideRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString());

        var existing = await _db.CompanyEntitlementOverrides
            .FirstOrDefaultAsync(o => o.CompanyId == id);

        if (existing != null)
        {
            existing.MaxSeats = request.MaxSeats;
            existing.MaxMonthlyTokens = request.MaxMonthlyTokens;
            existing.MaxApiRequestsPerDay = request.MaxApiRequestsPerDay;
            existing.MaxStorageMb = request.MaxStorageMb;
            existing.MaxKnowledgeBases = request.MaxKnowledgeBases;
            existing.MaxInboxMessages = request.MaxInboxMessages;
            existing.MaxMailboxConnections = request.MaxMailboxConnections;
            existing.Note = request.Note ?? string.Empty;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newOverride = new CompanyEntitlementOverride
            {
                Id = Guid.NewGuid(),
                CompanyId = id,
                MaxSeats = request.MaxSeats,
                MaxMonthlyTokens = request.MaxMonthlyTokens,
                MaxApiRequestsPerDay = request.MaxApiRequestsPerDay,
                MaxStorageMb = request.MaxStorageMb,
                MaxKnowledgeBases = request.MaxKnowledgeBases,
                MaxInboxMessages = request.MaxInboxMessages,
                MaxMailboxConnections = request.MaxMailboxConnections,
                Note = request.Note ?? string.Empty,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.CompanyEntitlementOverrides.Add(newOverride);
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Entitlement override saved" });
    }

    [HttpDelete("companies/{id}/entitlements")]
    public async Task<IActionResult> DeleteEntitlementOverride(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var existing = await _db.CompanyEntitlementOverrides
            .FirstOrDefaultAsync(o => o.CompanyId == id);

        if (existing == null)
        {
            return NotFound(new { error = "No override found for this company" });
        }

        _db.CompanyEntitlementOverrides.Remove(existing);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Entitlement override removed, reverting to plan defaults" });
    }

    // --- Module Override Endpoints ---

    public class ModuleOverrideRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid ModuleDefinitionId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string Note { get; set; } = string.Empty;
    }

    [HttpGet("companies/{id}/modules")]
    public async Task<IActionResult> GetModuleOverrides(Guid id)
    {
        if (!IsGlobalAdmin()) return Forbid();

        var overrides = await _db.CompanyModuleOverrides
            .IgnoreQueryFilters()
            .Where(o => o.CompanyId == id)
            .Include(o => o.ModuleDefinition)
            .Select(o => new
            {
                o.Id,
                o.ModuleDefinitionId,
                ModuleKey = o.ModuleDefinition.Key,
                ModuleName = o.ModuleDefinition.DisplayName,
                o.IsEnabled,
                o.Note,
                o.CreatedAt
            })
            .ToListAsync();

        return Ok(overrides);
    }

    [HttpPost("companies/{id}/modules")]
    public async Task<IActionResult> AddModuleOverride(Guid id, [FromBody] ModuleOverrideRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var module = await _db.ModuleDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == request.ModuleDefinitionId);

        if (module == null)
        {
            return NotFound(new { error = "Module not found" });
        }

        var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString());

        var existing = await _db.CompanyModuleOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.CompanyId == id && o.ModuleDefinitionId == request.ModuleDefinitionId);

        if (existing != null)
        {
            existing.IsEnabled = request.IsEnabled;
            existing.Note = request.Note ?? string.Empty;
        }
        else
        {
            var newOverride = new CompanyModuleOverride
            {
                Id = Guid.NewGuid(),
                CompanyId = id,
                ModuleDefinitionId = request.ModuleDefinitionId,
                IsEnabled = request.IsEnabled,
                Note = request.Note ?? string.Empty,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _db.CompanyModuleOverrides.Add(newOverride);
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Module override {(request.IsEnabled ? "granted" : "suppressed")}" });
    }

    [HttpDelete("companies/{id}/modules/{moduleId}")]
    public async Task<IActionResult> RemoveModuleOverride(Guid id, Guid moduleId)
    {
        if (!IsSuperAdmin()) return Forbid();

        var existing = await _db.CompanyModuleOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.CompanyId == id && o.ModuleDefinitionId == moduleId);

        if (existing == null)
        {
            return NotFound(new { error = "Module override not found" });
        }

        _db.CompanyModuleOverrides.Remove(existing);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Module override removed, reverting to plan default" });
    }

    // --- Module Definition CRUD ---

    public class ModuleDefinitionRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Key { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required]
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Utility";
        public string Tier { get; set; } = "Utility";
        public string Visibility { get; set; } = "UserFacing";
        public bool IsOperational { get; set; }
        public bool IsPremium { get; set; }
        public string? IconKey { get; set; }
        public string? UpgradePromptText { get; set; }
        public int SortOrder { get; set; }
    }

    public class ModuleDefinitionResponse
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;
        public bool IsOperational { get; set; }
        public bool IsActive { get; set; }
        public bool IsPremium { get; set; }
        public string? IconKey { get; set; }
        public string? UpgradePromptText { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [HttpGet("modules")]
    public async Task<IActionResult> ListModules()
    {
        if (!IsGlobalAdmin()) return Forbid();

        var modules = await _db.ModuleDefinitions
            .IgnoreQueryFilters()
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.DisplayName)
            .Select(m => new ModuleDefinitionResponse
            {
                Id = m.Id,
                Key = m.Key,
                DisplayName = m.DisplayName,
                Description = m.Description,
                Category = m.Category,
                Tier = m.Tier,
                Visibility = m.Visibility,
                IsOperational = m.IsOperational,
                IsActive = m.IsActive,
                IsPremium = m.IsPremium,
                IconKey = m.IconKey,
                UpgradePromptText = m.UpgradePromptText,
                SortOrder = m.SortOrder,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(modules);
    }

    [HttpPost("modules")]
    public async Task<IActionResult> CreateModule([FromBody] ModuleDefinitionRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var existing = await _db.ModuleDefinitions
            .IgnoreQueryFilters()
            .AnyAsync(m => m.Key == request.Key);

        if (existing)
        {
            return Conflict(new { error = $"Module with key '{request.Key}' already exists" });
        }

        var module = new ModuleDefinition
        {
            Id = Guid.NewGuid(),
            Key = request.Key,
            DisplayName = request.DisplayName,
            Description = request.Description ?? string.Empty,
            Category = request.Category ?? "Utility",
            Tier = request.Tier ?? "Utility",
            Visibility = request.Visibility ?? "UserFacing",
            IsOperational = request.IsOperational,
            IsActive = true,
            IsPremium = request.IsPremium,
            IconKey = request.IconKey,
            UpgradePromptText = request.UpgradePromptText,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        _db.ModuleDefinitions.Add(module);
        await _db.SaveChangesAsync();

        return Ok(new ModuleDefinitionResponse
        {
            Id = module.Id,
            Key = module.Key,
            DisplayName = module.DisplayName,
            Description = module.Description,
            Category = module.Category,
            Tier = module.Tier,
            Visibility = module.Visibility,
            IsOperational = module.IsOperational,
            IsActive = module.IsActive,
            IsPremium = module.IsPremium,
            IconKey = module.IconKey,
            UpgradePromptText = module.UpgradePromptText,
            SortOrder = module.SortOrder,
            CreatedAt = module.CreatedAt
        });
    }

    [HttpPut("modules/{moduleId:guid}")]
    public async Task<IActionResult> UpdateModule(Guid moduleId, [FromBody] ModuleDefinitionRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var module = await _db.ModuleDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module == null)
        {
            return NotFound(new { error = "Module not found" });
        }

        module.Key = request.Key;
        module.DisplayName = request.DisplayName;
        module.Description = request.Description ?? string.Empty;
        module.Category = request.Category ?? "Utility";
        module.Tier = request.Tier ?? "Utility";
        module.Visibility = request.Visibility ?? "UserFacing";
        module.IsOperational = request.IsOperational;
        module.IsPremium = request.IsPremium;
        module.IconKey = request.IconKey;
        module.UpgradePromptText = request.UpgradePromptText;
        module.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync();

        return Ok(new ModuleDefinitionResponse
        {
            Id = module.Id,
            Key = module.Key,
            DisplayName = module.DisplayName,
            Description = module.Description,
            Category = module.Category,
            Tier = module.Tier,
            Visibility = module.Visibility,
            IsOperational = module.IsOperational,
            IsActive = module.IsActive,
            IsPremium = module.IsPremium,
            IconKey = module.IconKey,
            UpgradePromptText = module.UpgradePromptText,
            SortOrder = module.SortOrder,
            CreatedAt = module.CreatedAt
        });
    }

    [HttpPost("modules/{moduleId:guid}/toggle")]
    public async Task<IActionResult> ToggleModule(Guid moduleId, [FromBody] ToggleModuleRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var module = await _db.ModuleDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module == null)
        {
            return NotFound(new { error = "Module not found" });
        }

        module.IsActive = request.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { module.Id, module.Key, module.IsActive });
    }

    public class ToggleModuleRequest
    {
        public bool IsActive { get; set; }
    }

    // --- Company Audit Log ---

    [HttpGet("companies/{id}/audit")]
    public async Task<IActionResult> GetCompanyAudit(Guid id, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        if (!IsGlobalAdmin()) return Forbid();

        var auditEntries = await _db.AuditTrails
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == id)
            .OrderByDescending(a => a.Timestamp)
            .Skip(offset)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.Action,
                a.Actor,
                a.EntityId,
                a.PreviousState,
                a.NewState,
                a.DecisionDetails,
                a.Timestamp
            })
            .ToListAsync();

        return Ok(auditEntries);
    }
}

/// <summary>
/// T4-2: Added TargetStatus parameter for post-payment scenarios.
/// </summary>
public record AssignPlanRequest(Guid PlanTemplateId, string? BillingInterval, string? TargetStatus = null);
