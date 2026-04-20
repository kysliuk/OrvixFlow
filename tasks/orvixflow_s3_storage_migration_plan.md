# OrvixFlow — S3 Storage Migration Plan (MinIO → Azure Blob) with Small Phases

## Purpose

This plan defines a **production-grade storage migration path** for OrvixFlow, replacing `LocalFileStorage` with a provider-agnostic object storage layer that uses **MinIO for local/dev and early self-hosted environments** and **Azure Blob Storage for production**. It also includes the related fixes required so the migration is safe in a real SaaS environment: virus scanning, tenant/department isolation, dead-lettering, testing, and the boundaries between storage, Inbox Guardian, and n8n.

This plan is grounded in the current platform review, where `LocalFileStorage` and `NoopVirusScanService` are identified as live production blockers, the Knowledge Base currently relies on local storage, and Inbox Guardian currently dispatches outbound activity via n8n rather than the internal .NET email stack. fileciteturn1file4 fileciteturn1file1

The architecture also needs to respect the current multi-tenant and department-aware model, where tenant isolation is enforced centrally and departments already exist as first-class entities. fileciteturn1file2

> Production stance: this is **not** treated as a prototype migration. The platform is intended to be a production service, so every phase below assumes operational safety, rollback, auditability, and testability are mandatory. fileciteturn1file3

---

## Final decisions resolved from your Q&A

### Storage
- **Local S3-compatible provider:** **MinIO**
- **Production cloud provider:** **Azure Blob Storage**
- **Isolation strategy:** **shared container/bucket + strict key-prefix isolation**, not bucket-per-tenant
- **Department isolation:** enforce through **metadata + authorization rules**, not separate buckets/containers per department

### Email / Inbox Guardian
- **Inbox Guardian outbound path:** stays **n8n-driven**, not direct .NET SMTP/provider send
- **Platform transactional emails:** should remain **separate from Inbox Guardian**
- **Free-tier transactional provider:** **Resend free tier** is the best fit for platform notifications, based on the earlier strategy guidance that already recommended Resend as the free-tier outbound email option. fileciteturn1file0

### Workflow hot-swap
- **Departments exist now:** confirmed
- **Workflow model:** use a **workflow template/definition registry** in OrvixFlow and create/manage corresponding workflows in n8n
- **Workflow lifecycle:** workflow remains persisted in n8n for reuse, not ephemeral per request
- **Hot-swap authority:** **SuperAdmin only**
- **Customization level:** **full custom per tenant**

---

## What this migration is really solving

This is not only “replace local files with object storage.” It solves five production problems together:

1. **Eliminate `LocalFileStorage` as a data-loss and scaling risk**. The current review explicitly calls out local file storage as a blocker that must be replaced before external customer usage. fileciteturn1file1 fileciteturn1file0
2. **Introduce a hot-swappable storage abstraction** so local/dev uses MinIO while production uses Azure Blob without changing business logic. The implementation plan explicitly says storage must become provider-agnostic at configuration level, not only at interface level. fileciteturn1file4
3. **Preserve strong tenant isolation** while respecting the existing company/department model and future department-level access restrictions. fileciteturn1file2
4. **Add fail-closed virus scanning** before any user-uploaded document is persisted. Both the review and implementation plan identify `NoopVirusScanService` as unacceptable in production. fileciteturn1file1 fileciteturn1file4
5. **Keep Inbox Guardian architecture clean** by ensuring storage, approval, and workflow routing are platform concerns, while n8n stays an executor for mailbox automation and email-side actions. The current architecture review shows Inbox Guardian already routes through webhook callback to n8n. fileciteturn1file1

---

## Recommended target architecture

## 1. Storage architecture

Use a single storage abstraction in `.NET`:

- `IFileStorageService` / `IObjectStorageService`
- `MinioObjectStorageService` for local/dev/test
- `AzureBlobObjectStorageService` for production

Business code must never know whether the underlying provider is MinIO or Azure Blob. Provider selection must be purely configuration-driven. This follows the production implementation guidance that storage swapping must not require touching business logic. fileciteturn1file4

### Recommended provider split
- **Development / local Docker:** MinIO
- **CI integration tests:** MinIO Testcontainers
- **Production:** Azure Blob Storage
- **Migration tooling:** copy from MinIO to Azure Blob using a dedicated migration job/tool

### Why this is the right split
MinIO gives you a cheap, fully local S3-compatible environment for development and repeatable testing. Azure Blob aligns with your production choice and broader Azure-oriented platform direction, which the strategy file already assumes for cloud infra choices. fileciteturn1file0

---

## 2. Isolation model

### Decision
Use **key-prefix isolation** inside a shared bucket/container.

Do **not** create a bucket/container per tenant or department.

### Why
Bucket/container-per-tenant sounds cleaner at first, but it becomes operationally noisy:
- provisioning complexity
- harder lifecycle management
- more infrastructure code
- weaker portability across providers
- unnecessary pressure on cloud object namespace governance

The implementation plan already favors prefix-based isolation as the scalable option. fileciteturn1file4

### Recommended storage key convention
```text
{tenantId}/{departmentId or _shared}/{module}/{entityType}/{entityId}/{yyyy}/{MM}/{sanitizedFileName}
```

### Example
```text
c7a8.../sales/knowledge-base/document/9ad1.../2026/04/vendor-policy.pdf
c7a8.../_shared/knowledge-base/document/ab12.../2026/04/employee-handbook.pdf
c7a8.../finance/inbox-guardian/attachment/ee45.../2026/04/invoice-1042.pdf
```

### Why this convention is better than only `{tenantId}/{moduleId}/{entityId}`
Because your platform already has departments and department-aware access rules, a pure tenant prefix is not enough for future-safe storage governance. Departments should not be separate physical containers, but they **should** be first-class in the key model so:
- authorization is easier to reason about
- operational cleanup is easier
- per-department analytics and quotas are easier later
- audits are easier later

### Rule
Physical isolation remains **tenant-first**.
Department isolation is **logical and authorization-driven**.

---

## 3. Metadata model for files

Every stored file should have both:
- **storage key** in object storage
- **database metadata row** in PostgreSQL

### Proposed metadata entity
```csharp
public class StoredObject
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Module { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string StorageProvider { get; set; } = default!; // MinIO | AzureBlob
    public string ContainerOrBucket { get; set; } = default!;
    public string StorageKey { get; set; } = default!;
    public string OriginalFileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = default!;
    public string VirusScanStatus { get; set; } = default!; // Pending | Clean | Infected | Failed
    public string LifecycleStatus { get; set; } = default!; // Active | SoftDeleted | Archived
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
}
```

### Why DB metadata is mandatory
Without DB metadata, object storage becomes an opaque blob dump. You lose:
- proper authorization checks
- ownership mapping
- file lifecycle management
- dedup/hash checks
- cleanup safety
- auditability

---

## 4. Virus scanning architecture

### Decision
Deploy **ClamAV** alongside API + workers and scan **before final persistence**.

This is directly aligned with the review and implementation plan, both of which mark virus scanning as a production prerequisite. fileciteturn1file1 fileciteturn1file4

### Required behavior
- Upload arrives
- Temporary stream/spool created
- ClamAV scan runs
- If infected → reject upload, audit event, no persistence
- If scanner unavailable → fail closed for external uploads
- If clean → persist object and create metadata row

### Important fix
Do **not** implement “scan after upload to permanent storage” for the first version.
For a production SaaS, the initial safer model is:
1. receive stream
2. scan
3. persist

Later, if direct-to-blob uploads are introduced for large files, you can move to quarantine-container → scan job → promote-to-final-container.

---

## 5. Inbox Guardian and n8n boundary

Your answer clarifies that Inbox Guardian is not using the internal `.NET` email subsystem for outbound replies. It builds a payload and posts to n8n via `WebhookCallbackService`, while the internal email services are reserved for platform notifications. That matches the functional review’s summary that Inbox Guardian’s ingress/egress is webhook-driven and n8n-mediated. fileciteturn1file1

### Keep this split
#### Inbox Guardian via n8n
Use n8n for:
- mailbox ingest triggers
- moving emails between folders/labels/categories
- draft creation in mailbox ecosystem
- send/reply through tenant mailbox account
- provider-specific mailbox actions

#### Internal platform email service
Use separate internal provider integration for:
- auth verification
- password reset
- billing alerts
- usage alerts
- approval notifications
- system incident notifications

### Best free-tier choice
Use **Resend** for platform transactional notifications.
It was already identified in your strategy as the best free-tier outbound option. fileciteturn1file0

### Important architectural fix
Do not let n8n become the source of truth for storage, policy, or authorization.
It should stay a workflow executor, not the owner of business rules.

---

## 6. Workflow hot-swap model

The implementation plan suggests a hierarchical routing model with workflow definitions and assignments. Your Q&A changes the governance model in one important way: **workflow hot swap is superadmin-only**, not tenant-admin self-service. fileciteturn1file4

### Revised governance model
#### SuperAdmin can
- create workflow templates
- clone templates into tenant-specific workflow definitions
- publish/activate/deactivate tenant workflows
- assign workflows to tenant / department / category
- roll back to previous version

#### Tenant admin cannot
- hot-swap workflows directly
- publish new workflow versions directly
- modify active workflow bindings

### Recommended workflow data model
Keep these concepts:
- `WorkflowTemplate` — reusable blueprint
- `WorkflowDefinition` — tenant-specific materialized workflow
- `WorkflowAssignment` — binding of workflow to tenant/department/category
- `WorkflowActivationLog` — audit trail

### n8n lifecycle approach
Your answer suggests the correct operational pattern:
1. define workflow template in platform
2. create actual workflow in n8n
3. store returned workflow ID + webhook path in platform DB
4. keep workflow alive for future invocations
5. switch assignments by changing active binding in OrvixFlow

That is the correct model. Do **not** generate transient workflows per message.

---

## 7. Storage migration strategy

Because production target is **Azure Blob**, not AWS S3, the best architecture is not “S3 everywhere forever.”
It is:

- **S3-compatible abstraction for local/dev using MinIO**
- **provider-agnostic object storage abstraction in .NET**
- **Azure Blob provider in production**

So the migration should be described as:

> `LocalFileStorage` → provider-agnostic object storage abstraction → MinIO locally / Azure Blob in production

That is more correct than calling Azure Blob your “S3 production.” It is not S3. It is the cloud production provider behind the same platform abstraction.

---

# Implementation roadmap with small phases

Each phase is intentionally small enough to fit into a focused AI-agent execution slice and well under your context-window requirement.

---

## Phase 0 — Decision lock and architecture freeze

### Goal
Freeze the migration rules before code changes start.

### Scope
- lock provider choices
- lock key convention
- lock file metadata model
- lock virus scan behavior
- lock ownership boundaries between storage / platform email / Inbox Guardian / n8n

### Deliverables
- ADR: `ADR-001-object-storage-provider-model.md`
- ADR: `ADR-002-storage-key-and-isolation-model.md`
- ADR: `ADR-003-inbox-guardian-vs-platform-email-boundary.md`
- architecture diagrams updated

### Proposed fixes in this phase
- explicitly rename implementation effort from “S3 migration” to “object storage migration” in internal docs
- state clearly that Azure Blob is production target
- state clearly that department isolation is logical, not bucket-level

### Exit criteria
- no unresolved architectural ambiguity remains
- every downstream agent task references the same storage key scheme

---

## Phase 1 — Introduce provider-agnostic storage abstraction

### Goal
Replace the current storage dependency direction without yet migrating data.

### Scope
- define `IObjectStorageService`
- define `ObjectStorageOptions`
- add provider selection by config
- leave `LocalFileStorage` in place temporarily behind compatibility adapter if needed

### Deliverables
- `IObjectStorageService`
- `ObjectStorageOptions`
- `MinioObjectStorageService`
- `AzureBlobObjectStorageService` skeleton
- DI registration extension

### Proposed fixes
1. **Do not expose provider SDK models in interface contracts**
   - return platform-native DTOs only
2. **Do not hardcode MinIO/S3 naming into business code**
   - use neutral names like `ObjectStorage`, `StoredObject`, `ContainerName`
3. **Add strict filename sanitization**
   - prevent traversal and weird Unicode/path edge cases
4. **Add hash generation at write time**
   - SHA-256 for dedup/audit/integrity

### Exit criteria
- app can compile with provider selected by config
- no business code depends directly on local filesystem APIs for file persistence

---

## Phase 2 — Add MinIO local implementation

### Goal
Make local and CI environments use object storage instead of local disk.

### Scope
- add MinIO to Docker Compose
- implement write/read/delete/list/presigned URL flows
- wire appsettings for local/dev/test

### Deliverables
- `docker-compose.yml` updated with MinIO
- bucket bootstrap/init routine
- local dev config for MinIO
- MinIO integration tests using Testcontainers

### Proposed fixes
1. **Create bucket on startup only through idempotent bootstrap service**
2. **Use path-style access for MinIO**
3. **Add health checks for MinIO connectivity**
4. **Add structured logging with tenantId, departmentId, module, entityId on every storage operation**

### Exit criteria
- local uploads go to MinIO only
- local file retrieval works end-to-end
- local file deletion works end-to-end

---

## Phase 3 — Add DB metadata and authorization alignment

### Goal
Make stored files first-class domain entities instead of raw object references.

### Scope
- add `StoredObject` table/entity
- write metadata on upload
- align reads/downloads with auth checks
- prepare for department-aware access

### Deliverables
- EF migration
- metadata repository/service
- auth checks for download/read/delete
- audit events for file lifecycle

### Proposed fixes
1. **Every storage operation must resolve through DB metadata first**
   - never trust raw object key from client
2. **Department-aware checks**
   - enforce department access using membership + module grants
3. **Soft delete in DB before physical delete**
   - then async hard delete job
4. **Audit log every state transition**
   - upload, scan pass/fail, read, delete, restore

### Exit criteria
- API never exposes direct blind object-key access
- tenant/department authorization is enforced before download

---

## Phase 4 — ClamAV fail-closed integration

### Goal
Remove `NoopVirusScanService` and make uploads safe.

### Scope
- deploy ClamAV service
- add scanner client/service
- wire scan-before-persist flow
- reject infected content

### Deliverables
- `ClamAvVirusScanService`
- scanner health check
- upload pipeline updated
- audit events on infection
- integration tests with EICAR sample

### Proposed fixes
1. **Fail closed when scanner unavailable** for user uploads
2. **Separate error codes**
   - `SCAN_FAILED`
   - `FILE_INFECTED`
3. **Do not create metadata row on infected file**
4. **Alert platform admins on infection spikes**

### Exit criteria
- `NoopVirusScanService` removed
- infected test file rejected
- scanner outage blocks upload as designed

---

## Phase 5 — Migrate Knowledge Base to object storage

### Goal
Move Knowledge Base off local disk to the new object storage layer.

### Scope
- replace all KB file persistence with object storage
- keep document parsing/chunking pipeline intact
- preserve existing ingestion job contracts

### Deliverables
- KB upload path uses object storage
- ingestion jobs resolve content from object storage streams
- existing file entities linked to `StoredObject`

### Proposed fixes
1. **stream from object storage to parser, not temp full-file copies where unnecessary**
2. **preserve content hash and MIME validation results in metadata**
3. **store source provider in metadata to support migration observability**
4. **introduce retry around object fetch in ingestion jobs**

### Exit criteria
- KB uploads and retrieval work end-to-end with MinIO
- no KB write path uses local disk persistence

---

## Phase 6 — Data migration tool for old local files

### Goal
Move already stored local files into object storage safely.

### Scope
- build one-time migration job/tool
- walk legacy storage paths
- create object storage keys
- upload to MinIO
- backfill metadata
- verify hashes

### Deliverables
- migration console app or Hangfire admin job
- migration report
- resumable/idempotent migration state

### Proposed fixes
1. **run migration in dry-run mode first**
2. **verify SHA-256 before marking migrated**
3. **log every failure row explicitly**
4. **do not delete old local file until verification passes**

### Exit criteria
- all legacy files represented in metadata table
- all verified files present in MinIO
- local legacy storage can be marked read-only

---

## Phase 7 — Azure Blob production provider

### Goal
Implement production cloud provider without changing business logic.

### Scope
- implement `AzureBlobObjectStorageService`
- add config and secret wiring
- support shared container + prefix isolation
- add production health checks

### Deliverables
- Azure Blob implementation
- production configuration shape
- managed identity or secret-based auth
- container bootstrap policy

### Proposed fixes
1. **prefer Managed Identity in production** if deployment model supports it
2. **single container per environment**, not per tenant
3. **mirror same key convention as MinIO**
4. **presigned URL equivalent only if truly needed**; otherwise keep downloads API-mediated for stricter auth

### Exit criteria
- staging environment runs against Azure Blob
- same tests pass against Azure Blob adapter where possible

---

## Phase 8 — MinIO → Azure Blob migration execution

### Goal
Move production/staging data from MinIO-backed object storage to Azure Blob.

### Scope
- copy objects
- verify integrity
- update provider flags
- cut over reads/writes
- keep rollback option

### Deliverables
- migration runner
- provider cutover checklist
- rollback checklist
- migration audit report

### Proposed fixes
1. **use dual-read / single-write transition briefly if needed**
   - write new files to Azure
   - read from Azure first, fallback to MinIO only during cutover window
2. **do not perform big-bang delete of MinIO immediately**
3. **cut over in staging first, then production**
4. **record migrated provider on each metadata row if needed during transition**

### Exit criteria
- all reads/writes served from Azure Blob
- MinIO no longer required in production runtime
- rollback window expires cleanly

---

## Phase 9 — Hardening and cleanup

### Goal
Turn migration work into stable platform infrastructure.

### Scope
- remove `LocalFileStorage`
- remove dead legacy configs
- add dashboards/alerts
- finalize runbooks

### Deliverables
- `LocalFileStorage` deleted
- legacy migration code archived or feature-flagged off
- runbooks for incident response
- dashboards for storage errors / scan errors / orphaned metadata / failed deletions

### Proposed fixes
1. **scheduled orphan cleanup job**
   - metadata row exists, object missing
   - object exists, metadata missing
2. **storage lifecycle policies**
   - later for archive/cold storage
3. **quota enforcement based on metadata + provider stats**
4. **NetArchTest rule to prevent filesystem persistence reappearing in modules**

### Exit criteria
- no production path depends on local storage
- full observability exists for storage lifecycle

---

# Proposed implementation order for AI-agent slices

## Slice 1
- Phase 0 + Phase 1 ADRs and contracts

## Slice 2
- Phase 2 MinIO implementation + local config + tests

## Slice 3
- Phase 3 metadata model + authorization integration

## Slice 4
- Phase 4 ClamAV integration + tests

## Slice 5
- Phase 5 Knowledge Base migration

## Slice 6
- Phase 6 legacy file migration tool

## Slice 7
- Phase 7 Azure Blob provider

## Slice 8
- Phase 8 cutover tooling and rollout docs

## Slice 9
- Phase 9 cleanup, hardening, observability

This sequence keeps each slice bounded and lets you validate production safety incrementally instead of betting everything on one huge migration.

---

# Required code-level fixes

## 1. Rename the abstraction cleanly
Prefer:
- `IObjectStorageService`
- `ObjectStorageOptions`
- `StoredObject`

Avoid locking the domain to S3 terms when production target is Azure Blob.

## 2. Keep storage provider out of core business logic
No `if (provider == "MinIO")` or `if (provider == "Azure")` outside infrastructure registration.

## 3. Add strict stream handling
- reset stream position after scan
- avoid double-reading huge files unnecessarily
- use buffered streaming, not naive `byte[]` loads for big documents

## 4. Add provider-neutral integration tests
Every provider must satisfy the same contract:
- upload
- download
- delete
- exists
- metadata
- naming convention
- failure behavior

## 5. Add audit coverage
Audit these events at minimum:
- file uploaded
- file scan passed
- file scan failed
- file deleted
- file restored
- file migrated
- file download denied by auth

## 6. Add migration state tracking
Do not rely on log parsing to know which file migrated.
Create explicit migration state rows or flags.

## 7. Add object-key immutability rule
Once created, storage key should not be mutated casually.
If a file is “renamed,” update display metadata, not necessarily the physical object key.

---

# n8n and workflow fixes related to this migration

These are adjacent and should be fixed near the storage work, even though they are not part of storage provider code itself.

## Fix 1 — n8n dead-letter path
If Inbox Guardian callback to n8n fails, persist payload to dead-letter table and log to audit. The implementation plan already flags this as a missing operational layer. fileciteturn1file4

## Fix 2 — n8n health checks
Add recurring health check and mark workflow transport unhealthy early. This is also explicitly called out in the blocker list. fileciteturn1file4 fileciteturn1file0

## Fix 3 — separate platform notifications from Inbox Guardian mailbox actions
Do not mix Resend-based platform notifications with tenant mailbox actions handled by n8n.

## Fix 4 — workflow registry before deep tenant customization
Since you want full custom per tenant and superadmin-only hot-swap, add workflow registry entities before trying to operationalize custom routing widely. The implementation plan already marks missing workflow registry/versioning as a blocker. fileciteturn1file4

---

# Risks to watch

## 1. Calling it “S3 migration” forever
This can mislead the codebase and future contributors because production is Azure Blob. Internally, treat it as object storage migration.

## 2. Over-physicalizing department isolation
Separate containers per department would add pain and little value. Keep department isolation in metadata + auth.

## 3. Allowing direct client download URLs too early
For sensitive tenant data, API-mediated downloads are safer initially. Add signed direct URLs only if scale/perf needs justify it.

## 4. Letting n8n become business-logic engine
n8n should execute mailbox workflows, not decide storage auth or policy.

## 5. Migrating legacy local files without verification
Hash verification is mandatory before old file deletion.

## 6. Reintroducing local disk through temp shortcuts
Agents and developers often add temp local save paths “just for parsing.” Avoid accidental regression by architecture tests and code review rules.

---

# Success criteria

The migration is successful when all of the following are true:

- `LocalFileStorage` is fully removed
- `NoopVirusScanService` is fully removed
- local/dev uses MinIO only
- staging/production uses Azure Blob only
- storage provider is selected by configuration only
- every stored object has DB metadata
- tenant and department authorization is enforced before file access
- Knowledge Base works end-to-end on object storage
- infected uploads are rejected fail-closed
- legacy files are migrated with checksum verification
- Inbox Guardian continues to use n8n for mailbox-side actions
- platform notifications use separate internal provider flow
- workflow hot-swap is governed by superadmin-only registry/assignment model

---

# Recommended first execution slice

If you want the highest-ROI first slice, do this exact subset first:

1. Phase 0 architecture freeze
2. Phase 1 abstraction
3. Phase 2 MinIO local provider
4. Phase 4 ClamAV integration
5. Phase 5 Knowledge Base migration

That subset removes the two biggest production blockers first: local file persistence and no-op virus scanning. Both were explicitly identified as critical blockers in your existing reviews and strategy. fileciteturn1file1 fileciteturn1file0 fileciteturn1file4

---

# Final recommendation

Your best path is **not** “build S3 now and think about Azure later.”
Your best path is:

- design a **provider-neutral object storage boundary** now
- use **MinIO locally** because it is cheap, testable, and S3-compatible
- use **Azure Blob in production** because that is your chosen cloud target
- use **key-prefix isolation** as the physical isolation strategy
- use **DB metadata + auth rules** for department-aware protection
- keep **Inbox Guardian on n8n** for mailbox automation
- keep **platform notifications separate** using **Resend free tier**
- add **workflow registry + superadmin-controlled hot-swap** as a platform control layer, not as loose n8n config

That gives you a production-safe migration path without locking the platform to the wrong storage vocabulary or turning n8n into the heart of the system.

---

## Source grounding
- Production blockers around `LocalFileStorage`, `NoopVirusScanService`, and n8n reliability: fileciteturn1file0 fileciteturn1file1 fileciteturn1file4
- Existing multi-tenant and department-aware model: fileciteturn1file2
- Production-service framing: fileciteturn1file3
