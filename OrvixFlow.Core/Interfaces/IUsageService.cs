using System;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

/// <summary>
/// Shadow module: Metered-Billing
/// Counts AI token usage and n8n node executions per company/module.
/// </summary>
public interface IUsageService
{
    /// <summary>Record AI token consumption.</summary>
    Task RecordTokensAsync(Guid companyId, string moduleKey, int tokenCount, Guid? userId = null, Guid? departmentId = null);

    /// <summary>Record n8n workflow node executions.</summary>
    Task RecordN8nExecutionAsync(Guid companyId, string moduleKey, int nodeCount, Guid? userId = null, Guid? departmentId = null);

    /// <summary>Get total usage quantities for a company grouped by metric type.</summary>
    Task<UsageSummary> GetCompanySummaryAsync(Guid companyId);
}

public record UsageSummary(decimal TotalAiTokens, decimal TotalN8nNodes);
