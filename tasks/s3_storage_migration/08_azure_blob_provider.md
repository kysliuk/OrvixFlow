# Phase 08 — Azure Blob Storage Production Provider

## Phase Goal

Implement `AzureBlobObjectStorageService` (as `AzureBlobFileStorage`) that satisfies `IFileStorage`.  
Wire it in DI under `Storage:Provider = AzureBlob`.  
The same storage key convention as MinIO must be used.  
No business logic or controller changes required.

---

## Phase Purpose

Production target is **Azure Blob Storage**, not MinIO. MinIO is only for local/dev and CI.  
This phase adds the production provider so deployments to Azure can use the same interface without any business-code changes.

Do **not** rush this phase. Azure Blob has different SDK surface, auth model, and container semantics from the S3-compatible MinIO client.

---

## Scope

### Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Infrastructure/Storage/AzureBlobFileStorage.cs` | Azure Blob implementation of `IFileStorage` |

### Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Add `AzureBlob` branch to provider switch |
| `OrvixFlow.Api/appsettings.json` | Add `Storage:AzureBlob` config section |
| `.env.example` | Add Azure Blob env vars |

### Files NOT to Change

- `IFileStorage` interface — no new methods needed
- `StorageContext` — unchanged
- Controllers and services — unchanged

---

## Prerequisites

- Phase 03 complete (MinIO implementation working)
- Phase 07 complete (stream-handling fixes in place)
- NuGet package added: `Azure.Storage.Blobs`

---

## NuGet Package

```bash
dotnet add OrvixFlow.Infrastructure package Azure.Storage.Blobs
```

---

## Key Differences Between MinIO (S3) and Azure Blob

| Concept | MinIO / S3 | Azure Blob |
|---------|-----------|------------|
| Top-level unit | Bucket | Container |
| Key separator | `/` (forward slash) | `/` (same — blob names can include `/`) |
| Path style | `http://host:9000/bucket/key` | `https://account.blob.core.windows.net/container/blob` |
| Auth | AccessKey + SecretKey | Connection string OR Managed Identity |
| Server-side encryption | SSE-AES256 flag | Enabled by default at account level |
| Delete | DeleteObjectAsync | DeleteBlobAsync |
| Existence check | DoesS3BucketExistV2Async | GetPropertiesAsync() or GetBlobServiceClient |
| Non-seekable stream | `GetObjectResponse.ResponseStream` | `BlobDownloadStreamingResult.Content.ToStream()` |
| Download | GetObjectAsync → ResponseStream | OpenReadAsync() or DownloadStreamingAsync() |

---

## Implementation Instructions

### Step 1 — Implement `AzureBlobFileStorage`

**File:** `OrvixFlow.Infrastructure/Storage/AzureBlobFileStorage.cs` (new file)

```csharp
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
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobFileStorage> _logger;

    public AzureBlobFileStorage(BlobContainerClient container, ILogger<AzureBlobFileStorage> logger)
    {
        _container = container;
        _logger = logger;
    }

    /// <summary>
    /// Same key convention as MinIOFileStorage.BuildKey() — provider-neutral layout.
    /// tenants/{tenantId}/depts/{departmentId|__company__}/docs/{documentId}/{guid}.{ext}
    /// </summary>
    private static string BuildKey(StorageContext ctx)
    {
        var deptSegment = ctx.DepartmentId.HasValue
            ? ctx.DepartmentId.Value.ToString()
            : "__company__";
        var extension = Path.GetExtension(ctx.OriginalFileName);
        return $"tenants/{ctx.TenantId}/depts/{deptSegment}/docs/{ctx.DocumentId}/{Guid.NewGuid()}{extension}";
    }

    public async Task<string> SaveFileAsync(StorageContext ctx, Stream fileStream)
    {
        var blobName = BuildKey(ctx);
        _logger.LogInformation(
            "Uploading blob to Azure: container={Container}, blob={Blob}, tenant={TenantId}",
            _container.Name, blobName, ctx.TenantId);

        var blobClient = _container.GetBlobClient(blobName);
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" }
        };

        await blobClient.UploadAsync(fileStream, options);
        _logger.LogInformation("Blob uploaded successfully: {Blob}", blobName);
        return blobName;
    }

    /// <summary>
    /// Legacy overload — delegates to StorageContext overload with null DepartmentId.
    /// </summary>
    public Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream)
        => SaveFileAsync(new StorageContext(tenantId, null, documentId, fileName), fileStream);

    public async Task<Stream> GetFileAsync(string storagePath)
    {
        _logger.LogInformation("Downloading blob from Azure: {Blob}", storagePath);
        var blobClient = _container.GetBlobClient(storagePath);

        // DownloadStreamingAsync returns a non-seekable stream.
        // IngestionPipelineService handles buffering (see Phase 07 fix).
        var result = await blobClient.DownloadStreamingAsync();
        return result.Value.Content;
    }

    public async Task DeleteFileAsync(string storagePath)
    {
        if (string.IsNullOrEmpty(storagePath)) return;

        _logger.LogInformation("Deleting blob from Azure: {Blob}", storagePath);
        var blobClient = _container.GetBlobClient(storagePath);

        try
        {
            await blobClient.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "BlobNotFound")
        {
            _logger.LogWarning("Blob not found for deletion (already gone?): {Blob}", storagePath);
            // Treat as success — idempotent delete
        }
    }
}
```

---

### Step 2 — Add Container Initializer

**File:** `OrvixFlow.Infrastructure/Storage/AzureBlobContainerInitializer.cs` (new file)

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
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
                Azure.Storage.Blobs.Models.PublicAccessType.None,   // CRITICAL: no public access
                cancellationToken: cancellationToken);

            if (created?.Value != null)
                _logger.LogInformation("Azure Blob container '{Container}' created.", _container.Name);
            else
                _logger.LogInformation("Azure Blob container '{Container}' already exists.", _container.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Blob container '{Container}'.", _container.Name);
            throw; // Fail fast
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

> **CRITICAL:** `PublicAccessType.None` — the container must **never** be publicly accessible. All access is API-mediated.

---

### Step 3 — Register Azure Blob in DI

**File:** `OrvixFlow.Infrastructure/DependencyInjection.cs`

Extend the `storageProvider` switch block to include `AzureBlob`:

```csharp
else if (storageProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    var azureSection = configuration.GetSection("Storage:AzureBlob");
    var connectionString = configuration["AZURE_STORAGE_CONNECTION_STRING"]
        ?? throw new InvalidOperationException(
            "AZURE_STORAGE_CONNECTION_STRING env var is required when Storage:Provider=AzureBlob");
    var containerName = azureSection["ContainerName"] ?? "orvixflow";

    // Register BlobContainerClient as singleton (handles connection pooling internally)
    services.AddSingleton(sp =>
    {
        var serviceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
        return serviceClient.GetBlobContainerClient(containerName);
    });

    services.AddSingleton<AzureBlobContainerInitializer>(sp =>
        new AzureBlobContainerInitializer(
            sp.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>(),
            sp.GetRequiredService<ILogger<AzureBlobContainerInitializer>>()));
    services.AddHostedService(sp => sp.GetRequiredService<AzureBlobContainerInitializer>());

    services.AddScoped<IFileStorage>(sp =>
        new AzureBlobFileStorage(
            sp.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>(),
            sp.GetRequiredService<ILogger<AzureBlobFileStorage>>()));
}
```

---

### Step 4 — Configuration

**File:** `OrvixFlow.Api/appsettings.json` — Add AzureBlob section:

```json
"Storage": {
  "Provider": "Local",
  "MinIO": {
    "Endpoint": "http://minio:9000",
    "Bucket": "orvixflow",
    "UseSSL": false
  },
  "AzureBlob": {
    "ContainerName": "orvixflow"
  },
  "Local": {
    "BasePath": "/app/uploads"
  }
}
```

**File:** `.env.example` — Add:
```bash
# Azure Blob (production only)
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=...
```

> The connection string is never in `appsettings.json`. Always inject via environment variable.

> **Production consideration:** Prefer Managed Identity over connection string when deploying to Azure App Service or AKS. To use Managed Identity, replace `BlobServiceClient(connectionString)` with `new BlobServiceClient(new Uri("https://{account}.blob.core.windows.net"), new Azure.Identity.DefaultAzureCredential())`. The `DefaultAzureCredential` approach requires separate NuGet: `Azure.Identity`.

---

## Tests to Write

**File:** `OrvixFlow.Tests/Storage/AzureBlobFileStorageTests.cs` (new file)

Because `Azure.Storage.Blobs.BlobContainerClient` does not implement an interface easily mockable in xUnit, use the approach of testing `BuildKey` logic indirectly by capturing the blob name passed to upload:

```csharp
// Tests should verify:
// 1. Key contains correct tenant/dept/doc segments
// 2. Null DepartmentId → "__company__" sentinel
// 3. Non-null DepartmentId → department GUID in path
// 4. DeleteFileAsync with empty path → does not call SDK
// 5. Key format matches MinIOFileStorage key format (same convention)
```

Consider using `Azure.Storage.Blobs.Test.Fakes` if available, or use a real MinIO-compatible endpoint in CI integration tests (both use the same key format).

---

## Provider Selection Summary

After Phase 08, the DI block in `DependencyInjection.cs` handles three providers:

```
Storage:Provider = "Local"     → LocalFileStorage  (dev without Docker)
Storage:Provider = "MinIO"     → MinIOFileStorage  (local Docker, CI)
Storage:Provider = "AzureBlob" → AzureBlobFileStorage  (production)
```

No other code in the application knows which provider is active.

---

## Constraints

- Container must be created with `PublicAccessType.None` — no public blob access
- Connection string must come from environment variable — never appsettings or code
- `AzureBlobFileStorage` must follow the same key convention as `MinIOFileStorage` exactly (same `BuildKey` logic)
- Do not add any Azure-specific types to `OrvixFlow.Core` or `OrvixFlow.Api`
- The non-seekable stream returned by `DownloadStreamingAsync` is handled by the Phase 07 buffering fix — do not duplicate buffering here

---

## Validation Checklist

- [ ] `Storage:Provider = AzureBlob` starts without error (with valid connection string)
- [ ] Upload a file → blob visible in Azure Portal with correct key prefix
- [ ] Download a file → returns correct content
- [ ] Delete a file → blob removed from Azure
- [ ] Container is created with `PublicAccessType.None`
- [ ] `dotnet test` passes

---

## Completion Criteria

- [ ] `AzureBlobFileStorage` implemented
- [ ] `AzureBlobContainerInitializer` implemented
- [ ] DI provider switch includes `AzureBlob` branch
- [ ] Connection string comes from env var only
- [ ] Tests written and passing (at least unit-level key-building tests)

---

## Handoff to Phase 09

Phase 09 builds the one-time migration tool to copy existing local files into MinIO. This is needed for any deployment that has files stored in `LocalFileStorage`'s `/app/uploads/` volume.
