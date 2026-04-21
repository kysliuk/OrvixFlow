using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using TenantProviderFactory = OrvixFlow.Infrastructure.Services.TenantProviderFactory;

namespace OrvixFlow.Infrastructure.Ai.Jobs;

public class FileIngestionJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantProviderFactory _tenantProviderFactory;
    private readonly ILogger<FileIngestionJob> _logger;

    public FileIngestionJob(
        IServiceProvider serviceProvider,
        ITenantProviderFactory tenantProviderFactory,
        ILogger<FileIngestionJob> logger)
    {
        _serviceProvider = serviceProvider;
        _tenantProviderFactory = tenantProviderFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task ProcessFileAsync(Guid documentId, string storagePath, string fileName, string contentType, Guid? userId = null, Guid? departmentId = null, Guid tenantId = default)
    {
        _logger.LogInformation(
            "FileIngestionJob starting: doc={DocumentId} storageKey={StorageKey} tenant={TenantId}",
            documentId,
            storagePath,
            tenantId);

        using var scope = _serviceProvider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IIngestionPipelineService>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        Stream? fileStream = null;

        try
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    fileStream = await storage.GetFileAsync(storagePath);
                    break;
                }
                catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
                {
                    _logger.LogError(
                        ex,
                        "File not found in object storage: key={StorageKey} doc={DocumentId}. Not retrying.",
                        storagePath,
                        documentId);

                    await UpdateDocumentStatusAsync(
                        scope,
                        documentId,
                        "Failed",
                        $"File not found in storage at key: {storagePath}");

                    return;
                }
                catch (Exception ex) when (attempt < 3)
                {
                    _logger.LogWarning(
                        ex,
                        "Transient storage fetch error on attempt {Attempt}/3 for key={StorageKey}",
                        attempt,
                        storagePath);

                    await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
                }
            }

            if (fileStream == null)
            {
                _logger.LogError("All storage fetch attempts failed for key={StorageKey}", storagePath);
                throw new InvalidOperationException($"Cannot fetch file from storage key: {storagePath}");
            }

            using (fileStream)
            {
                var result = await pipeline.IngestFileAsync(fileStream, fileName, contentType, documentId, tenantId, userId, departmentId);

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
            throw;
        }
    }

    private static async Task UpdateDocumentStatusAsync(IServiceScope scope, Guid documentId, string status, string errorMessage)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = await dbContext.KnowledgeBaseDocuments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == documentId);

        if (document == null)
        {
            return;
        }

        document.Status = status;
        document.ErrorMessage = errorMessage;
        await dbContext.SaveChangesAsync();
    }
}
