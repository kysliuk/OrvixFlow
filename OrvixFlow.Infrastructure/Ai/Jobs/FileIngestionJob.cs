using System;
using System.IO;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;
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
        _logger.LogInformation("Processing background ingestion for file {FileName} (DocID: {DocumentId}, Tenant: {TenantId})", fileName, documentId, tenantId);

        using var scope = _serviceProvider.CreateScope();
        
        var pipeline = scope.ServiceProvider.GetRequiredService<IIngestionPipelineService>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        try
        {
            using (var stream = await storage.GetFileAsync(storagePath))
            {
                var result = await pipeline.IngestFileAsync(stream, fileName, contentType, documentId, tenantId, userId, departmentId);
                
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
}
