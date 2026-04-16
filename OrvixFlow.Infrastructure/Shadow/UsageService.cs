using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using static OrvixFlow.Core.Entities.UsageMetric;

namespace OrvixFlow.Infrastructure.Shadow;

/// <summary>
/// Metered-Billing shadow module implementation.
/// Records AI token consumption and n8n node executions per company/module.
/// </summary>
public sealed class UsageService : IUsageService
{
    private readonly AppDbContext _db;

    public UsageService(AppDbContext db) => _db = db;

    public Task RecordTokensAsync(
        Guid companyId, string moduleKey, int tokenCount,
        Guid? userId = null, Guid? departmentId = null)
        => WriteEventAsync(companyId, moduleKey, "ai-tokens", tokenCount, userId, departmentId);

    public Task RecordN8nExecutionAsync(
        Guid companyId, string moduleKey, int nodeCount,
        Guid? userId = null, Guid? departmentId = null)
        => WriteEventAsync(companyId, moduleKey, "n8n-nodes", nodeCount, userId, departmentId);

    public async Task<UsageSummary> GetCompanySummaryAsync(Guid companyId)
    {
        return await GetCompanySummaryAsync(companyId, periodStart: null);
    }

    /// <summary>
    /// Returns usage totals for the specified period window.
    /// Pass null for periodStart to get lifetime totals (legacy behaviour).
    /// </summary>
    public async Task<UsageSummary> GetCompanySummaryAsync(Guid companyId, DateTime? periodStart)
    {
        var query = _db.UsageEvents
            .IgnoreQueryFilters()
            .Where(e => e.CompanyId == companyId);

        if (periodStart.HasValue)
        {
            query = query.Where(e => e.OccurredAt >= periodStart.Value);
        }

        var events = await query
            .GroupBy(e => e.MetricType)
            .Select(g => new { MetricType = g.Key, Total = g.Sum(e => e.Quantity) })
            .ToListAsync();

        var tokens  = events.FirstOrDefault(e => e.MetricType == AiTokens)?.Total ?? 0;
        var nodes   = events.FirstOrDefault(e => e.MetricType == N8nNodes)?.Total ?? 0;
        var storage = events.FirstOrDefault(e => e.MetricType == StorageMb)?.Total ?? 0;
        var kbCount = events.FirstOrDefault(e => e.MetricType == KnowledgeBases)?.Total ?? 0;
        var inbox   = events.FirstOrDefault(e => e.MetricType == InboxMessages)?.Total ?? 0;

        return new UsageSummary(tokens, nodes, storage, kbCount, inbox);
    }

    public Task RecordStorageAsync(
        Guid companyId, string moduleKey, int megabytes,
        Guid? userId = null, Guid? departmentId = null)
        => WriteEventAsync(companyId, moduleKey, StorageMb, megabytes, userId, departmentId);

    public Task RecordKnowledgeBaseAsync(
        Guid companyId, string moduleKey, int count,
        Guid? userId = null, Guid? departmentId = null)
        => WriteEventAsync(companyId, moduleKey, KnowledgeBases, count, userId, departmentId);

    public Task RecordInboxMessageAsync(
        Guid companyId, string moduleKey, int messageCount,
        Guid? userId = null, Guid? departmentId = null)
        => WriteEventAsync(companyId, moduleKey, InboxMessages, messageCount, userId, departmentId);

    private async Task WriteEventAsync(
        Guid companyId, string moduleKey, string metricType,
        decimal quantity, Guid? userId, Guid? departmentId)
    {
        _db.UsageEvents.Add(new UsageEvent
        {
            CompanyId    = companyId,
            DepartmentId = departmentId,
            UserId       = userId,
            ModuleKey    = moduleKey,
            MetricType   = metricType,
            Quantity     = quantity,
            OccurredAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
