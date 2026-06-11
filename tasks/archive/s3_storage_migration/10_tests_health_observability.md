# Phase 10 — Tests, Health Checks, Observability, and Hardening

> **Obsolete / Historical Migration Plan**
> Superseded by later storage and observability implementation by 2026-06-11.
> Use current code, `memory/memory-feature-map.md`, and `tasks/production/current-state-audit.md` before relying on this phase plan.

## Phase Goal

Complete the quality layer for the storage migration:
- Storage health check endpoint for load balancer readiness
- Orphan detection job (MinIO objects without DB records)
- Integration test coverage for the full upload-download-delete flow
- Verify all existing tests still pass after all migration phases

---

## Scope

### Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Api/Health/StorageHealthCheck.cs` | ASP.NET Core health check for object storage |
| `OrvixFlow.Infrastructure/Storage/OrphanDetectionJob.cs` | Hangfire job detecting MinIO objects without `StoredObject` rows |
| `OrvixFlow.Tests/Storage/StorageIntegrationTests.cs` | End-to-end upload → download → delete tests |

### Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Api/Program.cs` | Register health check, add `/health/storage` endpoint |

---

## Prerequisites

- Phases 02–09 complete
- `dotnet test` passes
- MinIO running in Docker

---

## Implementation Instructions

### Step 1 — Storage Health Check

**File:** `OrvixFlow.Api/Health/StorageHealthCheck.cs` (new file)

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Util;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace OrvixFlow.Api.Health;

public class StorageHealthCheck : IHealthCheck
{
    private readonly IAmazonS3? _s3;
    private readonly string? _bucket;
    private readonly ILogger<StorageHealthCheck> _logger;

    public StorageHealthCheck(IAmazonS3? s3, string? bucket, ILogger<StorageHealthCheck> logger)
    {
        _s3 = s3;
        _bucket = bucket;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_s3 == null || string.IsNullOrEmpty(_bucket))
        {
            // Local filesystem provider — no remote check needed
            return HealthCheckResult.Healthy("Storage provider is Local (no remote check required).");
        }

        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3, _bucket);
            if (exists)
                return HealthCheckResult.Healthy($"MinIO bucket '{_bucket}' is accessible.");

            return HealthCheckResult.Unhealthy($"MinIO bucket '{_bucket}' does not exist.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage health check failed.");
            return HealthCheckResult.Unhealthy("MinIO unreachable.", ex);
        }
    }
}
```

**Register in `Program.cs`:**

```csharp
// Add after other health checks:
builder.Services.AddHealthChecks()
    .AddCheck<StorageHealthCheck>("storage", tags: new[] { "storage", "readiness" });

// Add endpoint (in or near existing /health endpoints):
app.MapHealthChecks("/health/storage", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("storage")
});
```

---

### Step 2 — Orphan Detection Job

**File:** `OrvixFlow.Infrastructure/Storage/OrphanDetectionJob.cs` (new file)

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Storage;

/// <summary>
/// Scans MinIO objects and logs any that have no corresponding StoredObject row in the DB.
/// Read-only job — does NOT delete anything. Human review required before any cleanup.
/// </summary>
public class OrphanDetectionJob
{
    private readonly AppDbContext _db;
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<OrphanDetectionJob> _logger;

    public OrphanDetectionJob(AppDbContext db, IAmazonS3 s3, string bucket, ILogger<OrphanDetectionJob> logger)
    {
        _db = db;
        _s3 = s3;
        _bucket = bucket;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Orphan detection starting for bucket '{Bucket}'.", _bucket);

        var allStoredKeys = await _db.StoredObjects
            .IgnoreQueryFilters()
            .Select(s => s.StorageKey)
            .ToHashSetAsync();

        var orphanCount = 0;
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucket,
                ContinuationToken = continuationToken,
                MaxKeys = 1000
            };

            var response = await _s3.ListObjectsV2Async(request);

            foreach (var obj in response.S3Objects)
            {
                if (!allStoredKeys.Contains(obj.Key))
                {
                    orphanCount++;
                    _logger.LogWarning(
                        "ORPHAN: key={Key} size={Size} lastModified={LastModified}",
                        obj.Key, obj.Size, obj.LastModified);
                }
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken != null);

        _logger.LogInformation("Orphan detection complete. Found {Count} orphaned objects.", orphanCount);
    }
}
```

---

### Step 3 — Integration Tests

**File:** `OrvixFlow.Tests/Storage/StorageIntegrationTests.cs` (new file)

These tests use the in-memory EF + mocked storage, not a real MinIO connection.  
Real MinIO connectivity can be tested via the Hangfire dry-run migration job offline.

```csharp
// Tests to implement:
// 1. UploadFile → KnowledgeBaseDocument created with correct DepartmentId + StoragePath
// 2. UploadFile → StoredObject row created with correct EntityType, Sha256
// 3. DownloadDocument → returns stream for authorized user
// 4. DownloadDocument → returns 403 for unauthorized department
// 5. DownloadDocument → returns 404 for cross-tenant document
// 6. DeleteDocument → calls IFileStorage.DeleteFileAsync + removes KnowledgeBaseDocument + chunks
// 7. DeleteDocument → returns 403 if user cannot access department
// 8. FileIngestionJob → buffering of non-seekable stream works
// 9. MinIOFileStorage path traversal: "../etc/passwd" in filename → no ".." in generated key
// 10. StoredObject.LifecycleStatus = "Active" on creation; can be set to "SoftDeleted"
```

---

## Validation Checklist

- [ ] `/health/storage` responds `Healthy` when MinIO is running
- [ ] `/health/storage` responds `Unhealthy` when MinIO is stopped
- [ ] Orphan detection job runs without errors
- [ ] All Phase 02–09 features covered by tests
- [ ] `dotnet test` passes with zero failures

---

## Completion Criteria

- [ ] Health check registered and endpoint responding
- [ ] Orphan detection job runnable via Hangfire
- [ ] Integration test suite written and passing

---

## Handoff to Phase 11

Phase 11 performs the final cleanup: removing `LocalFileStorage`, deleting the `uploads_data` volume, and updating all memory files to reflect the new architecture.
