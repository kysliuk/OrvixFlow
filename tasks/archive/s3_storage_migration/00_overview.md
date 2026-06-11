# Object Storage Migration — Master Overview

> **Obsolete / Historical Migration Overview**
> Large parts of this overview were superseded by later storage, RBAC, and production-ops implementation by 2026-06-11.
> Use current code, `memory/memory-feature-map.md`, and `tasks/production/current-state-audit.md` before relying on this plan.

## What This Migration Is and Is Not

This migration is **not merely "replace LocalFileStorage with MinIO."**

It is a phased upgrade that solves five simultaneous production problems:

1. **Eliminate `LocalFileStorage`** — current implementation loses data on container restart and cannot scale.
2. **Remove `NoopVirusScanService`** — currently always returns `true` (safe). No virus scanning happens in production.
3. **Introduce a provider-agnostic storage abstraction** — local/dev uses MinIO; production uses Azure Blob Storage.
4. **Add department-level authorization** to file access without creating bucket-per-department complexity.
5. **Add `StoredObject` DB metadata** as the authorization gate — no raw object key escapes to clients.

**Production target is Azure Blob Storage, not S3.**  
MinIO is only used for local/dev because it is S3-compatible and free to self-host.  
The `.NET` abstraction is provider-neutral. Provider selection is config-driven only.

---

## Current State (Confirmed by Codebase Inspection)

### What Exists

| Component | File | State |
|-----------|------|-------|
| `IFileStorage` | `OrvixFlow.Core/Interfaces/IFileStorage.cs` | Exists — 3 methods: `SaveFileAsync`, `GetFileAsync`, `DeleteFileAsync` |
| `LocalFileStorage` | `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs` | Exists — writes to `/app/uploads` on local disk |
| `NoopVirusScanService` | `OrvixFlow.Infrastructure/Services/Security/NoopVirusScanService.cs` | Exists — always returns `true`. Config ignored. **DI bug exists (see below)**. |
| `ClamAvVirusScanService` | `OrvixFlow.Infrastructure/Services/Security/ClamAvVirusScanService.cs` | Exists — uses `nClam` library + `IClamAvClient`/`NclamClient` |
| `FileIngestionController` | `OrvixFlow.Api/Controllers/FileIngestionController.cs` | Exists — upload/list/delete. **No download endpoint.** |
| `IngestionPipelineService` | `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs` | Calls `_storage.SaveFileAsync` for images only |
| `FileIngestionJob` | `OrvixFlow.Infrastructure/Ai/Jobs/FileIngestionJob.cs` | Calls `storage.GetFileAsync(storagePath)` to re-read file |
| `KnowledgeBaseDocument` | `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs` | Has `StoragePath`; **no `DepartmentId`** |
| `KnowledgeBaseImage` | `OrvixFlow.Core/Entities/KnowledgeBaseImage.cs` | Has `StoragePath` — image files also stored via `LocalFileStorage` |
| ClamAV in docker-compose | `docker-compose.yml` | **Missing** — ClamAV service not defined |
| MinIO in docker-compose | `docker-compose.yml` | **Missing** — MinIO service not defined |
| `StorageContext` value object | `OrvixFlow.Core/Models/` | **Does not exist yet** |

### Critical Bug Found in DependencyInjection.cs

`DependencyInjection.cs` lines 146–156 conditionally register `ClamAvVirusScanService` when `Provider = ClamAv`, BUT line 174 unconditionally re-registers `NoopVirusScanService`:

```csharp
// Line 174 — overrides whatever was registered above
services.AddScoped<IVirusScanService, NoopVirusScanService>();
```

This means **ClamAV is always bypassed even when `Provider=ClamAv` is configured.**  
This bug must be fixed in Phase 03 (ClamAV activation).

### What the `F20-minio-storage-plan.md` Already Designed (Not Yet Implemented)

A detailed design exists in `tasks/F20-minio-storage-plan.md` covering:
- `StorageContext` value object
- `MinIOFileStorage` implementation
- `MinIOBucketInitializer` hosted service  
- RBAC model for department-level access
- Download proxy endpoint design
- DI registration pattern

**None of this has been implemented yet.** The current codebase has not changed since that design was written.

---

## Phase Execution Order

```
Phase 01 — Fix ClamAV DI bug + activate virus scanning
Phase 02 — Storage abstraction upgrade (StorageContext + IFileStorage extension)
Phase 03 — MinIO implementation + Docker + local config
Phase 04 — Domain model: DepartmentId on KnowledgeBaseDocument + EF migration
Phase 05 — KB file access: RBAC + download endpoint + list filtering
Phase 06 — StoredObject metadata entity (EF + DB metadata per file)
Phase 07 — FileIngestionJob adaptation for object storage
Phase 08 — Azure Blob Storage production provider
Phase 09 — Legacy file migration tool (local → MinIO)
Phase 10 — Tests, health checks, observability, and hardening
Phase 11 — Cleanup: remove LocalFileStorage, runbooks, memory updates
```

**Phases must be executed in order.**  
Phases 01–05 are the "immediate production unblock" set: they remove the virus scan noop and build the MinIO layer.  
Phase 06 onwards adds the DB metadata model and production cloud provider.

---

## Architecture Rules the Execution Agent Must Always Follow

1. **Clean Architecture layers must not be crossed**
   - Entities and interfaces live in `OrvixFlow.Core`
   - Implementations live in `OrvixFlow.Infrastructure`
   - Controllers live in `OrvixFlow.Api`
   - DI wiring lives in `OrvixFlow.Infrastructure/DependencyInjection.cs`
   - No storage SDK types (`IAmazonS3`, `BlobContainerClient`) must escape Infrastructure

2. **Tenant isolation is sacred**
   - Every entity that touches storage has `TenantId`
   - `AppDbContext` applies global query filters — never bypass without `IgnoreQueryFilters()`
   - `IgnoreQueryFilters()` only in admin service methods, never in regular request paths
   - When adding `StoredObject` entity: it must have a query filter on `TenantId`

3. **No business logic in Infrastructure**
   - Authorization logic belongs in controllers or `AccessResolver`, not in storage implementations
   - Storage services must be "dumb" — they execute what the caller requests

4. **Provider selection is config-only**
   - No `if (provider == "MinIO")` blocks outside DI registration
   - Business code, jobs, and services only call `IFileStorage` or `IObjectStorageService` interfaces

5. **Secrets are environment variables only**
   - MinIO credentials, Azure connection strings, ClamAV host — never in `appsettings.json`
   - Use `.env` (gitignored) for local dev, environment injection for production

6. **Two-tier role system — never conflate**
   - `User.Role` = global platform roles (`SuperAdmin`, `InternalOperator`) only
   - `UserCompanyMembership.CompanyRole` = company roles (`CompanyOwner`, `Operator`, etc.)
   - Storage RBAC uses `IScopeContext` for company roles — see `ScopeContext`

7. **Background jobs need `BackgroundTenantProvider`**
   - Jobs run without a JWT/HTTP context
   - `FileIngestionJob` already uses `IServiceProvider` scope — follow this exact pattern

8. **Tests are mandatory**
   - Run `dotnet test` after every phase
   - EF InMemory is used in tests — pgvector/vector columns must be ignored in InMemory mode
   - A test that compiles but fails silently (due to InMemory vector fallback) is documented pattern — see `memory-risks.md`

9. **Storage paths must never come from user input**
   - `StoragePath` in DB is always server-generated
   - The download endpoint loads document from DB first (EF filter enforces TenantId), then uses the server-assigned key

10. **DI registration order matters**
    - The last `AddScoped<IVirusScanService, ...>()` wins (this is the bug in current code)
    - Register ClamAV or Noop once — do not double-register

---

## Major Risks

| Risk | Severity | How to Avoid |
|------|----------|--------------|
| ClamAV DI double-registration bug | **Critical** | Phase 01 — fix line 174 in DependencyInjection.cs |
| `NoopVirusScanService` in production | **Critical** | Phase 01 — activate ClamAV or fail-closed mode |
| `LocalFileStorage` data loss on container restart | **High** | Phase 03 — MinIO with persistent volume |
| No download endpoint exposed | **High** | Phase 05 — add proxy download endpoint |
| Background job receives raw `storagePath` string | **Medium** | Phase 07 — adapt FileIngestionJob to use DB metadata |
| EF query filter bypass | **Critical** | Always use `IgnoreQueryFilters()` only in admin paths |
| Duplicate virus scan DI on new features | **Medium** | Phase 01 fix + comment prevents reintroduction |
| Image files also stored via IFileStorage | **Medium** | Phase 07 — images go through same object storage path |
| MinIO `ForcePathStyle = true` missing | **High** | Phase 03 — must set; virtual-hosted-style fails against local MinIO |

---

## Key Config Keys the Agent Must Know

| Config Key | Purpose | Set In |
|-----------|---------|--------|
| `Storage:Provider` | `"Local"`, `"MinIO"`, or `"AzureBlob"` | `appsettings.json` / env var |
| `Storage:MinIO:Endpoint` | MinIO API URL, e.g. `http://minio:9000` | `appsettings.json` |
| `Storage:MinIO:Bucket` | Bucket name, e.g. `"orvixflow"` | `appsettings.json` |
| `MINIO_ACCESS_KEY` | MinIO root user | `.env` only |
| `MINIO_SECRET_KEY` | MinIO root password | `.env` only |
| `Security:VirusScan:Provider` | `"Noop"` or `"ClamAv"` | `appsettings.json` |
| `Security:VirusScan:ClamAv:Host` | ClamAV daemon host | `appsettings.json` |
| `Security:VirusScan:ClamAv:Port` | ClamAV daemon port (default `3310`) | `appsettings.json` |

---

## Reference Files

- Primary design doc: `tasks/F20-minio-storage-plan.md`
- Migration plan: `tasks/orvixflow_s3_storage_migration_plan.md`
- Architecture memory: `memory/memory-architecture.md`
- Feature map: `memory/memory-feature-map.md`
- File map: `memory/memory-file-map.md`
- Risks: `memory/memory-risks.md`
- Project rules: `AGENTS.md`
