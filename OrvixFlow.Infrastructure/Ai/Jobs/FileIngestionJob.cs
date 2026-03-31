using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Ai.Jobs;

public class FileIngestionJob
{
    private readonly IIngestionPipelineService _pipeline;
    private readonly IFileStorage _storage;
    private readonly ILogger<FileIngestionJob> _logger;

    public FileIngestionJob(
        IIngestionPipelineService pipeline,
        IFileStorage storage,
        ILogger<FileIngestionJob> logger)
    {
        _pipeline = pipeline;
        _storage = storage;
        _logger = logger;
    }

    public async Task ProcessFileAsync(Guid documentId, string storagePath, string fileName, string contentType, Guid? userId = null, Guid? departmentId = null)
    {
        _logger.LogInformation("Processing background ingestion for file {FileName} (DocID: {DocumentId})", fileName, documentId);

        try
        {
            using (var stream = await _storage.GetFileAsync(storagePath))
            {
                var result = await _pipeline.IngestFileAsync(stream, fileName, contentType, userId, departmentId);
                
                if (result.ErrorMessage != null)
                {
                    _logger.LogError("Ingestion failed for file {FileName}: {Error}", fileName, result.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation("Successfully ingested file {FileName}. Chunks: {Chunks}, Images: {Images}", fileName, result.ChunkCount, result.ImageCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in background ingestion job for file {FileName}", fileName);
            throw; // Retried by Hangfire
        }
    }
}
