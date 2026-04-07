using System;
using System.Threading.Tasks;
using System.IO;

namespace OrvixFlow.Core.Interfaces;

public record IngestionResult(
    Guid DocumentId,
    int ChunkCount,
    int ImageCount,
    string? ErrorMessage = null
);

public interface IIngestionPipelineService
{
    Task<IngestionResult> IngestFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid documentId,
        Guid tenantId,
        Guid? userId = null,
        Guid? departmentId = null);
}
