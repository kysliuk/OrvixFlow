# OrvixFlow - File Map

## Core Entities

| Entity | Location |
|--------|----------|
| User | `OrvixFlow.Core/Entities/User.cs` |
| Tenant | `OrvixFlow.Core/Entities/Tenant.cs` |
| Department | `OrvixFlow.Core/Entities/Department.cs` |
| KnowledgeBase | `OrvixFlow.Core/Entities/KnowledgeBase.cs` |
| InboxEvent | `OrvixFlow.Core/Entities/InboxEvent.cs` |
| AuditTrail | `OrvixFlow.Core/Entities/AuditTrail.cs` |
| WorkflowPolicy | `OrvixFlow.Core/Entities/WorkflowPolicy.cs` |
| ActionRequest | `OrvixFlow.Core/Entities/ActionRequest.cs` |
| ModuleDefinition | `OrvixFlow.Core/Entities/ModuleDefinition.cs` |
| ModuleAssignment | `OrvixFlow.Core/Entities/ModuleAssignment.cs` |
| ModulePermissionGrant | `OrvixFlow.Core/Entities/ModulePermissionGrant.cs` |
| BillingSubscription | `OrvixFlow.Core/Entities/BillingSubscription.cs` |
| UsageEvent | `OrvixFlow.Core/Entities/UsageEvent.cs` |
| Invitation | `OrvixFlow.Core/Entities/Invitation.cs` |

## Core Interfaces

| Interface | Location |
|-----------|----------|
| ITenantProvider | `OrvixFlow.Core/Interfaces/ITenantProvider.cs` |
| IScopeContext | `OrvixFlow.Core/Interfaces/IScopeContext.cs` |
| IAuthService | `OrvixFlow.Core/Interfaces/IAuthService.cs` |
| IAgentService | `OrvixFlow.Core/Interfaces/IAgentService.cs` |
| IIngestionService | `OrvixFlow.Core/Interfaces/IIngestionService.cs` |
| IInboxGuardianService | `OrvixFlow.Core/Interfaces/IInboxGuardianService.cs` |
| IAccessResolver | `OrvixFlow.Core/Interfaces/IAccessResolver.cs` |
| IAuditService | `OrvixFlow.Core/Interfaces/IAuditService.cs` |
| IUsageService | `OrvixFlow.Core/Interfaces/IUsageService.cs` |

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
| AdminController | `OrvixFlow.Api/Controllers/AdminController.cs` |
| OrganizationController | `OrvixFlow.Api/Controllers/OrganizationController.cs` |

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
| HybridVectorSearchService | `OrvixFlow.Infrastructure/Ai/HybridVectorSearchService.cs` |
| PolicyGateService | `OrvixFlow.Infrastructure/Services/PolicyGateService.cs` |
| WebhookCallbackService | `OrvixFlow.Infrastructure/Services/WebhookCallbackService.cs` |
| BackgroundTenantProvider | `OrvixFlow.Infrastructure/Services/BackgroundTenantProvider.cs` |
| AuditService (Shadow) | `OrvixFlow.Infrastructure/Shadow/AuditService.cs` |
| UsageService (Shadow) | `OrvixFlow.Infrastructure/Shadow/UsageService.cs` |

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

## Data

| Component | Location |
|-----------|----------|
| AppDbContext | `OrvixFlow.Infrastructure/Data/AppDbContext.cs` |
| InboxEventRepository | `OrvixFlow.Infrastructure/Data/InboxEventRepository.cs` |
| Migrations | `OrvixFlow.Infrastructure/Migrations/*.cs` |

## Configuration

| File | Purpose |
|------|---------|
| `OrvixFlow.Api/Program.cs` | API startup, DI registration |
| `OrvixFlow.Api/appsettings.json` | API configuration |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Service registration |
| `docker-compose.yml` | Full stack environment |
| `orvixflow-web/next.config.ts` | Next.js configuration |
| `orvixflow-web/auth.ts` | NextAuth configuration |
