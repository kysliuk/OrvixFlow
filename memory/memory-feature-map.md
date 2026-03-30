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

**Files:**
- `OrvixFlow.Api/Controllers/InboxController.cs`
- `OrvixFlow.Api/Controllers/WebhookController.cs`
- `OrvixFlow.Api/Controllers/InboxEventsController.cs`
- `OrvixFlow.Api/Jobs/InboxProcessingJob.cs`
- `OrvixFlow.Infrastructure/Ai/IngestionService.cs`
- `OrvixFlow.Infrastructure/Ai/InboxGuardianService.cs`
- `OrvixFlow.Infrastructure/Data/InboxEventRepository.cs`

## AI Agent

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Chat | AgentController | AgentService | - |
| Intent Classification | AgentController | IntentClassifierService | - |
| Draft Generation | AgentController | DraftGeneratorService | - |

**Files:**
- `OrvixFlow.Api/Controllers/AgentController.cs`
- `OrvixFlow.Infrastructure/Ai/AgentService.cs`
- `OrvixFlow.Infrastructure/Ai/IntentClassifierService.cs`
- `OrvixFlow.Infrastructure/Ai/DraftGeneratorService.cs`
- `OrvixFlow.Infrastructure/Ai/Plugins/KnowledgeBaseSearchPlugin.cs`
- `OrvixFlow.Infrastructure/Ai/Plugins/N8nAutomationPlugin.cs`

## Knowledge Base

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Search | KnowledgeBaseController | HybridVectorSearchService | KnowledgeBase |
| Vector Index | - | IngestionService (embedding) | KnowledgeBase |

**Files:**
- `OrvixFlow.Api/Controllers/KnowledgeBaseController.cs`
- `OrvixFlow.Infrastructure/Ai/HybridVectorSearchService.cs`
- `OrvixFlow.Core/Entities/KnowledgeBase.cs`

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

## Billing & Plans (Admin Panel Phase 1)

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Plan Templates | PlansController | PlanService | PlanTemplate |
| Plan Modules | - | - | PlanModuleInclusion |
| Plan Entitlements | - | - | PlanEntitlements |
| Company Subscription | - | CompanySubscriptionService | CompanySubscription |
| Entitlement Resolution | - | EntitlementResolver | - |
| Subscription | BillingController | - | BillingSubscription |
| Usage Tracking | - | UsageService | UsageEvent |

**Files:**
- `OrvixFlow.Api/Controllers/PlansController.cs`
- `OrvixFlow.Core/Entities/PlanTemplate.cs`
- `OrvixFlow.Core/Entities/PlanModuleInclusion.cs`
- `OrvixFlow.Core/Entities/PlanEntitlements.cs`
- `OrvixFlow.Core/Entities/CompanySubscription.cs`
- `OrvixFlow.Core/Interfaces/IPlanService.cs`
- `OrvixFlow.Core/Interfaces/IEntitlementResolver.cs`
- `OrvixFlow.Core/Interfaces/ICompanySubscriptionService.cs`
- `OrvixFlow.Infrastructure/Services/PlanService.cs`
- `OrvixFlow.Infrastructure/Services/EntitlementResolver.cs`
- `OrvixFlow.Infrastructure/Services/CompanySubscriptionService.cs`
- `OrvixFlow.Api/Controllers/BillingController.cs`
- `OrvixFlow.Core/Entities/BillingSubscription.cs`
- `OrvixFlow.Core/Entities/UsageEvent.cs`
- `OrvixFlow.Infrastructure/Shadow/UsageService.cs`

## Audit & Compliance

| Feature | Controller | Entity |
|---------|-----------|--------|
| Audit Log | AuditController | AuditTrail |

**Files:**
- `OrvixFlow.Api/Controllers/AuditController.cs`
- `OrvixFlow.Core/Entities/AuditTrail.cs`
- `OrvixFlow.Infrastructure/Shadow/AuditService.cs`

## Workflow Policies

| Feature | Controller | Service | Entity |
|---------|-----------|---------|--------|
| Policy Gates | ActionsController | PolicyGateService | WorkflowPolicy |
| Action Requests | ActionsController | - | ActionRequest |
| Workflow Logs | - | - | WorkflowLog |

**Files:**
- `OrvixFlow.Api/Controllers/ActionsController.cs`
- `OrvixFlow.Infrastructure/Services/PolicyGateService.cs`
- `OrvixFlow.Core/Entities/WorkflowPolicy.cs`
- `OrvixFlow.Core/Entities/ActionRequest.cs`

## Administration

| Feature | Controller |
|---------|-----------|
| Admin Panel | AdminController |
| Organization | OrganizationController |

**Files:**
- `OrvixFlow.Api/Controllers/AdminController.cs`
- `OrvixFlow.Api/Controllers/OrganizationController.cs`

## Frontend Pages

| Page | Route | Auth Required |
|------|-------|---------------|
| Login | /login | No |
| Register | /register | No |
| Dashboard | /(dashboard)/page.tsx | Yes |
| Inbox | /(dashboard)/inbox/page.tsx | Yes |
| Pending Items | /(dashboard)/inbox/pending/page.tsx | Yes |
| Knowledge | /(dashboard)/knowledge/page.tsx | Yes |
| Settings | /(dashboard)/settings/page.tsx | Yes |
| Billing | /(dashboard)/billing/page.tsx | Yes |
| Admin | /admin/page.tsx | Yes |
