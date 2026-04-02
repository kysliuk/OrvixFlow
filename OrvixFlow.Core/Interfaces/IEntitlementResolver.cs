using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;

namespace OrvixFlow.Core.Interfaces;

public interface IEntitlementResolver
{
    Task<CompanySubscription?> GetSubscriptionAsync(Guid companyId);
    Task<PlanTemplate?> GetActivePlanAsync(Guid companyId);
    Task<IEnumerable<ModuleDefinition>> GetCompanyModulesAsync(Guid companyId);
    Task<CompanyEntitlements> GetEntitlementsAsync(Guid companyId);
    Task<bool> CanUseModuleAsync(Guid companyId, string moduleKey);
    Task<bool> CanInviteUserAsync(Guid companyId, int currentMemberCount);
    Task<bool> IsWithinTokenLimitAsync(Guid companyId, int tokensToConsume);
    Task<bool> IsWithinApiLimitAsync(Guid companyId);
    Task<bool> IsWithinStorageLimitAsync(Guid companyId, int mbToConsume);
    Task<bool> IsWithinKnowledgeBaseLimitAsync(Guid companyId);
    Task<LimitCheckResult> CheckLimitAsync(Guid companyId, string limitType, int amount = 1);

    Task<CompanyEntitlementOverride?> GetEntitlementOverrideAsync(Guid companyId);
    Task<IEnumerable<CompanyModuleOverride>> GetModuleOverridesAsync(Guid companyId);
    Task<CompanyEntitlements> GetEffectiveEntitlementsAsync(Guid companyId);
    Task<bool> CanUseModuleWithOverridesAsync(Guid companyId, string moduleKey);
}

public class CompanyEntitlements
{
    public int? MaxSeats { get; set; }
    public int MaxMonthlyTokens { get; set; }
    public int MaxApiRequestsPerDay { get; set; }
    public int MaxStorageMb { get; set; }
    public int MaxKnowledgeBases { get; set; }
    public int TokensUsedThisPeriod { get; set; }
    public int ApiRequestsUsedToday { get; set; }
    public int StorageUsedMb { get; set; }
    public int KnowledgeBasesCount { get; set; }

    public int MaxInboxMessagesPerMonth { get; set; }
    public int InboxMessagesUsedThisMonth { get; set; }
    public int MaxMailboxConnections { get; set; }

    public bool HasEntitlementOverride { get; set; }
    public string? OverrideNote { get; set; }

    public bool CanAddSeats(int count) => MaxSeats == null || (MaxSeats.Value >= count);
    public bool CanAddTokens(int count) => MaxMonthlyTokens >= (TokensUsedThisPeriod + count);
    public bool CanAddApiRequests => MaxApiRequestsPerDay > ApiRequestsUsedToday;
    public bool CanAddStorage(int mb) => MaxStorageMb >= (StorageUsedMb + mb);
    public bool CanAddKnowledgeBase => MaxKnowledgeBases > KnowledgeBasesCount;
    public bool CanProcessInboxMessage => MaxInboxMessagesPerMonth == 0 || MaxInboxMessagesPerMonth > InboxMessagesUsedThisMonth;
}

public class LimitCheckResult
{
    public bool Allowed { get; set; }
    public string? ExceededLimit { get; set; }
    public int CurrentUsage { get; set; }
    public int Limit { get; set; }
    public string? UpgradeUrl { get; set; }
}
