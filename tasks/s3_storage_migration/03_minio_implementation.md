# Phase 03 — Storage Abstraction Upgrade (StorageContext + MinIO Implementation)

## Phase Goal

Replace the current raw `(tenantId, documentId, fileName)` signature with a typed `StorageContext` value object.  
Extend `IFileStorage` without breaking the existing implementation.  
Implement `MinIOFileStorage` and `MinIOBucketInitializer`.  
Add MinIO to docker-compose.  
Wire provider-selection DI so switching between Local and MinIO is config-driven only.

---

## Phase Purpose

This phase delivers the core storage layer change. After this phase:
- `LocalFileStorage` still exists as fallback (config: `Storage:Provider = Local`)
- `MinIOFileStorage` is available (config: `Storage:Provider = MinIO`)
- No business logic or controller code changes — only infrastructure layer

---

## Scope

### Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Core/Models/StorageContext.cs` | Typed value object replacing loose tuple params |
| `OrvixFlow.Infrastructure/Storage/MinIOFileStorage.cs` | S3-compatible MinIO implementation of `IFileStorage` |
| `OrvixFlow.Infrastructure/Storage/MinIOBucketInitializer.cs` | `IHostedService` — idempotent bucket creation on startup |
| `OrvixFlow.Tests/Storage/MinIOFileStorageTests.cs` | Unit tests for key building, delegation, safety |

### Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Core/Interfaces/IFileStorage.cs` | Add new `SaveFileAsync(StorageContext, Stream)` overload |
| `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs` | Implement new overload (delegates to existing method) |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Replace hardcoded `LocalFileStorage` with conditional provider switch |
| `docker-compose.yml` | Add MinIO service, volume, `orvix-api` dependency |
| `OrvixFlow.Api/appsettings.json` | Add `Storage:` config section |
| `.env` / `.env.example` | Add MinIO credentials and provider env vars |

---

## Prerequisites

- Phase 02 complete (ClamAV DI bug fixed)
- `dotnet test` passes

---

## NuGet Packages Required

Add to `OrvixFlow.Infrastructure.csproj`:

```xml
<PackageReference Include="AWSSDK.S3" Version="3.*" />
```

Run:
```bash
dotnet add OrvixFlow.Infrastructure package AWSSDK.S3
```

> `AWSSDK.S3` works with MinIO via `ForcePathStyle = true`. This same SDK also works with any S3-compatible endpoint. Azure Blob will use a different package added in Phase 08.

---

## Implementation Instructions

### Step 1 — Create `StorageContext` Value Object

**File:** `OrvixFlow.Core/Models/StorageContext.cs` (new file)

```csharp
namespace OrvixFlow.Core.Models;

/// <summary>
/// Typed context for a storage operation. Replaces loose (tenantId, departmentId, documentId, fileName) tuples.
/// Provider-neutral — no S3, MinIO, or cloud SDK types here.
/// </summary>
public record StorageContext(
    Guid TenantId,
    Guid? DepartmentId,   // null = company-wide (no department scope)
    Guid DocumentId,
    string OriginalFileName);
```

> This belongs in `OrvixFlow.Core.Models` not `OrvixFlow.Core.Entities` because it is a value object used for operations, not a persistent domain entity.

---

### Step 2 — Extend `IFileStorage`

**File:** `OrvixFlow.Core/Interfaces/IFileStorage.cs`

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Core.Interfaces;

public interface IFileStorage
{
    // Existing overload — kept for backward compat. Do NOT remove.
    // Used by: IngestionPipelineService (image save), FileIngestionJob (file read)
    Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream);
    
    // New context-aware overload — preferred for new code
    // Carries DepartmentId for proper key-prefix isolation
    Task<string> SaveFileAsync(StorageContext ctx, Stream fileStream);
    
    Task<Stream> GetFileAsync(string storagePath);
    Task DeleteFileAsync(string storagePath);
}
```

---

### Step 3 — Implement New Overload in `LocalFileStorage`

**File:** `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs`

Add the new `SaveFileAsync(StorageContext, Stream)` overload. It delegates to the existing implementation:

```csharp
// Add this method to LocalFileStorage class:
public Task<string> SaveFileAsync(StorageContext ctx, Stream fileStream)
    => SaveFileAsync(ctx.TenantId, ctx.DocumentId, ctx.OriginalFileName, fileStream);
```

The existing 4-parameter method remains unchanged. `LocalFileStorage` now satisfies both overloads.

---

### Step 4 — Implement `MinIOFileStorage`

**File:** `OrvixFlow.Infrastructure/Storage/MinIOFileStorage.cs` (new file)

```csharp
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
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<MinIOFileStorage> _logger;

    public MinIOFileStorage(IAmazonS3 s3, string bucket, ILogger<MinIOFileStorage> logger)
    {
        _s3 = s3;
        _bucket = bucket;
        _logger = logger;
    }

    /// <summary>
    /// Builds a deterministic, traversal-safe S3 key from typed StorageContext.
    /// Key structure:
    ///   tenants/{tenantId}/depts/{departmentId|__company__}/docs/{documentId}/{guid}.{ext}
    ///
    /// Rules:
    /// - All segments are server-generated GUIDs — no user-controlled path components
    /// - departmentId == null uses sentinel "__company__" (not a valid UUID — cannot collide)
    /// - GUID at end ensures no filename collisions within the same document
    /// </summary>
    private static string BuildKey(StorageContext ctx)
    {
        var deptSegment = ctx.DepartmentId.HasValue
            ? ctx.DepartmentId.Value.ToString()
            : "__company__";
        var extension = Path.GetExtension(ctx.OriginalFileName);
        // Extension may be empty for extensionless files — that is fine
        return $"tenants/{ctx.TenantId}/depts/{deptSegment}/docs/{ctx.DocumentId}/{Guid.NewGuid()}{extension}";
    }

    public async Task<string> SaveFileAsync(StorageContext ctx, Stream fileStream)
    {
        var key = BuildKey(ctx);
        _logger.LogInformation(
            "Uploading object to MinIO: bucket={Bucket}, key={Key}, tenant={TenantId}",
            _bucket, key, ctx.TenantId);

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

    /// <summary>
    /// Legacy overload — delegates to StorageContext overload with null DepartmentId.
    /// Used by IngestionPipelineService for image files and FileIngestionJob.
    /// </summary>
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
        if (string.IsNullOrEmpty(storagePath)) return;

        _logger.LogInformation("Deleting object from MinIO: {Key}", storagePath);
        await _s3.DeleteObjectAsync(_bucket, storagePath);
    }
}
```

---

### Step 5 — Implement `MinIOBucketInitializer`

**File:** `OrvixFlow.Infrastructure/Storage/MinIOBucketInitializer.cs` (new file)

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrvixFlow.Infrastructure.Storage;

/// <summary>
/// IHostedService that ensures the MinIO bucket exists on startup.
/// Runs exactly once. Idempotent — safe to re-run on every startup.
/// Throws on failure to prevent the API from starting in a broken state.
/// </summary>
public class MinIOBucketInitializer : IHostedService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<MinIOBucketInitializer> _logger;

    public MinIOBucketInitializer(IAmazonS3 s3, string bucket, ILogger<MinIOBucketInitializer> logger)
    {
        _s3 = s3;
        _bucket = bucket;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3, _bucket);
            if (!exists)
            {
                await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, cancellationToken);
                _logger.LogInformation("MinIO bucket '{Bucket}' created successfully.", _bucket);
            }
            else
            {
                _logger.LogInformation("MinIO bucket '{Bucket}' already exists.", _bucket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MinIO bucket '{Bucket}'. API startup aborted.", _bucket);
            throw; // Fail fast — object storage is critical platform infrastructure
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

---

### Step 6 — Update DI Registration

**File:** `OrvixFlow.Infrastructure/DependencyInjection.cs`

Replace the existing `LocalFileStorage` registration (currently line ~136):

```csharp
// OLD — REMOVE:
services.AddScoped<IFileStorage, LocalFileStorage>();

// NEW — replace with this block:
var storageProvider = configuration["Storage:Provider"] ?? "Local";

if (storageProvider.Equals("MinIO", StringComparison.OrdinalIgnoreCase))
{
    var minioSection = configuration.GetSection("Storage:MinIO");
    var bucket = minioSection["Bucket"] ?? "orvixflow";
    
    services.AddSingleton<IAmazonS3>(_ =>
    {
        var endpoint = minioSection["Endpoint"] 
            ?? throw new InvalidOperationException("Storage:MinIO:Endpoint is required when Storage:Provider=MinIO");
        var accessKey = configuration["MINIO_ACCESS_KEY"] 
            ?? throw new InvalidOperationException("MINIO_ACCESS_KEY environment variable is required");
        var secretKey = configuration["MINIO_SECRET_KEY"]
            ?? throw new InvalidOperationException("MINIO_SECRET_KEY environment variable is required");
        
        var config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,   // MANDATORY for MinIO — do not remove
            UseHttp = !minioSection.GetValue<bool>("UseSSL")
        };
        return new Amazon.S3.AmazonS3Client(accessKey, secretKey, config);
    });
    
    services.AddSingleton<MinIOBucketInitializer>(sp =>
        new MinIOBucketInitializer(
            sp.GetRequiredService<IAmazonS3>(),
            bucket,
            sp.GetRequiredService<ILogger<MinIOBucketInitializer>>()));
    
    services.AddHostedService(sp => sp.GetRequiredService<MinIOBucketInitializer>());
    
    services.AddScoped<IFileStorage>(sp =>
        new MinIOFileStorage(
            sp.GetRequiredService<IAmazonS3>(),
            bucket,
            sp.GetRequiredService<ILogger<MinIOFileStorage>>()));
}
else
{
    // Default: LocalFileStorage (dev without Docker, unit tests)
    services.AddScoped<IFileStorage, LocalFileStorage>();
}
```

> **CRITICAL: `ForcePathStyle = true`**  
> MinIO uses path-style access (`http://minio:9000/bucket/key`) not virtual-hosted-style (`http://bucket.minio:9000/key`).  
> Without `ForcePathStyle = true`, the AWS SDK generates virtual-hosted-style URLs that fail against local MinIO.

---

### Step 7 — Update Configuration

**File:** `OrvixFlow.Api/appsettings.json`

Add Storage section:
```json
"Storage": {
  "Provider": "Local",
  "MinIO": {
    "Endpoint": "http://minio:9000",
    "Bucket": "orvixflow",
    "UseSSL": false
  },
  "Local": {
    "BasePath": "/app/uploads"
  }
}
```

> Keep `Provider: "Local"` as default in appsettings so that unit tests and standalone runs work without Docker.  
> Override to `MinIO` via environment variable in docker-compose.

**File:** `docker-compose.yml` — add to `orvix-api.environment`:
```yaml
Storage__Provider: ${STORAGE_PROVIDER:-MinIO}
Storage__MinIO__Endpoint: http://orvix-minio:9000
Storage__MinIO__Bucket: ${MINIO_BUCKET:-orvixflow}
Storage__MinIO__UseSSL: "false"
MINIO_ACCESS_KEY: ${MINIO_ACCESS_KEY}
MINIO_SECRET_KEY: ${MINIO_SECRET_KEY}
```

**File:** `.env.example` — add:
```bash
# Object Storage (MinIO for local/dev)
STORAGE_PROVIDER=MinIO
MINIO_ACCESS_KEY=orvixflow_admin
MINIO_SECRET_KEY=orvixflow_secret_changeme
MINIO_BUCKET=orvixflow
```

---

### Step 8 — Add MinIO to docker-compose

**File:** `docker-compose.yml`

Add the MinIO service:
```yaml
  orvix-minio:
    image: minio/minio:latest
    container_name: orvix_minio
    command: server /data --console-address ":9001"
    ports:
      - "9000:9000"    # S3 API port
      - "9001:9001"    # MinIO Web Console (dev only)
    environment:
      MINIO_ROOT_USER: ${MINIO_ACCESS_KEY}
      MINIO_ROOT_PASSWORD: ${MINIO_SECRET_KEY}
    volumes:
      - minio_data:/data
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - internal
```

Add `minio_data` to volumes:
```yaml
volumes:
  pgdata:
  n8n_data:
  uploads_data:
  clamav_data:
  minio_data:     # NEW
```

Update `orvix-api.depends_on`:
```yaml
  orvix-api:
    depends_on:
      orvix-db:
        condition: service_started
      n8n:
        condition: service_started
      clamav:
        condition: service_healthy
      orvix-minio:
        condition: service_healthy   # NEW
```

---

### Step 9 — Write Unit Tests for MinIOFileStorage

**File:** `OrvixFlow.Tests/Storage/MinIOFileStorageTests.cs` (new file)

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Storage;

namespace OrvixFlow.Tests.Storage;

public class MinIOFileStorageTests
{
    private static MinIOFileStorage CreateService(Mock<IAmazonS3> s3Mock)
        => new MinIOFileStorage(s3Mock.Object, "test-bucket", NullLogger<MinIOFileStorage>.Instance);

    [Fact]
    public async Task SaveFileAsync_WithDepartmentId_KeyContainsDepartmentSegment()
    {
        // Arrange
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new PutObjectResponse());
        
        var service = CreateService(s3);
        var tenantId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var ctx = new StorageContext(tenantId, deptId, docId, "report.pdf");

        // Act
        var key = await service.SaveFileAsync(ctx, new MemoryStream(new byte[] { 0x01 }));

        // Assert
        key.Should().StartWith($"tenants/{tenantId}/depts/{deptId}/docs/{docId}/");
        key.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task SaveFileAsync_NullDepartmentId_UsesCompanySentinel()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new PutObjectResponse());
        
        var service = CreateService(s3);
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var ctx = new StorageContext(tenantId, null, docId, "company-policy.pdf");

        var key = await service.SaveFileAsync(ctx, new MemoryStream(new byte[] { 0x01 }));

        key.Should().Contain("__company__");
        key.Should().NotContain("null");
    }

    [Fact]
    public async Task SaveFileAsync_LegacyOverload_UsesCompanySentinel()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new PutObjectResponse());
        
        var service = CreateService(s3);
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        // Legacy overload (used by IngestionPipelineService for images)
        var key = await service.SaveFileAsync(tenantId, docId, "img_0_doc.png", new MemoryStream(new byte[] { 0x01 }));

        // Sentinel must be used (null departmentId → __company__)
        key.Should().Contain("__company__");
    }

    [Fact]
    public async Task SaveFileAsync_KeyContainsNoUserControlledSegments()
    {
        // Attack: try to inject path traversal via filename
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new PutObjectResponse());
        
        var service = CreateService(s3);
        var ctx = new StorageContext(Guid.NewGuid(), null, Guid.NewGuid(), "../../../etc/passwd");

        var key = await service.SaveFileAsync(ctx, new MemoryStream(new byte[] { 0x01 }));

        // Extension parsing from "../../../etc/passwd" gives "" (no extension) — key ends with GUID
        key.Should().NotContain("..");
        key.Should().NotContain("/etc/");
        key.Should().NotContain("passwd");
    }

    [Fact]
    public async Task DeleteFileAsync_EmptyPath_DoesNotCallS3()
    {
        var s3 = new Mock<IAmazonS3>();
        var service = CreateService(s3);

        await service.DeleteFileAsync(string.Empty);

        s3.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

> **Note:** `Moq` must be available in `OrvixFlow.Tests`. Check `OrvixFlow.Tests.csproj` for existing Moq reference. If absent, run:  
> `dotnet add OrvixFlow.Tests package Moq`

---

## Storage Key Convention

Defined and used by `MinIOFileStorage.BuildKey()`:

```
tenants/{tenantId}/depts/{departmentId|__company__}/docs/{documentId}/{guid}.{ext}
```

### Examples

```
tenants/c7a8.../depts/9f3a.../docs/ab12.../2f4d7e1b-....pdf   ← dept-scoped
tenants/c7a8.../depts/__company__/docs/ab12.../8ae3c9f1-....pdf  ← company-wide
tenants/c7a8.../depts/__company__/docs/bb45.../ee01fa33-....png  ← image (legacy overload)
```

### Key Construction Rules

1. **All path components are server-generated GUIDs** — no user input reaches the key
2. `Path.GetExtension(originalFileName)` extracts file extension only — cannot inject path components
3. The trailing GUID prevents collision even if the same `documentId` receives multiple uploads (e.g., retries)
4. `__company__` is a deliberate sentinel — not a valid UUID, cannot be mistaken for a real department

---

## Constraints

- Do not bypass `IFileStorage` interface — `MinIOFileStorage` must never be injected directly into business code
- Do not expose `IAmazonS3` outside `OrvixFlow.Infrastructure`
- The `uploads_data` Docker volume remains for now — legacy files exist there; do not remove it yet (Phase 09 handles migration)
- `IngestionPipelineService` still uses the old `SaveFileAsync(tenantId, documentId, fileName, stream)` overload for image saves — this is intentional and works via the legacy delegation

---

## Validation Checklist

- [ ] `docker compose up -d orvix-minio` — MinIO UI accessible at `http://localhost:9001`
- [ ] `docker compose up -d` — all services start, `orvix-api` logs show "MinIO bucket 'orvixflow' created/exists"
- [ ] Upload a file via `POST /api/v1/knowledge/upload` — verify object appears in MinIO console under correct prefix
- [ ] Delete the document via `DELETE /api/v1/knowledge/documents/{id}` — verify object removed from MinIO
- [ ] `dotnet test --filter Storage` passes all `MinIOFileStorageTests`
- [ ] `dotnet test` passes (no regressions)
- [ ] With `Storage:Provider = Local`, `LocalFileStorage` is used and `IAmazonS3` is never instantiated

---

## Completion Criteria

- [ ] `StorageContext` record exists in `OrvixFlow.Core/Models/`
- [ ] `IFileStorage` has both overloads
- [ ] `LocalFileStorage` implements both overloads
- [ ] `MinIOFileStorage` implemented and tested
- [ ] `MinIOBucketInitializer` implemented
- [ ] DI switches between Local/MinIO based on `Storage:Provider` config
- [ ] MinIO defined in docker-compose with healthcheck
- [ ] Unit tests pass

---

## Handoff to Phase 04

Phase 04 adds `DepartmentId` to `KnowledgeBaseDocument` entity, the EF migration, and the `AppDbContext` relationship.  
After Phase 04, the storage key for new uploads will carry the correct `DepartmentId` segment.
