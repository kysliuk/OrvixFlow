using System;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface IRagMetricsCollector
{
    Task RecordRetrievalMetricsAsync(
        Guid tenantId, 
        Guid traceId, 
        int snippetCount, 
        int imageCount, 
        double durationMs, 
        string model);

    Task RecordIngestionMetricsAsync(
        Guid tenantId, 
        Guid documentId, 
        int chunkCount, 
        int imageCount, 
        double durationMs);
}
