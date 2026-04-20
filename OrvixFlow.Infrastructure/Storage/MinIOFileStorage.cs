using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Storage;

public class MinIOFileStorage : IFileStorage
{
    private const string CompanySentinel = "__company__";

    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<MinIOFileStorage> _logger;

    public MinIOFileStorage(IAmazonS3 s3, string bucket, ILogger<MinIOFileStorage> logger)
    {
        _s3 = s3;
        _bucket = bucket;
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
        var key = BuildKey(ctx);

        _logger.LogInformation(
            "Uploading object to MinIO: bucket={Bucket}, key={Key}, tenant={TenantId}",
            _bucket,
            key,
            ctx.TenantId);

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = fileStream,
            ContentType = "application/octet-stream",
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        await _s3.PutObjectAsync(request);

        _logger.LogInformation("Object uploaded successfully: {Key}", key);
        return key;
    }

    public Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream)
        => SaveFileAsync(new StorageContext(tenantId, null, documentId, fileName), fileStream);

    public async Task<Stream> GetFileAsync(string storagePath)
    {
        _logger.LogInformation("Retrieving object from MinIO: {Key}", storagePath);

        var response = await _s3.GetObjectAsync(_bucket, storagePath);
        return response.ResponseStream;
    }

    public async Task DeleteFileAsync(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        _logger.LogInformation("Deleting object from MinIO: {Key}", storagePath);

        await _s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucket,
            Key = storagePath
        });
    }
}
