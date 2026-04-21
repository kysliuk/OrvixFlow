using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrvixFlow.Infrastructure.Storage;

public class AzureBlobContainerInitializer : IHostedService
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobContainerInitializer> _logger;

    public AzureBlobContainerInitializer(BlobContainerClient container, ILogger<AzureBlobContainerInitializer> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var created = await _container.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            if (created?.Value != null)
            {
                _logger.LogInformation("Azure Blob container '{Container}' created.", _container.Name);
            }
            else
            {
                _logger.LogInformation("Azure Blob container '{Container}' already exists.", _container.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Blob container '{Container}'.", _container.Name);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
