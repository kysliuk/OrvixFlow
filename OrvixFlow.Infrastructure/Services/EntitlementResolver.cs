using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services;

public class EntitlementResolver : IEntitlementResolver
{
    private readonly AppDbContext _dbContext;

    public EntitlementResolver(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CompanySubscription?> GetSubscriptionAsync(Guid companyId)
    {
        return await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.PlanTemplate)
                .ThenInclude(p => p!.Entitlements)
            .Include(s => s.PlanTemplate)
                .ThenInclude(p => p!.ModuleInclusions)
                    .ThenInclude(m => m.ModuleDefinition)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);
    }

    public async Task<PlanTemplate?> GetActivePlanAsync(Guid companyId)
    {
        var subscription = await GetSubscriptionAsync(companyId);
        return subscription?.PlanTemplate;
    }

    public async Task<IEnumerable<ModuleDefinition>> GetCompanyModulesAsync(Guid companyId)
    {
        var plan = await GetActivePlanAsync(companyId);
        if (plan == null)
        {
            return Enumerable.Empty<ModuleDefinition>();
        }

        return plan.ModuleInclusions
            .Select(m => m.ModuleDefinition)
            .Where(m => m.IsActive)
            .ToList();
    }

    public async Task<CompanyEntitlements> GetEntitlementsAsync(Guid companyId)
    {
        var subscription = await GetSubscriptionAsync(companyId);

        if (subscription == null
            || subscription.Status == SubscriptionState.Suspended
            || subscription.Status == SubscriptionState.Cancelled)
        {
            return new CompanyEntitlements();
        }

        var entitlements = new CompanyEntitlements();

        if (subscription?.PlanTemplate != null)
        {
            entitlements.MaxSeats = subscription.PlanTemplate.MaxSeats;
            
            if (subscription.PlanTemplate.Entitlements != null)
            {
                var planEntitlements = subscription.PlanTemplate.Entitlements;
                entitlements.MaxMonthlyTokens = planEntitlements.MaxMonthlyTokens;
                entitlements.MaxApiRequestsPerDay = planEntitlements.MaxApiRequestsPerDay;
                entitlements.MaxStorageMb = planEntitlements.MaxStorageMb;
                entitlements.MaxKnowledgeBases = planEntitlements.MaxKnowledgeBases;
                // Inbox Guardian limits from plan
                entitlements.MaxInboxMessagesPerMonth = planEntitlements.MaxInboxMessagesPerMonth;
                entitlements.MaxMailboxConnections = planEntitlements.MaxMailboxConnections;
            }
            else
            {
                // Defaults when no entitlements defined
                entitlements.MaxMonthlyTokens = 100000;
                entitlements.MaxApiRequestsPerDay = 1000;
                entitlements.MaxStorageMb = 500;
                entitlements.MaxKnowledgeBases = 5;
                entitlements.MaxInboxMessagesPerMonth = 0; // Unlimited
                entitlements.MaxMailboxConnections = 1;
            }
        }

        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodStart = subscription?.CurrentPeriodStart ?? DateTime.UtcNow.AddMonths(-1);

        var usageSummary = await _dbContext.UsageEvents
            .IgnoreQueryFilters()
            .Where(e => e.CompanyId == companyId && e.OccurredAt >= periodStart)
            .GroupBy(e => e.MetricType)
            .Select(g => new
            {
                MetricType = g.Key,
                Total = g.Sum(e => e.Quantity)
            })
            .ToListAsync();

        var tokenUsage = usageSummary.FirstOrDefault(u => u.MetricType == "ai-tokens");
        entitlements.TokensUsedThisPeriod = (int)(tokenUsage?.Total ?? 0);

        var storageUsage = usageSummary.FirstOrDefault(u => u.MetricType == "storage-mb");
        entitlements.StorageUsedMb = (int)(storageUsage?.Total ?? 0);

        var kbUsage = usageSummary.FirstOrDefault(u => u.MetricType == "knowledge-bases");
        var kbCountFromDb = await _dbContext.KnowledgeBases
            .IgnoreQueryFilters()
            .Where(k => k.TenantId == companyId)
            .CountAsync();
        entitlements.KnowledgeBasesCount = Math.Max((int)(kbUsage?.Total ?? 0), kbCountFromDb);

        entitlements.ApiRequestsUsedToday = (int)await _dbContext.UsageEvents
            .IgnoreQueryFilters()
            .Where(e => e.CompanyId == companyId && e.OccurredAt >= today)
            .SumAsync(e => e.Quantity);

        // Track inbox messages used this month
        entitlements.InboxMessagesUsedThisMonth = await _dbContext.InboxEvents
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == companyId && e.ReceivedAtUtc >= startOfMonth)
            .CountAsync();

        return entitlements;
    }

    public async Task<bool> CanUseModuleAsync(Guid companyId, string moduleKey)
    {
        var modules = await GetCompanyModulesAsync(companyId);
        return modules.Any(m => m.Key == moduleKey);
    }

    public async Task<bool> CanInviteUserAsync(Guid companyId, int currentMemberCount)
    {
        var entitlements = await GetEffectiveEntitlementsAsync(companyId);
        return entitlements.CanAddSeats(currentMemberCount + 1);
    }

    public async Task<bool> IsWithinTokenLimitAsync(Guid companyId, int tokensToConsume)
    {
        var entitlements = await GetEffectiveEntitlementsAsync(companyId);
        return entitlements.CanAddTokens(tokensToConsume);
    }

    public async Task<bool> IsWithinApiLimitAsync(Guid companyId)
    {
        var entitlements = await GetEffectiveEntitlementsAsync(companyId);
        return entitlements.CanAddApiRequests;
    }

    public async Task<bool> IsWithinStorageLimitAsync(Guid companyId, int mbToConsume)
    {
        var entitlements = await GetEffectiveEntitlementsAsync(companyId);
        return entitlements.CanAddStorage(mbToConsume);
    }

    public async Task<bool> IsWithinKnowledgeBaseLimitAsync(Guid companyId)
    {
        var entitlements = await GetEffectiveEntitlementsAsync(companyId);
        return entitlements.CanAddKnowledgeBase;
    }

    public async Task<LimitCheckResult> CheckLimitAsync(Guid companyId, string limitType, int amount = 1)
    {
        var entitlements = await GetEffectiveEntitlementsAsync(companyId);
        
        var result = new LimitCheckResult { Allowed = true };

        switch (limitType.ToLowerInvariant())
        {
            case "tokens":
            case "ai-tokens":
                result.Limit = entitlements.MaxMonthlyTokens;
                result.CurrentUsage = entitlements.TokensUsedThisPeriod;
                result.ExceededLimit = "AI Tokens";
                result.Allowed = entitlements.CanAddTokens(amount);
                break;
                
            case "storage":
            case "storage-mb":
                result.Limit = entitlements.MaxStorageMb;
                result.CurrentUsage = entitlements.StorageUsedMb;
                result.ExceededLimit = "Storage";
                result.Allowed = entitlements.CanAddStorage(amount);
                break;
                
            case "knowledge-bases":
            case "kb":
                result.Limit = entitlements.MaxKnowledgeBases;
                result.CurrentUsage = entitlements.KnowledgeBasesCount;
                result.ExceededLimit = "Knowledge Bases";
                result.Allowed = entitlements.CanAddKnowledgeBase;
                break;
                
            case "api-requests":
                result.Limit = entitlements.MaxApiRequestsPerDay;
                result.CurrentUsage = entitlements.ApiRequestsUsedToday;
                result.ExceededLimit = "API Requests";
                result.Allowed = entitlements.CanAddApiRequests;
                break;
                
            case "seats":
                var memberCount = await _dbContext.UserCompanyMemberships
                    .IgnoreQueryFilters()
                    .CountAsync(m => m.CompanyId == companyId && m.Status == "Active");
                result.Limit = entitlements.MaxSeats ?? int.MaxValue;
                result.CurrentUsage = memberCount;
                result.ExceededLimit = "Seats";
                result.Allowed = entitlements.CanAddSeats(memberCount + amount);
                break;
                
            case "inbox-messages":
            case "inbox":
                result.Limit = entitlements.MaxInboxMessagesPerMonth;
                result.CurrentUsage = entitlements.InboxMessagesUsedThisMonth;
                result.ExceededLimit = "Inbox Messages";
                result.Allowed = entitlements.CanProcessInboxMessage;
                break;
                
            case "mailbox-connections":
            case "mailbox":
                result.Limit = entitlements.MaxMailboxConnections;
                result.CurrentUsage = await _dbContext.MailboxConnections
                    .IgnoreQueryFilters()
                    .Where(m => m.TenantId == companyId)
                    .CountAsync();
                result.ExceededLimit = "Mailbox Connections";
                // 0 means unlimited
                result.Allowed = result.Limit == 0 || result.CurrentUsage < result.Limit;
                break;
        }

        result.UpgradeUrl = "/billing";
        return result;
    }

    public async Task<CompanyEntitlementOverride?> GetEntitlementOverrideAsync(Guid companyId)
    {
        return await _dbContext.CompanyEntitlementOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.CompanyId == companyId);
    }

    public async Task<IEnumerable<CompanyModuleOverride>> GetModuleOverridesAsync(Guid companyId)
    {
        return await _dbContext.CompanyModuleOverrides
            .IgnoreQueryFilters()
            .Where(o => o.CompanyId == companyId)
            .ToListAsync();
    }

    public async Task<CompanyEntitlements> GetEffectiveEntitlementsAsync(Guid companyId)
    {
        var entitlements = await GetEntitlementsAsync(companyId);
        var overrideEntity = await GetEntitlementOverrideAsync(companyId);

        if (overrideEntity != null)
        {
            if (overrideEntity.MaxSeats.HasValue) entitlements.MaxSeats = overrideEntity.MaxSeats;
            if (overrideEntity.MaxMonthlyTokens.HasValue) entitlements.MaxMonthlyTokens = overrideEntity.MaxMonthlyTokens.Value;
            if (overrideEntity.MaxApiRequestsPerDay.HasValue) entitlements.MaxApiRequestsPerDay = overrideEntity.MaxApiRequestsPerDay.Value;
            if (overrideEntity.MaxStorageMb.HasValue) entitlements.MaxStorageMb = overrideEntity.MaxStorageMb.Value;
            if (overrideEntity.MaxKnowledgeBases.HasValue) entitlements.MaxKnowledgeBases = overrideEntity.MaxKnowledgeBases.Value;
            if (overrideEntity.MaxInboxMessages.HasValue) entitlements.MaxInboxMessagesPerMonth = overrideEntity.MaxInboxMessages.Value;
            if (overrideEntity.MaxMailboxConnections.HasValue) entitlements.MaxMailboxConnections = overrideEntity.MaxMailboxConnections.Value;

            entitlements.HasEntitlementOverride = true;
            entitlements.OverrideNote = overrideEntity.Note;
        }

        return entitlements;
    }

    public async Task<bool> CanUseModuleWithOverridesAsync(Guid companyId, string moduleKey)
    {
        var subscription = await GetSubscriptionAsync(companyId);
        if (subscription == null
            || subscription.Status == SubscriptionState.Suspended
            || subscription.Status == SubscriptionState.Cancelled)
        {
            return false;
        }

        var plan = await GetActivePlanAsync(companyId);
        if (plan == null)
        {
            return false;
        }

        var moduleIds = plan.ModuleInclusions
            .Where(m => m.ModuleDefinition.IsActive)
            .Select(m => m.ModuleDefinitionId)
            .ToHashSet();

        var overrides = await GetModuleOverridesAsync(companyId);
        foreach (var o in overrides)
        {
            if (o.IsEnabled)
            {
                moduleIds.Add(o.ModuleDefinitionId);
            }
            else
            {
                moduleIds.Remove(o.ModuleDefinitionId);
            }
        }

        var modules = await _dbContext.ModuleDefinitions
            .Where(m => moduleIds.Contains(m.Id))
            .ToListAsync();

        return modules.Any(m => m.Key == moduleKey);
    }
}
