using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Storage;

/// <summary>
/// One-time admin migration job that copies legacy local files into MinIO-compatible object storage.
/// The job is safe to re-run because only rows with absolute local filesystem paths are selected.
/// </summary>
public class LocalToMinioMigrationJob : IDisposable
{
    private const string CompanySentinel = "__company__";

    private readonly AppDbContext _db;
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string _basePath;
    private readonly ILogger<LocalToMinioMigrationJob> _logger;
    private readonly bool _ownsS3Client;

    public LocalToMinioMigrationJob(
        AppDbContext db,
        IAmazonS3 s3,
        string bucket,
        string basePath,
        ILogger<LocalToMinioMigrationJob> logger,
        bool ownsS3Client = false)
    {
        _db = db;
        _s3 = s3;
        _bucket = bucket;
        _basePath = Path.GetFullPath(basePath);
        if (!_basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            _basePath += Path.DirectorySeparatorChar;
        }
        _logger = logger;
        _ownsS3Client = ownsS3Client;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task RunAsync(bool dryRun = false, CancellationToken ct = default)
    {
        _logger.LogInformation("Storage migration starting. DryRun={DryRun}", dryRun);

        var successCount = 0;
        var failCount = 0;
        var skipCount = 0;

        var documents = await _db.KnowledgeBaseDocuments
            .IgnoreQueryFilters()
            .Where(d => !string.IsNullOrWhiteSpace(d.StoragePath) && d.StoragePath.StartsWith("/"))
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} documents with local storage paths.", documents.Count);

        foreach (var document in documents)
        {
            var result = await MigrateFileAsync(
                localPath: document.StoragePath,
                tenantId: document.TenantId,
                departmentId: document.DepartmentId,
                documentId: document.Id,
                originalFileName: document.FileName,
                contentType: document.ContentType,
                ct: ct,
                dryRun: dryRun);

            if (result.Skipped)
            {
                skipCount++;
                continue;
            }

            if (!result.Success)
            {
                failCount++;
                _logger.LogError(
                    "Failed migrating document {DocumentId} from {LocalPath}: {Error}",
                    document.Id,
                    document.StoragePath,
                    result.Error);
                continue;
            }

            successCount++;

            if (dryRun)
            {
                continue;
            }

            var previousStoragePath = document.StoragePath;
            document.StoragePath = result.NewKey!;

            await EnsureStoredObjectAsync(
                currentStoragePath: previousStoragePath,
                entityType: "document",
                entityId: document.Id,
                tenantId: document.TenantId,
                departmentId: document.DepartmentId,
                storageKey: result.NewKey!,
                originalFileName: document.FileName,
                contentType: document.ContentType,
                sizeBytes: document.FileSizeBytes,
                sha256: result.Sha256!,
                ct: ct);

            await _db.SaveChangesAsync(ct);
        }

        var images = await _db.KnowledgeBaseImages
            .IgnoreQueryFilters()
            .Where(i => !string.IsNullOrWhiteSpace(i.StoragePath) && i.StoragePath.StartsWith("/"))
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} images with local storage paths.", images.Count);

        foreach (var image in images)
        {
            var entityId = image.Id;
            var originalFileName = Path.GetFileName(image.StoragePath);

            var result = await MigrateFileAsync(
                localPath: image.StoragePath,
                tenantId: image.TenantId,
                departmentId: null,
                documentId: entityId,
                originalFileName: originalFileName,
                contentType: image.ContentType,
                ct: ct,
                dryRun: dryRun);

            if (result.Skipped)
            {
                skipCount++;
                continue;
            }

            if (!result.Success)
            {
                failCount++;
                _logger.LogError(
                    "Failed migrating image {ImageId}: {Error}",
                    image.Id,
                    result.Error);
                continue;
            }

            successCount++;

            if (dryRun)
            {
                continue;
            }

            var previousStoragePath = image.StoragePath;
            image.StoragePath = result.NewKey!;

            await EnsureStoredObjectAsync(
                currentStoragePath: previousStoragePath,
                entityType: "image",
                entityId: entityId,
                tenantId: image.TenantId,
                departmentId: null,
                storageKey: result.NewKey!,
                originalFileName: originalFileName,
                contentType: image.ContentType,
                sizeBytes: result.SizeBytes,
                sha256: result.Sha256!,
                ct: ct);

            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Storage migration complete. Success={Success} Failed={Failed} Skipped={Skipped} DryRun={DryRun}",
            successCount,
            failCount,
            skipCount,
            dryRun);

        if (failCount > 0)
        {
            _logger.LogWarning("{FailCount} files failed to migrate. Review the previous log entries.", failCount);
        }
    }

    private async Task EnsureStoredObjectAsync(
        string currentStoragePath,
        string entityType,
        Guid entityId,
        Guid tenantId,
        Guid? departmentId,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        CancellationToken ct)
    {
        var storedObject = await _db.StoredObjects
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.StorageKey == currentStoragePath, ct);

        if (storedObject == null)
        {
            storedObject = new StoredObject
            {
                TenantId = tenantId,
                DepartmentId = departmentId,
                Module = "knowledge-base",
                EntityType = entityType,
                EntityId = entityId,
                CreatedByUserId = Guid.Empty
            };

            _db.StoredObjects.Add(storedObject);
        }

        storedObject.TenantId = tenantId;
        storedObject.DepartmentId = departmentId;
        storedObject.Module = "knowledge-base";
        storedObject.EntityType = entityType;
        storedObject.EntityId = entityId;
        storedObject.StorageProvider = "MinIO";
        storedObject.ContainerOrBucket = _bucket;
        storedObject.StorageKey = storageKey;
        storedObject.OriginalFileName = string.IsNullOrWhiteSpace(storedObject.OriginalFileName)
            ? originalFileName
            : storedObject.OriginalFileName;
        storedObject.ContentType = contentType;
        storedObject.SizeBytes = sizeBytes;
        storedObject.Sha256 = sha256;
        storedObject.VirusScanStatus = "Pending";
        storedObject.LifecycleStatus = "Active";
    }

    private async Task<MigrationResult> MigrateFileAsync(
        string localPath,
        Guid tenantId,
        Guid? departmentId,
        Guid documentId,
        string originalFileName,
        string contentType,
        CancellationToken ct,
        bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return MigrationResult.Fail("Local path is empty.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(localPath);
        }
        catch (Exception ex)
        {
            return MigrationResult.Fail($"Invalid path format: {ex.Message}");
        }

        if (!fullPath.StartsWith(_basePath, StringComparison.Ordinal))
        {
            _logger.LogWarning("Path traversal or outside base path blocked: {Path}", localPath);
            return MigrationResult.Fail("Path is outside the allowed base directory.");
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Local file not found for migration: {FullPath}", fullPath);
            return MigrationResult.Skip();
        }

        var newKey = BuildKey(tenantId, departmentId, documentId, originalFileName, fullPath);

        if (dryRun)
        {
            _logger.LogInformation("Dry run: would migrate file to {StorageKey}", newKey);
            return MigrationResult.Ok(newKey, string.Empty, new FileInfo(localPath).Length);
        }

        try
        {
            var sha256 = await ComputeSha256Async(fullPath, ct);
            await using var stream = File.OpenRead(fullPath);

            var request = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = newKey,
                InputStream = stream,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            request.Metadata["x-amz-meta-sha256"] = sha256;

            await _s3.PutObjectAsync(request, ct);
            await _s3.GetObjectMetadataAsync(_bucket, newKey, ct);

            _logger.LogInformation("Migrated file to {StorageKey}", newKey);
            return MigrationResult.Ok(newKey, sha256, stream.Length);
        }
        catch (Exception ex)
        {
            return MigrationResult.Fail(ex.Message);
        }
    }

    private static string BuildKey(
        Guid tenantId,
        Guid? departmentId,
        Guid documentId,
        string originalFileName,
        string localPath)
    {
        var departmentSegment = departmentId.HasValue ? departmentId.Value.ToString() : CompanySentinel;
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = Path.GetExtension(localPath);
        }

        return $"tenants/{tenantId}/depts/{departmentSegment}/docs/{documentId}/{Guid.NewGuid()}{extension}";
    }

    private static async Task<string> ComputeSha256Async(string localPath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(localPath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record MigrationResult(bool Success, bool Skipped, string? NewKey, string? Sha256, string? Error, long SizeBytes)
    {
        public static MigrationResult Ok(string newKey, string sha256, long sizeBytes) => new(true, false, newKey, sha256, null, sizeBytes);

        public static MigrationResult Skip() => new(false, true, null, null, null, 0);

        public static MigrationResult Fail(string error) => new(false, false, null, null, error, 0);
    }

    public void Dispose()
    {
        if (_ownsS3Client && _s3 is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
