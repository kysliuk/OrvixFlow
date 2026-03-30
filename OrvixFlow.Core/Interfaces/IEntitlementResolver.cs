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

    public bool CanAddSeats(int count) => MaxSeats == null || (MaxSeats.Value >= count);
    public bool CanAddTokens(int count) => MaxMonthlyTokens >= (TokensUsedThisPeriod + count);
    public bool CanAddApiRequests => MaxApiRequestsPerDay > ApiRequestsUsedToday;
    public bool CanAddStorage(int mb) => MaxStorageMb >= (StorageUsedMb + mb);
    public bool CanAddKnowledgeBase => MaxKnowledgeBases > KnowledgeBasesCount;
}
