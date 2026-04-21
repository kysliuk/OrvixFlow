using System;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Storage;

public class AzureBlobFileStorage : IFileStorage
{
    private const string CompanySentinel = "__company__";

    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobFileStorage> _logger;

    public AzureBlobFileStorage(BlobContainerClient container, ILogger<AzureBlobFileStorage> logger)
    {
        _container = container;
        _logger = logger;
    }

    private static string BuildKey(StorageContext ctx)
    {
        var departmentSegment = ctx.DepartmentId.HasValue
            ? ctx.DepartmentId.Value.ToString()
            : CompanySentinel;
        var extension = Path.GetExtension(ctx.OriginalFileName);

        return $"tenants/{ctx.TenantId}/depts/{departmentSegment}/docs/{ctx.DocumentId}/{Guid.NewGuid()}{extension}";
    }

    public async Task<string> SaveFileAsync(StorageContext ctx, Stream fileStream)
    {
        var blobName = BuildKey(ctx);

        _logger.LogInformation(
            "Uploading blob to Azure: container={Container}, blob={Blob}, tenant={TenantId}",
            _container.Name,
            blobName,
            ctx.TenantId);

        var blobClient = _container.GetBlobClient(blobName);
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/octet-stream"
            }
        };

        await blobClient.UploadAsync(fileStream, options);

        _logger.LogInformation("Blob uploaded successfully: {Blob}", blobName);
        return blobName;
    }

    public Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream)
        => SaveFileAsync(new StorageContext(tenantId, null, documentId, fileName), fileStream);

    public async Task<Stream> GetFileAsync(string storagePath)
    {
        _logger.LogInformation("Downloading blob from Azure: {Blob}", storagePath);

        var blobClient = _container.GetBlobClient(storagePath);
        var result = await blobClient.DownloadStreamingAsync();
        return result.Value.Content;
    }

    public async Task DeleteFileAsync(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        _logger.LogInformation("Deleting blob from Azure: {Blob}", storagePath);

        var blobClient = _container.GetBlobClient(storagePath);

        try
        {
            await blobClient.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "BlobNotFound")
        {
            _logger.LogWarning("Blob not found during delete, treating as success: {Blob}", storagePath);
        }
    }
}
