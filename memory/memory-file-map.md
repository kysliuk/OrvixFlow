# OrvixFlow - File Map

## Core Entities

| Entity | Location |
|--------|----------|
| User | `OrvixFlow.Core/Entities/User.cs` |
| Tenant | `OrvixFlow.Core/Entities/Tenant.cs` |
| Department | `OrvixFlow.Core/Entities/Department.cs` |
| KnowledgeBase | `OrvixFlow.Core/Entities/KnowledgeBase.cs` |
| KnowledgeBaseDocument | `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs` |
| KnowledgeBaseImage | `OrvixFlow.Core/Entities/KnowledgeBaseImage.cs` |
| StoredObject | `OrvixFlow.Core/Entities/StoredObject.cs` |
| InboxEvent | `OrvixFlow.Core/Entities/InboxEvent.cs` |
| MailboxConnection | `OrvixFlow.Core/Entities/MailboxConnection.cs` |
| AgentPersona | `OrvixFlow.Core/Entities/AgentPersona.cs` |
| DraftFeedback | `OrvixFlow.Core/Entities/DraftFeedback.cs` |
| AuditTrail | `OrvixFlow.Core/Entities/AuditTrail.cs` |
| WorkflowPolicy | `OrvixFlow.Core/Entities/WorkflowPolicy.cs` |
| ActionRequest | `OrvixFlow.Core/Entities/ActionRequest.cs` |
| ModuleDefinition | `OrvixFlow.Core/Entities/ModuleDefinition.cs` |
| ModuleAssignment | `OrvixFlow.Core/Entities/ModuleAssignment.cs` |
| ModulePermissionGrant | `OrvixFlow.Core/Entities/ModulePermissionGrant.cs` |
| BillingSubscription | `OrvixFlow.Core/Entities/BillingSubscription.cs` |
| UsageEvent | `OrvixFlow.Core/Entities/UsageEvent.cs` |
| Invitation | `OrvixFlow.Core/Entities/Invitation.cs` |
| PlanTemplate | `OrvixFlow.Core/Entities/PlanTemplate.cs` |
| PlanModuleInclusion | `OrvixFlow.Core/Entities/PlanModuleInclusion.cs` |
| PlanEntitlements | `OrvixFlow.Core/Entities/PlanEntitlements.cs` |
| CompanySubscription | `OrvixFlow.Core/Entities/CompanySubscription.cs` |
| CompanyEntitlementOverride | `OrvixFlow.Core/Entities/CompanyEntitlementOverride.cs` |
| CompanyModuleOverride | `OrvixFlow.Core/Entities/CompanyModuleOverride.cs` |
| PlanCatalog | `OrvixFlow.Core/Entities/PlanCatalog.cs` |
| SubscriptionState | `OrvixFlow.Core/Entities/SubscriptionState.cs` |
| BillingInterval | `OrvixFlow.Core/Entities/BillingInterval.cs` |
| UsageMetric | `OrvixFlow.Core/Entities/UsageMetric.cs` |
| Invoice | `OrvixFlow.Core/Entities/Invoice.cs` |
| NotificationQueue | `OrvixFlow.Core/Entities/NotificationQueue.cs` |

## Tests

| Test Suite | Location |
|------------|----------|
| PlanTemplateTests | `OrvixFlow.Tests/PlanTemplateTests.cs` |
| EntitlementResolverTests | `OrvixFlow.Tests/EntitlementResolverTests.cs` |
| PlanServiceTests | `OrvixFlow.Tests/PlanServiceTests.cs` |
| CompanySubscriptionServiceTests | `OrvixFlow.Tests/CompanySubscriptionServiceTests.cs` |
| SeatLimitTests | `OrvixFlow.Tests/SeatLimitTests.cs` |
| EntitlementResolverIntegrationTests | `OrvixFlow.Tests/EntitlementResolverIntegrationTests.cs` |
| AuditLogTests | `OrvixFlow.Tests/AuditLogTests.cs` |
| OverlapChunkerTests | `OrvixFlow.Tests/OverlapChunkerTests.cs` |
| IngestionPipelineServiceTests | `OrvixFlow.Tests/IngestionPipelineServiceTests.cs` |
| PlainTextParserTests | `OrvixFlow.Tests/PlainTextParserTests.cs` |
| DraftFeedbackServiceTests | `OrvixFlow.Tests/DraftFeedbackServiceTests.cs` |
| MailboxConnectionTests | `OrvixFlow.Tests/MailboxConnectionTests.cs` |
| AgentPersonaTests | `OrvixFlow.Tests/AgentPersonaTests.cs` |
| InboxProcessingIntegrationTests | `OrvixFlow.Tests/InboxProcessingIntegrationTests.cs` |
| CompanyEntitlementOverrideTests | `OrvixFlow.Tests/CompanyEntitlementOverrideTests.cs` |
| CompanyModuleOverrideTests | `OrvixFlow.Tests/CompanyModuleOverrideTests.cs` |
| TrialExpirationTests | `OrvixFlow.Tests/TrialExpirationTests.cs` |
| CompanySubscriptionTests | `OrvixFlow.Tests/CompanySubscriptionTests.cs` |
| GlobalRoleTests | `OrvixFlow.Tests/GlobalRoleTests.cs` |
| SeatLimitTests | `OrvixFlow.Tests/SeatLimitTests.cs` |
| EmbeddingMigrationSmokeTests | `OrvixFlow.Tests/EmbeddingMigrationSmokeTests.cs` |
| InboxProcessingIntegrationTests | `OrvixFlow.Tests/InboxProcessingIntegrationTests.cs` |
| RagPipelineIntegrationTests | `OrvixFlow.Tests/RagPipelineIntegrationTests.cs` |
| StripeWebhookTests | `OrvixFlow.Tests/StripeWebhookTests.cs` |
| UsageAlertTests | `OrvixFlow.Tests/UsageAlertTests.cs` |
| RagHealthCheck | `OrvixFlow.Api/Health/RagHealthCheck.cs` |
| Load Test Script | `OrvixFlow.Api/load-test.sh` |

## Core Interfaces

| Interface | Location |
|-----------|----------|
| ITenantProvider | `OrvixFlow.Core/Interfaces/ITenantProvider.cs` |
| IScopeContext | `OrvixFlow.Core/Interfaces/IScopeContext.cs` |
| IAuthService | `OrvixFlow.Core/Interfaces/IAuthService.cs` |
| IAgentService | `OrvixFlow.Core/Interfaces/IAgentService.cs` |
| IIngestionService | `OrvixFlow.Core/Interfaces/IIngestionService.cs` |
| IIngestionPipelineService | `OrvixFlow.Core/Interfaces/IIngestionPipelineService.cs` |
| IDocumentParser | `OrvixFlow.Core/Interfaces/IDocumentParser.cs` |
| IChunker | `OrvixFlow.Core/Interfaces/IChunker.cs` |
| IFileStorage | `OrvixFlow.Core/Interfaces/IFileStorage.cs` |
| IInboxGuardianService | `OrvixFlow.Core/Interfaces/IInboxGuardianService.cs` |
| IDraftFeedbackService | `OrvixFlow.Core/Interfaces/IDraftFeedbackService.cs` |
| IImageResolver | `OrvixFlow.Core/Interfaces/IImageResolver.cs` |
| IAccessResolver | `OrvixFlow.Core/Interfaces/IAccessResolver.cs` |
| IAuditService | `OrvixFlow.Core/Interfaces/IAuditService.cs` |
| IUsageService | `OrvixFlow.Core/Interfaces/IUsageService.cs` |
| IPlanService | `OrvixFlow.Core/Interfaces/IPlanService.cs` |
| IEntitlementResolver | `OrvixFlow.Core/Interfaces/IEntitlementResolver.cs` |
| IDraftFeedbackService | `OrvixFlow.Core/Interfaces/IDraftFeedbackService.cs` |
| ICompanySubscriptionService | `OrvixFlow.Core/Interfaces/ICompanySubscriptionService.cs` |
| CompanyEntitlements | `OrvixFlow.Core/Interfaces/IEntitlementResolver.cs` |
| LimitCheckResult | `OrvixFlow.Core/Interfaces/IEntitlementResolver.cs` |
| IVirusScanService | `OrvixFlow.Core/Interfaces/IVirusScanService.cs` |
| IRagMetricsCollector| `OrvixFlow.Core/Interfaces/IRagMetricsCollector.cs` |
| IStripeService | `OrvixFlow.Core/Interfaces/IStripeService.cs` |
| IUsageAlertService | `OrvixFlow.Core/Interfaces/IUsageAlertService.cs` |

## Controllers

| Controller | Location |
|------------|----------|
| AuthController | `OrvixFlow.Api/Controllers/AuthController.cs` |
| AgentController | `OrvixFlow.Api/Controllers/AgentController.cs` |
| InboxController | `OrvixFlow.Api/Controllers/InboxController.cs` |
| InboxEventsController | `OrvixFlow.Api/Controllers/InboxEventsController.cs` |
| WebhookController | `OrvixFlow.Api/Controllers/WebhookController.cs` |
| KnowledgeBaseController | `OrvixFlow.Api/Controllers/KnowledgeBaseController.cs` |
| TeamController | `OrvixFlow.Api/Controllers/TeamController.cs` |
| ModulesController | `OrvixFlow.Api/Controllers/ModulesController.cs` |
| InviteController | `OrvixFlow.Api/Controllers/InviteController.cs` |
| BillingController | `OrvixFlow.Api/Controllers/BillingController.cs` |
| AuditController | `OrvixFlow.Api/Controllers/AuditController.cs` |
| ActionsController | `OrvixFlow.Api/Controllers/ActionsController.cs` |
| InboxSettingsController | `OrvixFlow.Api/Controllers/InboxSettingsController.cs` |
| MailboxConnectionsController | `OrvixFlow.Api/Controllers/MailboxConnectionsController.cs` |
| DraftFeedbackController | `OrvixFlow.Api/Controllers/DraftFeedbackController.cs` |
| AdminInboxController | `OrvixFlow.Api/Controllers/AdminInboxController.cs` |
| AdminController | `OrvixFlow.Api/Controllers/AdminController.cs` |
| OrganizationController | `OrvixFlow.Api/Controllers/OrganizationController.cs` |
| PlansController | `OrvixFlow.Api/Controllers/PlansController.cs` |
| FileIngestionController | `OrvixFlow.Api/Controllers/FileIngestionController.cs` |
| RagEmailController | `OrvixFlow.Api/Controllers/RagEmailController.cs` |
| RagDebugController | `OrvixFlow.Api/Controllers/RagDebugController.cs` |
| InboxGuardianController | `OrvixFlow.Api/Controllers/InboxGuardianController.cs` |
| RagHealthCheck | `OrvixFlow.Api/Health/RagHealthCheck.cs` |

## Infrastructure Services

| Service | Location |
|---------|----------|
| AuthService | `OrvixFlow.Infrastructure/Auth/AuthService.cs` |
| AccessResolver | `OrvixFlow.Infrastructure/Auth/AccessResolver.cs` |
| ScopeContext | `OrvixFlow.Infrastructure/Auth/ScopeContext.cs` |
| AgentService | `OrvixFlow.Infrastructure/Ai/AgentService.cs` |
| IngestionService | `OrvixFlow.Infrastructure/Ai/IngestionService.cs` |
| InboxGuardianService | `OrvixFlow.Infrastructure/Ai/InboxGuardianService.cs` |
| IntentClassifierService | `OrvixFlow.Infrastructure/Ai/IntentClassifierService.cs` |
| DraftGeneratorService | `OrvixFlow.Infrastructure/Ai/DraftGeneratorService.cs` |
| RagEmailService | `OrvixFlow.Infrastructure/Ai/RagEmailService.cs` |
| N8nProvisioningService | `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs` |
| HybridVectorSearchService | `OrvixFlow.Infrastructure/Ai/HybridVectorSearchService.cs` |
| IngestionPipelineService | `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs` |
| ImageResolver | `OrvixFlow.Infrastructure/Ai/ImageResolver.cs` |
| PlainTextParser | `OrvixFlow.Infrastructure/Ai/Parsers/PlainTextParser.cs` |
| PdfParser | `OrvixFlow.Infrastructure/Ai/Parsers/PdfParser.cs` |
| DocxParser | `OrvixFlow.Infrastructure/Ai/Parsers/DocxParser.cs` |
| ImageFileParser | `OrvixFlow.Infrastructure/Ai/Parsers/ImageFileParser.cs` |
| OverlapChunker | `OrvixFlow.Infrastructure/Ai/Chunking/OverlapChunker.cs` |
| FileIngestionJob | `OrvixFlow.Infrastructure/Ai/Jobs/FileIngestionJob.cs` |
| TrialExpirationJob | `OrvixFlow.Api/Jobs/TrialExpirationJob.cs` |
| PendingPlanChangeJob | `OrvixFlow.Api/Jobs/PendingPlanChangeJob.cs` |
| UsagePeriodRolloverJob | `OrvixFlow.Api/Jobs/UsagePeriodRolloverJob.cs` |
| InboxProcessingJob | `OrvixFlow.Api/Jobs/InboxProcessingJob.cs` |
| FeedbackEnrichmentJob | `OrvixFlow.Api/Jobs/FeedbackEnrichmentJob.cs` |
| NotificationProcessorJob | `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs` |
| EmailOptions | `OrvixFlow.Infrastructure/Services/EmailOptions.cs` |
| SmtpEmailService | `OrvixFlow.Infrastructure/Services/SmtpEmailService.cs` |
| ResendEmailService | `OrvixFlow.Infrastructure/Services/ResendEmailService.cs` |
| Load Test Script | `scripts/load-test-inbox.sh` |
| Company Detail Page | `orvixflow-web/app/admin/companies/[id]/page.tsx` |
| Company Audit Page | `orvixflow-web/app/admin/companies/[id]/audit/page.tsx` |
| Modules Page | `orvixflow-web/app/admin/modules/page.tsx` |
| Inbox Settings Page | `orvixflow-web/app/(dashboard)/settings/inbox/page.tsx` |
| Inbox History Page | `orvixflow-web/app/(dashboard)/inbox/history/page.tsx` |
| Admin Inbox Metrics | `orvixflow-web/app/(admin)/inbox-metrics/page.tsx` |
| LocalFileStorage | `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs` — [LEGACY] dev-only fallback |
| PolicyGateService | `OrvixFlow.Infrastructure/Services/PolicyGateService.cs` |
| WebhookCallbackService | `OrvixFlow.Infrastructure/Services/WebhookCallbackService.cs` |
| DraftFeedbackService | `OrvixFlow.Infrastructure/Services/DraftFeedbackService.cs` |
| BackgroundTenantProvider | `OrvixFlow.Infrastructure/Services/BackgroundTenantProvider.cs` |
| PlanService | `OrvixFlow.Infrastructure/Services/PlanService.cs` |
| EntitlementResolver | `OrvixFlow.Infrastructure/Services/EntitlementResolver.cs` |
| CompanySubscriptionService | `OrvixFlow.Infrastructure/Services/CompanySubscriptionService.cs` |
| StripeService | `OrvixFlow.Infrastructure/Services/Stripe/StripeService.cs` |
| StripeWebhookService | `OrvixFlow.Infrastructure/Services/Stripe/StripeWebhookService.cs` |
| DraftFeedbackService | `OrvixFlow.Infrastructure/Services/DraftFeedbackService.cs` |
| AuditService (Shadow) | `OrvixFlow.Infrastructure/Shadow/AuditService.cs` |
| UsageService (Shadow) | `OrvixFlow.Infrastructure/Shadow/UsageService.cs` |
| NoopVirusScanService | `OrvixFlow.Infrastructure/Services/Security/NoopVirusScanService.cs` |
| RagMetricsCollector | `OrvixFlow.Infrastructure/Ai/RagMetricsCollector.cs` |
| UsageAlertService | `OrvixFlow.Infrastructure/Services/UsageAlertService.cs` |

## API Middleware & Filters

| Component | Location |
|-----------|----------|
| HmacSignatureMiddleware | `OrvixFlow.Api/Middleware/HmacSignatureMiddleware.cs` |
| RequireModuleAttribute | `OrvixFlow.Api/Filters/RequireModuleAttribute.cs` |
| RequireAutomationKeyAttribute | `OrvixFlow.Api/Filters/RequireAutomationKeyAttribute.cs` |
| TenantProvider | `OrvixFlow.Api/Services/TenantProvider.cs` |

## Frontend

| Component | Location |
|-----------|----------|
| Auth Config | `orvixflow-web/auth.ts` |
| API Client | `orvixflow-web/lib/api-client.ts` |
| Login Page | `orvixflow-web/app/login/page.tsx` |
| Register Page | `orvixflow-web/app/register/page.tsx` |
| Dashboard Layout | `orvixflow-web/app/(dashboard)/layout.tsx` |
| Module Gate | `orvixflow-web/components/module-gate.tsx` |
| Settings Tabs | `orvixflow-web/components/settings/*.tsx` |
| Settings Billing | `orvixflow-web/app/(dashboard)/settings/billing/page.tsx` |
| Billing/Upgrade Page | `orvixflow-web/app/(dashboard)/billing/page.tsx` |
| Module Gate | `orvixflow-web/components/module-gate.tsx` |
| Admin Plans Page | `orvixflow-web/app/admin/plans/page.tsx` |
| Admin Company Detail | `orvixflow-web/app/admin/companies/[id]/page.tsx` |
| Inbox Settings | `orvixflow-web/app/(dashboard)/settings/inbox/page.tsx` |
| Inbox History | `orvixflow-web/app/(dashboard)/inbox/history/page.tsx` |
| Admin Inbox Metrics | `orvixflow-web/app/(admin)/inbox-metrics/page.tsx` |

## Data

| Component | Location |
|-----------|----------|
| AppDbContext | `OrvixFlow.Infrastructure/Data/AppDbContext.cs` |
| InboxEventRepository | `OrvixFlow.Infrastructure/Data/InboxEventRepository.cs` |
| Migrations | `OrvixFlow.Infrastructure/Migrations/*.cs` |
| AddStoredObjectTable Migration | `OrvixFlow.Infrastructure/Migrations/20260420115843_AddStoredObjectTable.cs` |
| AddPlanSystem Migration | `OrvixFlow.Infrastructure/Migrations/AddPlanSystem.cs` |

## Configuration

| File | Purpose |
|------|---------|
| `OrvixFlow.Api/Program.cs` | API startup, DI registration |
| `OrvixFlow.Api/appsettings.json` | API configuration |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Service registration |
| `docker-compose.yml` | Full stack environment |
| `orvixflow-web/next.config.ts` | Next.js configuration |
| `orvixflow-web/auth.ts` | NextAuth configuration |


## Storage & File Governance

| Component | Location |
|-----------|----------|
| StorageContext | `OrvixFlow.Core/Models/StorageContext.cs` |
| StoredObject | `OrvixFlow.Core/Entities/StoredObject.cs` |
| MinIOFileStorage | `OrvixFlow.Infrastructure/Storage/MinIOFileStorage.cs` |
| MinIOBucketInitializer | `OrvixFlow.Infrastructure/Storage/MinIOBucketInitializer.cs` |
| AzureBlobFileStorage | `OrvixFlow.Infrastructure/Storage/AzureBlobFileStorage.cs` |
| AzureBlobContainerInitializer | `OrvixFlow.Infrastructure/Storage/AzureBlobContainerInitializer.cs` |
| LocalToMinioMigrationJob | `OrvixFlow.Infrastructure/Storage/LocalToMinioMigrationJob.cs` |
| OrphanDetectionJob | `OrvixFlow.Infrastructure/Storage/OrphanDetectionJob.cs` |
| LocalFileStorage | `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs` — [LEGACY] dev-only fallback |
| StorageHealthCheck | `OrvixFlow.Api/Health/StorageHealthCheck.cs` |
| StorageMigrationController | `OrvixFlow.Api/Controllers/StorageMigrationController.cs` |
