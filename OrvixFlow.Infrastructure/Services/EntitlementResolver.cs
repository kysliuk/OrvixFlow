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
            .Include(s => s.PlanTemplate)
                .ThenInclude(p => p.Entitlements)
            .Include(s => s.PlanTemplate)
                .ThenInclude(p => p.ModuleInclusions)
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
            }
            else
            {
                entitlements.MaxMonthlyTokens = 100000;
                entitlements.MaxApiRequestsPerDay = 1000;
                entitlements.MaxStorageMb = 500;
                entitlements.MaxKnowledgeBases = 5;
            }
        }

        var today = DateTime.UtcNow.Date;
        var periodStart = subscription?.CurrentPeriodStart ?? DateTime.UtcNow.AddMonths(-1);

        var usageSummary = await _dbContext.UsageEvents
            .Where(e => e.CompanyId == companyId)
            .GroupBy(e => e.MetricType)
            .Select(g => new
            {
                MetricType = g.Key,
                Total = g.Sum(e => e.Quantity)
            })
            .ToListAsync();

        var tokenUsage = usageSummary.FirstOrDefault(u => u.MetricType == "tokens");
        entitlements.TokensUsedThisPeriod = (int)(tokenUsage?.Total ?? 0);

        entitlements.ApiRequestsUsedToday = (int)await _dbContext.UsageEvents
            .Where(e => e.CompanyId == companyId && e.OccurredAt >= today)
            .SumAsync(e => e.Quantity);

        entitlements.KnowledgeBasesCount = await _dbContext.KnowledgeBases
            .Where(k => k.TenantId == companyId)
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
        var entitlements = await GetEntitlementsAsync(companyId);
        return entitlements.CanAddSeats(currentMemberCount + 1);
    }

    public async Task<bool> IsWithinTokenLimitAsync(Guid companyId, int tokensToConsume)
    {
        var entitlements = await GetEntitlementsAsync(companyId);
        return entitlements.CanAddTokens(tokensToConsume);
    }

    public async Task<bool> IsWithinApiLimitAsync(Guid companyId)
    {
        var entitlements = await GetEntitlementsAsync(companyId);
        return entitlements.CanAddApiRequests;
    }

    public async Task<bool> IsWithinStorageLimitAsync(Guid companyId, int mbToConsume)
    {
        var entitlements = await GetEntitlementsAsync(companyId);
        return entitlements.CanAddStorage(mbToConsume);
    }

    public async Task<bool> IsWithinKnowledgeBaseLimitAsync(Guid companyId)
    {
        var entitlements = await GetEntitlementsAsync(companyId);
        return entitlements.CanAddKnowledgeBase;
    }
}
