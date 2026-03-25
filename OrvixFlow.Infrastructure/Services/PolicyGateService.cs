using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services;

public interface IPolicyGateService
{
    Task<PolicyDecision> EvaluateAsync(PolicyEvaluationContext context, Guid tenantId);
}

public class PolicyGateService : IPolicyGateService
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static readonly string[] HighRiskKeywords = {
        "lawsuit", "legal", "court", "attorney", "sue",
        "refund", "chargeback", "dispute", "wmata",
        "police", "fbi", "irs", "tax", "compliance",
        "breach", "hack", "data leak", "gdpr", "ccpa",
        "wrongful termination", "discrimination", "harassment",
        "contract breach", "violation", "violate"
    };

    public PolicyGateService(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<PolicyDecision> EvaluateAsync(PolicyEvaluationContext context, Guid tenantId)
    {
        var policy = await GetPolicyAsync(context.Category, tenantId);

        if (policy == null)
        {
            return new PolicyDecision
            {
                Decision = PolicyDecisionType.HoldForApproval,
                Reason = "No policy found for category - defaulting to human review",
                ConfidenceScore = context.ConfidenceScore,
                Category = context.Category
            };
        }

        if (ContainsHighRiskKeyword(context.BodyText))
        {
            return new PolicyDecision
            {
                Decision = PolicyDecisionType.HoldForApproval,
                Reason = "High-risk keyword detected in email body",
                ConfidenceScore = context.ConfidenceScore,
                Category = context.Category
            };
        }

        if (IsVipDomain(context.SenderEmail))
        {
            return new PolicyDecision
            {
                Decision = PolicyDecisionType.HoldForApproval,
                Reason = "VIP domain sender - requires human review",
                ConfidenceScore = context.ConfidenceScore,
                Category = context.Category
            };
        }

        if (!policy.AutoExecute)
        {
            return new PolicyDecision
            {
                Decision = PolicyDecisionType.HoldForApproval,
                Reason = $"Policy disabled auto-execution for {context.Category}",
                ConfidenceScore = context.ConfidenceScore,
                Category = context.Category
            };
        }

        if (context.ConfidenceScore < policy.ConfidenceThreshold)
        {
            return new PolicyDecision
            {
                Decision = PolicyDecisionType.HoldForApproval,
                Reason = $"Confidence score {context.ConfidenceScore:P0} below threshold {policy.ConfidenceThreshold:P0}",
                ConfidenceScore = context.ConfidenceScore,
                Category = context.Category
            };
        }

        if (!string.IsNullOrEmpty(policy.ExcludedKeywords))
        {
            var excludedKeywords = policy.ExcludedKeywords
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim().ToLowerInvariant())
                .Where(k => !string.IsNullOrEmpty(k));

            foreach (var keyword in excludedKeywords)
            {
                if (context.BodyText.ToLowerInvariant().Contains(keyword))
                {
                    return new PolicyDecision
                    {
                        Decision = PolicyDecisionType.HoldForApproval,
                        Reason = $"Excluded keyword '{keyword}' found in email",
                        ConfidenceScore = context.ConfidenceScore,
                        Category = context.Category
                    };
                }
            }
        }

        return new PolicyDecision
        {
            Decision = PolicyDecisionType.AutoExecute,
            Reason = $"Auto-approved: {context.Category} with {context.ConfidenceScore:P0} confidence",
            ConfidenceScore = context.ConfidenceScore,
            Category = context.Category,
            ShouldSendCallback = true
        };
    }

    private async Task<WorkflowPolicy?> GetPolicyAsync(string category, Guid tenantId)
    {
        var cacheKey = $"policy:{tenantId}:{category}";
        if (_cache.TryGetValue(cacheKey, out WorkflowPolicy? cachedPolicy))
        {
            return cachedPolicy;
        }

        var policy = await _dbContext.WorkflowPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Category == category);

        if (policy != null)
        {
            _cache.Set(cacheKey, policy, CacheDuration);
        }

        return policy;
    }

    private static bool ContainsHighRiskKeyword(string body)
    {
        var lowerBody = body.ToLowerInvariant();
        return HighRiskKeywords.Any(keyword => lowerBody.Contains(keyword));
    }

    private static bool IsVipDomain(string email)
    {
        var domain = email.Split('@').LastOrDefault()?.ToLowerInvariant() ?? "";
        var vipDomains = new[] { ".gov", ".mil", ".edu", ".org" };
        return vipDomains.Any(d => domain.EndsWith(d));
    }
}
