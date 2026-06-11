# Phase 11 — Cleanup, Runbooks, and Memory Updates

> **Obsolete / Historical Migration Plan**
> Superseded by later storage/runbook implementation and current memory sync by 2026-06-11.
> This file is retained as historical context only.

## Phase Goal

Remove `LocalFileStorage` from production code paths.  
Update all `memory/` files to reflect the new architecture.  
Clean up the `uploads_data` Docker volume.  
Write the operational runbooks for storage provider switch and incident response.

---

## Phase Purpose

This phase closes the migration. After this:
- `LocalFileStorage` no longer exists (or is clearly marked `[TestOnly]`)
- All file operations go through MinIO (dev) or Azure Blob (prod)
- `memory/memory-architecture.md` reflects the new storage layer
- Future engineers have runbooks for common storage operations

---

## Prerequisites

- ALL previous phases complete (01–10)
- Phase 09 migration tool run and status = "Complete"
- Phase 10 tests passing
- Production deployment verified on MinIO or Azure Blob for at least 1 week

---

## Scope

### Files to Delete or Archive

| File | Action |
|------|--------|
| `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs` | Move to `/.legacy/LocalFileStorage.deleted.cs` OR mark `[Obsolete]` and register only for test use |

### Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Remove `LocalFileStorage` from the production provider switch; keep only for test fallback |
| `docker-compose.yml` | Remove `uploads_data` volume (after confirmation no local files remain) |
| All `memory/` files listed below | Reflect new architecture |

### Files to Create

| File | Purpose |
|------|---------|
| `tasks/s3_storage_migration/runbook_provider_switch.md` | Step-by-step ops runbook for switching storage provider |
| `tasks/s3_storage_migration/runbook_incident_response.md` | Runbook for storage outage, data loss, corruption scenarios |

---

## Implementation Instructions

### Step 1 — Handle `LocalFileStorage`

**Option A (Preferred for test safety):** Mark as `[Obsolete]` and only register when `Storage:Provider = Local`. Keep the file but add a prominent comment:

```csharp
/// <summary>
/// LEGACY IMPLEMENTATION. Used only when Storage:Provider = "Local".
/// For local development without Docker.
/// NOT suitable for production.
/// </summary>
[Obsolete("Use MinIOFileStorage or AzureBlobFileStorage for all production and staging environments.")]
public class LocalFileStorage : IFileStorage
{
    // ... existing implementation unchanged
}
```

**Option B (If tests no longer need it):** Delete the file. Update DI to use `MinIOFileStorage` even for `Storage:Provider = Local` by running MinIO in Docker for all environments.

**Recommendation:** Option A for now. Option B must wait until test suite is updated to mock `IFileStorage` rather than use `LocalFileStorage` concretely.

---

### Step 2 — DI Cleanup

**File:** `OrvixFlow.Infrastructure/DependencyInjection.cs`

Ensure the registration block is clean and clearly documented:

```csharp
var storageProvider = configuration["Storage:Provider"] ?? "Local";

switch (storageProvider.ToUpperInvariant())
{
    case "MINIO":
        // ... MinIO registration (Phase 03)
        break;

    case "AZUREBLOB":
        // ... Azure Blob registration (Phase 08)
        break;

    case "LOCAL":
    default:
        // LocalFileStorage: dev-only fallback. Not for production.
        services.AddScoped<IFileStorage, LocalFileStorage>();
        break;
}
```

---

### Step 3 — Remove `uploads_data` Volume

**Prerequisite:** All files migrated (Phase 09 status = "Complete") AND production stable for 1+ week.

**Steps:**
1. Stop containers: `docker compose down`
2. Remove the volume: `docker volume rm orvixflow_uploads_data`
3. Remove from `docker-compose.yml` volumes block: `uploads_data:`
4. Remove from `orvix-api.volumes`: `- uploads_data:/app/uploads`
5. Restart: `docker compose up -d`

If the volume still has files, investigate before deleting.

---

### Step 4 — Update `memory/memory-architecture.md`

Add/update the Storage section to reflect:
- Storage layer is now provider-agnostic via `IFileStorage`
- Providers: `MinIOFileStorage` (dev), `AzureBlobFileStorage` (prod)
- Storage key convention: `tenants/{tenantId}/depts/{deptId|__company__}/docs/{docId}/{uuid}.{ext}`
- `StoredObject` entity is the file registry in PostgreSQL
- ClamAV daemon required in all Docker environments
- `VirusScanResult.Error` → fail-closed (upload rejected)
- `DepartmentId` on `KnowledgeBaseDocument` drives RBAC

---

### Step 5 — Update `memory/memory-file-map.md`

Add new files:
```
OrvixFlow.Core/Models/StorageContext.cs               — StorageContext value object
OrvixFlow.Core/Entities/StoredObject.cs               — file metadata registry entity
OrvixFlow.Infrastructure/Storage/MinIOFileStorage.cs  — S3-compatible MinIO provider
OrvixFlow.Infrastructure/Storage/MinIOBucketInitializer.cs — startup bucket creation
OrvixFlow.Infrastructure/Storage/AzureBlobFileStorage.cs   — Azure Blob production provider
OrvixFlow.Infrastructure/Storage/AzureBlobContainerInitializer.cs — container initialization
OrvixFlow.Infrastructure/Storage/LocalToMinioMigrationJob.cs — one-time migration job
OrvixFlow.Infrastructure/Storage/OrphanDetectionJob.cs    — periodic orphan scan
OrvixFlow.Api/Health/StorageHealthCheck.cs             — readiness health check for storage
OrvixFlow.Api/Controllers/StorageMigrationController.cs — admin migration trigger endpoint
```

Mark deprecated:
```
OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs  — [LEGACY] dev-only fallback
```

---

### Step 6 — Update `memory/memory-risks.md`

Add new risks:
```
RISK: MinIO ForcePathStyle missing — Without ForcePathStyle=true, SDK generates virtual-hosted URLs that fail against local MinIO.
RISK: IVirusScanService double-registration — DI last-registration wins. Adding a new service that registers IVirusScanService again will silently override ClamAV. Search codebase before adding any new IVirusScanService registration.
RISK: Non-seekable stream from object storage — MinIO/Azure return non-seekable network streams. IngestionPipelineService buffers to MemoryStream. Do not remove this buffer.
RISK: StoredObject query filter — StoredObjects has global TenantId filter. Admin queries must use IgnoreQueryFilters().
RISK: AzureBlob PublicAccessType.None — Container must never be created without PublicAccessType.None. Public containers bypass all RBAC.
```

---

### Step 7 — Write Operational Runbooks

**File:** `tasks/s3_storage_migration/runbook_provider_switch.md`

Include:
- How to switch from `Local` → `MinIO` safely (with migration tool)
- How to switch from `MinIO` → `AzureBlob` in production
- Rollback procedure for each switch
- Verification steps after switch

**File:** `tasks/s3_storage_migration/runbook_incident_response.md`

Include:
- MinIO unreachable: what happens, how to recover
- File not found in storage: how to detect, diagnose, remediate
- Orphan objects detected: how to safely clean up
- SHA-256 mismatch: what it means, how to respond
- ClamAV daemon down: fail-closed means uploads fail — how to diagnose and restore

---

## Validation Checklist

- [ ] `LocalFileStorage.cs` is marked `[Obsolete]` or deleted (after tests updated)
- [ ] DI switch is clean and documented
- [ ] `uploads_data` volume removed from docker-compose (after verified migration)
- [ ] `memory/memory-architecture.md` updated
- [ ] `memory/memory-file-map.md` updated
- [ ] `memory/memory-risks.md` updated
- [ ] Runbooks written
- [ ] `dotnet test` passes
- [ ] Final end-to-end smoke test: upload → download → delete in MinIO mode

---

## Completion Criteria

- [ ] All memory files updated to reflect new storage architecture
- [ ] `LocalFileStorage` deprecated or removed
- [ ] `uploads_data` volume removed from docker-compose
- [ ] Operational runbooks written
- [ ] No regressions — all tests pass

---

## Migration Complete ✓

After this phase, the OrvixFlow storage layer is:
- **Provider-agnostic** — MinIO for dev/CI, Azure Blob for production
- **Department-scoped** — RBAC enforces isolation at upload/download/delete
- **Auditable** — every file has a `StoredObject` metadata row
- **Virus-scanned** — ClamAV integration is active, fail-closed on scanner outage
- **Observable** — health check, orphan detection, Hangfire job visibility
- **Migrated** — all legacy local files are in MinIO
