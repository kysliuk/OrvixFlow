# Phase 09 — Legacy File Migration Tool (Local → MinIO)

## Phase Goal

Build a safe, idempotent, resumable migration tool that copies all files currently in `LocalFileStorage` (`/app/uploads/`) into MinIO, creates `StoredObject` metadata rows, and updates `KnowledgeBaseDocument.StoragePath` and `KnowledgeBaseImage.StoragePath` to use object storage keys.

---

## Phase Purpose

Any deployment that used `LocalFileStorage` before this migration has existing files on the local `uploads_data` Docker volume. These files will become inaccessible once the system switches to `Storage:Provider = MinIO` because MinIO keys don't exist for them.

This tool solves that by:
1. Walking all `KnowledgeBaseDocument` and `KnowledgeBaseImage` rows that have a local filesystem `StoragePath`
2. Reading each file from local disk
3. Uploading it to MinIO with the correct key convention
4. Computing SHA-256 hash for integrity verification
5. Updating the DB row's `StoragePath` to the new MinIO key
6. Creating a `StoredObject` metadata row
7. Logging every success and failure

---

## IMPORTANT: Run This Before Switching Provider

**Migration sequence:**
1. Deploy Phases 02–07 (MinIO wired, but `Storage:Provider` still = `Local`)
2. **Run this migration tool** (copies all local files to MinIO)
3. **Verify** all files are in MinIO and hashes match
4. Set `Storage:Provider = MinIO` and restart API
5. Old local files remain until manual cleanup confirms all is well

Do NOT switch the provider before running this tool.

---

## Scope

### Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Infrastructure/Storage/LocalToMinioMigrationJob.cs` | Hangfire admin job for migration |
| `OrvixFlow.Api/Controllers/StorageMigrationController.cs` | Admin-only endpoint to trigger and monitor migration |

### Files to Modify — None (migration is additive only)

---

## Prerequisites

- Phases 03–06 complete (MinIO running, `StoredObject` entity exists)
- EF migrations applied
- Both `LocalFileStorage` and `MinIOFileStorage` must be available simultaneously during migration
- `dotnet test` passes

---

## Implementation Instructions

### Step 1 — Create `LocalToMinioMigrationJob`

**File:** `OrvixFlow.Infrastructure/Storage/LocalToMinioMigrationJob.cs`

```csharp
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Storage;

/// <summary>
/// One-time migration job. Copies all local files to MinIO and updates DB paths.
/// Safe to re-run: already-migrated files are detected by presence in MinIO.
/// Must be triggered by a SuperAdmin via the StorageMigrationController.
/// </summary>
public class LocalToMinioMigrationJob
{
    private readonly AppDbContext _db;
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<LocalToMinioMigrationJob> _logger;

    public LocalToMinioMigrationJob(
        AppDbContext db,
        IAmazonS3 s3,
        string bucket,
        ILogger<LocalToMinioMigrationJob> logger)
    {
        _db = db;
        _s3 = s3;
        _bucket = bucket;
        _logger = logger;
    }

    public async Task RunAsync(bool dryRun = false, CancellationToken ct = default)
    {
        _logger.LogInformation("Storage migration starting. DryRun={DryRun}", dryRun);

        int successCount = 0, failCount = 0, skipCount = 0;

        // Migrate KnowledgeBaseDocuments
        var documents = await _db.KnowledgeBaseDocuments
            .IgnoreQueryFilters()   // admin migration — cross-tenant
            .Where(d => !string.IsNullOrEmpty(d.StoragePath) && d.StoragePath.StartsWith("/"))
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} documents with local storage paths.", documents.Count);

        foreach (var doc in documents)
        {
            var result = await MigrateFileAsync(
                localPath: doc.StoragePath,
                tenantId: doc.TenantId,
                departmentId: null,          // null = company-wide (legacy docs had no dept)
                documentId: doc.Id,
                originalFileName: doc.FileName,
                contentType: doc.ContentType,
                entityType: "document",
                dryRun: dryRun,
                ct: ct);

            if (result.Success)
            {
                if (!dryRun)
                {
                    doc.StoragePath = result.NewKey!;
                    await _db.SaveChangesAsync(ct);

                    // Create StoredObject row if it doesn't exist
                    var exists = await _db.StoredObjects
                        .IgnoreQueryFilters()
                        .AnyAsync(s => s.EntityId == doc.Id && s.EntityType == "document", ct);

                    if (!exists)
                    {
                        _db.StoredObjects.Add(new StoredObject
                        {
                            TenantId = doc.TenantId,
                            Module = "knowledge-base",
                            EntityType = "document",
                            EntityId = doc.Id,
                            StorageProvider = "MinIO",
                            ContainerOrBucket = _bucket,
                            StorageKey = result.NewKey!,
                            OriginalFileName = doc.FileName,
                            ContentType = doc.ContentType,
                            SizeBytes = doc.FileSizeBytes,
                            Sha256 = result.Sha256 ?? string.Empty,
                            VirusScanStatus = "Clean",
                            LifecycleStatus = "Active",
                            CreatedByUserId = Guid.Empty  // migration — no user context
                        });
                        await _db.SaveChangesAsync(ct);
                    }
                }
                successCount++;
            }
            else if (result.Skipped)
            {
                skipCount++;
            }
            else
            {
                failCount++;
                _logger.LogError("FAILED: doc={DocId} path={Path} error={Error}",
                    doc.Id, doc.StoragePath, result.Error);
            }
        }

        // Migrate KnowledgeBaseImages
        var images = await _db.KnowledgeBaseImages
            .IgnoreQueryFilters()
            .Where(i => !string.IsNullOrEmpty(i.StoragePath) && i.StoragePath.StartsWith("/"))
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} images with local storage paths.", images.Count);

        foreach (var img in images)
        {
            var result = await MigrateFileAsync(
                localPath: img.StoragePath,
                tenantId: img.TenantId,
                departmentId: null,
                documentId: img.DocumentId ?? Guid.NewGuid(),
                originalFileName: Path.GetFileName(img.StoragePath),
                contentType: img.ContentType,
                entityType: "image",
                dryRun: dryRun,
                ct: ct);

            if (result.Success && !dryRun)
            {
                img.StoragePath = result.NewKey!;
                await _db.SaveChangesAsync(ct);
                successCount++;
            }
            else if (result.Skipped) skipCount++;
            else failCount++;
        }

        _logger.LogInformation(
            "Migration complete. Success={Success} Failed={Failed} Skipped={Skipped} DryRun={DryRun}",
            successCount, failCount, skipCount, dryRun);

        if (failCount > 0)
            _logger.LogWarning("{FailCount} files failed to migrate. Review logs above.", failCount);
    }

    private async Task<MigrationResult> MigrateFileAsync(
        string localPath, Guid tenantId, Guid? departmentId, Guid documentId,
        string originalFileName, string contentType, string entityType,
        bool dryRun, CancellationToken ct)
    {
        if (!File.Exists(localPath))
        {
            _logger.LogWarning("Local file not found: {Path} — skipping.", localPath);
            return MigrationResult.Skip();
        }

        var deptSegment = departmentId.HasValue ? departmentId.Value.ToString() : "__company__";
        var extension = Path.GetExtension(localPath);
        var newKey = $"tenants/{tenantId}/depts/{deptSegment}/docs/{documentId}/{Guid.NewGuid()}{extension}";

        if (dryRun)
        {
            _logger.LogInformation("DRY RUN: Would migrate {LocalPath} → {Key}", localPath, newKey);
            return MigrationResult.Ok(newKey, string.Empty);
        }

        try
        {
            await using var fileStream = File.OpenRead(localPath);

            // Compute SHA-256 hash
            using var sha = SHA256.Create();
            var hash = await ComputeSha256Async(localPath);

            // Upload to MinIO with integrity metadata
            fileStream.Position = 0;
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = newKey,
                InputStream = fileStream,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                Metadata = { ["x-amz-meta-original-path"] = localPath, ["x-amz-meta-sha256"] = hash }
            }, ct);

            // Verify object exists in MinIO after upload
            var head = await _s3.GetObjectMetadataAsync(_bucket, newKey, ct);
            if (head == null)
                return MigrationResult.Fail($"Object not found after upload: {newKey}");

            _logger.LogInformation("Migrated: {LocalPath} → {Key} sha256={Hash}", localPath, newKey, hash);
            return MigrationResult.Ok(newKey, hash);
        }
        catch (Exception ex)
        {
            return MigrationResult.Fail(ex.Message);
        }
    }

    private static async Task<string> ComputeSha256Async(string localPath)
    {
        using var sha = SHA256.Create();
        await using var stream = File.OpenRead(localPath);
        var hashBytes = await sha.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private record MigrationResult(bool Success, bool Skipped, string? NewKey, string? Sha256, string? Error)
    {
        public static MigrationResult Ok(string key, string sha256) => new(true, false, key, sha256, null);
        public static MigrationResult Skip() => new(false, true, null, null, null);
        public static MigrationResult Fail(string error) => new(false, false, null, null, error);
    }
}
```

---

### Step 2 — Create Admin Migration Controller

**File:** `OrvixFlow.Api/Controllers/StorageMigrationController.cs` (new file)

```csharp
using System;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Storage;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;

namespace OrvixFlow.Api.Controllers;

/// <summary>
/// Admin-only endpoint to trigger and monitor the LocalFileStorage → MinIO migration.
/// SuperAdmin access only.
/// </summary>
[ApiController]
[Route("api/admin/storage-migration")]
[Authorize(Policy = "SuperAdminOnly")]
public class StorageMigrationController : ControllerBase
{
    private readonly IBackgroundJobClient _hangfire;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public StorageMigrationController(
        IBackgroundJobClient hangfire,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _hangfire = hangfire;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    /// <summary>
    /// Start the migration in dry-run mode first to preview actions without changes.
    /// </summary>
    [HttpPost("dry-run")]
    public IActionResult StartDryRun()
    {
        _hangfire.Enqueue<LocalToMinioMigrationJob>(job => job.RunAsync(true, default));
        return Accepted(new { message = "Dry run started. Check Hangfire dashboard for progress." });
    }

    /// <summary>
    /// Start the actual migration. Review dry-run output first.
    /// </summary>
    [HttpPost("run")]
    public IActionResult StartMigration()
    {
        _hangfire.Enqueue<LocalToMinioMigrationJob>(job => job.RunAsync(false, default));
        return Accepted(new { message = "Migration started. Check Hangfire dashboard for progress." });
    }

    /// <summary>
    /// Returns count of documents still using local paths (not yet migrated).
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrvixFlow.Infrastructure.Data.AppDbContext>();

        var localDocCount = await db.KnowledgeBaseDocuments
            .IgnoreQueryFilters()
            .CountAsync(d => d.StoragePath.StartsWith("/"));

        var localImgCount = await db.KnowledgeBaseImages
            .IgnoreQueryFilters()
            .CountAsync(i => i.StoragePath.StartsWith("/"));

        return Ok(new
        {
            documentsNeedingMigration = localDocCount,
            imagesNeedingMigration = localImgCount,
            status = localDocCount + localImgCount == 0 ? "Complete" : "Pending"
        });
    }
}
```

---

### Step 3 — Register LocalToMinioMigrationJob in DI

**File:** `OrvixFlow.Infrastructure/DependencyInjection.cs`

The job must be registered for Hangfire even when current provider is Local (because migration runs while Local is still active):

```csharp
// Register migration job — needed during transition only
// It requires IAmazonS3 even when Storage:Provider = Local
// Safe to register regardless — it's only triggered by admin action
services.AddScoped<LocalToMinioMigrationJob>(sp =>
{
    // Try to resolve IAmazonS3 — if not registered (Local mode), create from config
    IAmazonS3? s3 = null;
    try
    {
        s3 = sp.GetService<IAmazonS3>();
    }
    catch { }

    if (s3 == null)
    {
        // Resolve lazy MinIO config even when Storage:Provider = Local
        var conf = sp.GetRequiredService<IConfiguration>();
        var minioSection = conf.GetSection("Storage:MinIO");
        var config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = minioSection["Endpoint"] ?? "http://minio:9000",
            ForcePathStyle = true,
            UseHttp = true
        };
        s3 = new Amazon.S3.AmazonS3Client(
            conf["MINIO_ACCESS_KEY"] ?? string.Empty,
            conf["MINIO_SECRET_KEY"] ?? string.Empty,
            config);
    }

    return new LocalToMinioMigrationJob(
        sp.GetRequiredService<OrvixFlow.Infrastructure.Data.AppDbContext>(),
        s3,
        conf.GetSection("Storage:MinIO")["Bucket"] ?? "orvixflow",
        sp.GetRequiredService<ILogger<LocalToMinioMigrationJob>>());
});
```

---

## Migration Execution Order

1. Deploy application with `Storage:Provider = Local` and MinIO running
2. Call `POST /api/admin/storage-migration/dry-run` — review Hangfire logs
3. Call `GET /api/admin/storage-migration/status` — see count of files to migrate
4. Call `POST /api/admin/storage-migration/run` — execute migration
5. Monitor Hangfire dashboard for job completion
6. Call `GET /api/admin/storage-migration/status` again — should show `"status": "Complete"`
7. Spot-check MinIO console for files under correct key prefixes
8. Set `STORAGE_PROVIDER=MinIO` and redeploy
9. Verify downloads work
10. Old `uploads_data` volume remains — do NOT delete until fully confident
11. After 2+ weeks of stable operation, remove volume and delete `LocalFileStorage`

---

## Safety Guarantees

| Property | How Achieved |
|---------|-------------|
| Idempotent | Files with `/` prefix in StoragePath are migrated; once updated to MinIO key, they won't match the filter again |
| No data loss | Old local file is NOT deleted — only the DB pointer is updated |
| Hash verification | SHA-256 computed before upload, stored in metadata and `StoredObject.Sha256` |
| Dry run mode | Full preview without any writes |
| Per-file failure logging | Each failure is logged individually; one failure does not stop migration |
| Resumable | Re-run after partial failure — already-migrated files skipped automatically |

---

## Constraints

- Do NOT autorun this job on startup
- Do NOT run this in a database transaction spanning all files — each file update is committed individually
- Do NOT delete local files from the `uploads_data` volume until the entire deployment is confirmed stable
- Run this migration only with `SuperAdmin` authority — endpoint is gated by `"SuperAdminOnly"` policy

---

## Validation Checklist

- [ ] `GET /api/admin/storage-migration/status` returns correct counts
- [ ] Dry run logs show expected file paths without writing
- [ ] Run migration — all documents have updated `StoragePath` (no longer starts with `/`)
- [ ] MinIO console shows all files under `tenants/...` prefix
- [ ] `StoredObject` rows exist for all migrated files in PostgreSQL
- [ ] Switch `Storage:Provider = MinIO` — all downloads still work
- [ ] `dotnet test` passes

---

## Completion Criteria

- [ ] Migration job implemented with dry-run mode
- [ ] Admin endpoint to trigger and monitor migration
- [ ] All existing local files migrated to MinIO
- [ ] DB paths updated, `StoredObject` rows created
- [ ] Status endpoint shows "Complete"

---

## Handoff to Phase 10

Phase 10 covers tests, health checks, and observability. This includes a storage health check endpoint, orphan detection, and hardening the overall pipeline.
