using System;
using System.Text.Json;
using System.Threading.Tasks;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Ai;

public class RagMetricsCollector : IRagMetricsCollector
{
    private readonly IAuditService _auditService;

    public RagMetricsCollector(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task RecordRetrievalMetricsAsync(
        Guid tenantId, 
        Guid traceId, 
        int snippetCount, 
        int imageCount, 
        double durationMs, 
        string model)
    {
        var details = new
        {
            TraceId = traceId,
            Operation = "rag.retrieval",
            SnippetCount = snippetCount,
            ImageCount = imageCount,
            DurationMs = durationMs,
            Model = model,
            Timestamp = DateTime.UtcNow
        };

        await _auditService.RecordAsync(
            tenantId, 
            "rag.retrieval.metrics", 
            JsonSerializer.Serialize(details));
    }

    public async Task RecordIngestionMetricsAsync(
        Guid tenantId, 
        Guid documentId, 
        int chunkCount, 
        int imageCount, 
        double durationMs)
    {
        var details = new
        {
            DocumentId = documentId,
            Operation = "rag.ingestion",
            ChunkCount = chunkCount,
            ImageCount = imageCount,
            DurationMs = durationMs,
            Timestamp = DateTime.UtcNow
        };

        await _auditService.RecordAsync(
            tenantId, 
            "rag.ingestion.metrics", 
            JsonSerializer.Serialize(details));
    }
}
