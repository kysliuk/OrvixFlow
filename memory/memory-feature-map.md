# OrvixFlow - Feature Map

## Authentication & Users

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Register/Login | AuthController | AuthService | User, Tenant |
| OAuth Provision | AuthController | AuthService | User, Tenant |
| Switch Company | AuthController | AuthService | UserCompanyMembership |
| Invite User | InviteController | AuthService | Invitation |
| Accept Invite | AuthController | AuthService | Invitation, User |

**Files:**
- `OrvixFlow.Api/Controllers/AuthController.cs`
- `OrvixFlow.Api/Controllers/InviteController.cs`
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `OrvixFlow.Core/Authorization/Roles.cs`

## Inbox & Email Processing

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Ingest Email | InboxController | IngestionService | InboxEvent |
| Webhook Receive | WebhookController | - | InboxEvent |
| Process Queue | InboxEventsController | InboxGuardianService | InboxEvent |
| Process Job | - | InboxProcessingJob (Hangfire) | InboxEvent |
| Settings (Policies/Persona) | InboxSettingsController | - | WorkflowPolicy, AgentPersona |
| Mailbox Connections | MailboxConnectionsController | N8nProvisioningService | MailboxConnection |
| Draft Feedback | DraftFeedbackController | DraftFeedbackService | DraftFeedback |
| Feedback Enrichment | - | FeedbackEnrichmentJob (Hangfire) | KnowledgeBase |
| Action Requests | ActionsController | - | ActionRequest |
| Admin Metrics | AdminInboxController | - | All inbox entities |

**Files:**
- `OrvixFlow.Api/Controllers/InboxController.cs`
- `OrvixFlow.Api/Controllers/WebhookController.cs`
- `OrvixFlow.Api/Controllers/InboxEventsController.cs`
- `OrvixFlow.Api/Controllers/ActionsController.cs`
- `OrvixFlow.Api/Controllers/InboxSettingsController.cs`
- `OrvixFlow.Api/Controllers/MailboxConnectionsController.cs`
- `OrvixFlow.Api/Controllers/DraftFeedbackController.cs`
- `OrvixFlow.Api/Controllers/AdminInboxController.cs`
- `OrvixFlow.Api/Jobs/InboxProcessingJob.cs`
- `OrvixFlow.Api/Jobs/FeedbackEnrichmentJob.cs`
- `OrvixFlow.Api/Jobs/PendingPlanChangeJob.cs`
- `OrvixFlow.Infrastructure/Ai/IngestionService.cs`
- `OrvixFlow.Infrastructure/Ai/InboxGuardianService.cs`
- `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs`
- `OrvixFlow.Infrastructure/Ai/DraftGeneratorService.cs` (persona-aware)
- `OrvixFlow.Infrastructure/Services/WebhookCallbackService.cs` (with retry/backoff)
- `OrvixFlow.Infrastructure/Services/DraftFeedbackService.cs` (Levenshtein edit distance)
- `OrvixFlow.Infrastructure/Data/InboxEventRepository.cs`

### Inbox Processing Pipeline
```
InboxEvent (Ingested) → Hangfire Job → Fetch Persona → Classify Intent → 
RAG Search → Generate Draft (persona-aware) → Evaluate Policy → 
  Auto-Execute (callback + retry) OR Hold for Approval (ActionRequest)
```

### Feedback Learning Loop
```
Human modifies draft → DraftFeedback record created → 
FeedbackEnrichmentJob (if edit distance > 0.3) → 
Extract guidelines → KnowledgeBase entry created
```

### n8n Auto-Provisioning
```
Activate connection → ProvisionN8nWorkflowJob → Clone template workflow → 
Create credentials → Store N8nWorkflowId/N8nCredentialId → Connection becomes active
```

### Correlation & Tracing
- `InboxEvent.TraceId` assigned at processing start
- All log messages include `[TraceId]` prefix for full pipeline traceability
- Audit trails include TraceId reference

## AI Agent

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| RAG Orchestration | RagEmailController | RagEmailService | - |
| Email Ingestion (Vision) | - | IngestionPipelineService | KnowledgeBaseImage |
| RAG Health Monitoring | - | RagHealthCheck | - |
| RAG Metrics | - | RagMetricsCollector | AuditTrail |

**Files:**
- `OrvixFlow.Api/Controllers/AgentController.cs`
- `OrvixFlow.Api/Controllers/RagEmailController.cs`
- `OrvixFlow.Infrastructure/Ai/AgentService.cs`
- `OrvixFlow.Infrastructure/Ai/RagEmailService.cs`
- `OrvixFlow.Infrastructure/Ai/IntentClassifierService.cs`
- `OrvixFlow.Infrastructure/Ai/DraftGeneratorService.cs`
- `OrvixFlow.Infrastructure/Ai/Plugins/KnowledgeBaseSearchPlugin.cs`
- `OrvixFlow.Infrastructure/Ai/Plugins/N8nAutomationPlugin.cs`
- `OrvixFlow.Api/Health/RagHealthCheck.cs`
- `OrvixFlow.Infrastructure/Ai/RagMetricsCollector.cs`
- `OrvixFlow.Core/Interfaces/IRagMetricsCollector.cs`

## Knowledge Base

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Search (Hybrid) | KnowledgeBaseController | HybridVectorSearchService | KnowledgeBase |
| Vector Index | - | IngestionPipelineService | KnowledgeBase |
| Image Resolution| - | ImageResolver | KnowledgeBaseImage |
| Reranking | - | IReranker (LlmScorerReranker) | KnowledgeSnippet |
| File Upload | FileIngestionController | IngestionPipelineService | KnowledgeBaseDocument, StoredObject |
| File Listing | FileIngestionController | - | KnowledgeBaseDocument |
| File Delete | FileIngestionController | LocalFileStorage | KnowledgeBaseDocument |
| Stored Object Metadata | FileIngestionController | IngestionPipelineService | StoredObject |
| File Parsing | - | IDocumentParser implementations | - |
| Virus Scanning | - | IVirusScanService (ClamAv, Noop) | - |
| Background Jobs| - | FileIngestionJob | - |

**Files:**
- `OrvixFlow.Api/Controllers/KnowledgeBaseController.cs`
- `OrvixFlow.Api/Controllers/FileIngestionController.cs`
- `OrvixFlow.Infrastructure/Ai/HybridVectorSearchService.cs`
- `OrvixFlow.Infrastructure/Ai/ImageResolver.cs`
- `OrvixFlow.Infrastructure/Ai/LlmScorerReranker.cs`
- `OrvixFlow.Core/Interfaces/IReranker.cs`
- `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`
- `OrvixFlow.Infrastructure/Ai/Parsers/` (.txt, .pdf, .docx, .image)
- `OrvixFlow.Infrastructure/Ai/Chunking/OverlapChunker.cs`
- `OrvixFlow.Infrastructure/Ai/Jobs/FileIngestionJob.cs`
- `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs`
- `OrvixFlow.Core/Entities/KnowledgeBase.cs`
- `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs`
- `OrvixFlow.Core/Entities/KnowledgeBaseImage.cs`
- `OrvixFlow.Core/Entities/StoredObject.cs`

## Organization & Access

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Modules | ModulesController | AccessResolver | ModuleDefinition, ModuleAssignment |
| Departments | TeamController | - | Department |
| Team Members | TeamController | - | UserCompanyMembership, UserDepartmentMembership |

**Files:**
- `OrvixFlow.Api/Controllers/ModulesController.cs`
- `OrvixFlow.Api/Controllers/TeamController.cs`
- `OrvixFlow.Infrastructure/Auth/AccessResolver.cs`
- `OrvixFlow.Core/Entities/ModuleDefinition.cs`
- `OrvixFlow.Core/Entities/ModuleAssignment.cs`
- `OrvixFlow.Core/Entities/Department.cs`

## Billing & Plans (Admin Panel)

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Plan Templates | PlansController | PlanService | PlanTemplate |
| Plan Modules | - | - | PlanModuleInclusion |
| Plan Entitlements | - | - | PlanEntitlements |
| Company Subscription | - | CompanySubscriptionService | CompanySubscription |
| Entitlement Resolution | - | EntitlementResolver | - |
| Subscription | BillingController | - | BillingSubscription |
| Usage Tracking | - | UsageService | UsageEvent |
| Seat Limit Enforcement | InviteController | EntitlementResolver | - |
| Audit Logging (Plan Changes) | - | CompanySubscriptionService | AuditTrail |
| Module Access Control | RequireModuleAttribute | EntitlementResolver | - |
| Limit Check Methods | - | EntitlementResolver | - |
| Change Plan | BillingController | CompanySubscriptionService | CompanySubscription |
| Module Gate UI | module-gate.tsx | - | - |
| **Trial Expiration** | - | TrialExpirationJob (Hangfire) | CompanySubscription |
| **Pending Plan Change Processing** | - | PendingPlanChangeJob (Hangfire) | CompanySubscription |
| **Module Definitions CRUD** | AdminController | - | ModuleDefinition |
| **Company Audit Log** | AdminController | - | AuditTrail |
| **Entitlement Overrides** | AdminController | EntitlementResolver | CompanyEntitlementOverride |
| **Module Overrides** | AdminController | EntitlementResolver | CompanyModuleOverride |
| **InternalOperator Access** | AdminController | - | - |
| **Company Actions** | AdminController | CompanySubscriptionService | - |
| **Load Test Script** | `scripts/load-test-inbox.sh` | - | - |
| **Company Actions** | AdminController | CompanySubscriptionService | - |

### Billing Subsystems & Endpoints
| Component | Purpose |
|----------|---------|
| `/api/billing/subscription` | GET current subscription with effective entitlements |
| `/api/billing/plans` | GET available plans |
| `/api/billing/change-plan` | POST change subscription (Downgrade safety checks KB/Storage/Seats) |
| `/api/admin/companies/{id}/subscription` | GET full company subscription details + current usage |
| `TrialExpirationJob` | Background service to auto-downgrade expired plans |
| `UsagePeriodRolloverJob` | Background service to advance expired billing periods |

### Usage Tracking
- **ai-tokens**: AI token consumption
- **storage-mb**: Knowledge base storage
- **knowledge-bases**: KB count
- **inbox-messages**: Processed messages
- **n8n-nodes**: Workflow executions

### Enforcement
- **Downgrade Safety:** `ChangePlanAsync` throws `DowngradeNotAllowedException` or `SeatLimitExceededException` (409 Conflict) if limits are breached.
- **Entitlement Checks:** Limit checks (`IsWithinTokenLimitAsync`, etc.) respect admin overrides via `GetEffectiveEntitlementsAsync()`.
