# OrvixFlow - Architecture

## Layered Architecture

```
┌─────────────────────────────────────┐
│         orvixflow-web               │  Next.js 16 + NextAuth 5
│    (pages, components, hooks)       │
└──────────────┬──────────────────────┘
               │ HTTP + JWT Bearer
┌──────────────▼──────────────────────┐
│          OrvixFlow.Api              │  Controllers, Filters, Middleware
│    (Program.cs, Controllers/)       │
└──────────────┬──────────────────────┘
               │ DI
┌──────────────▼──────────────────────┐
│         OrvixFlow.Core              │  Entities, Interfaces, Models
│   (Entities/, Interfaces/, Models/) │
└──────────────┬──────────────────────┘
               │ DI
┌──────────────▼──────────────────────┐
│      OrvixFlow.Infrastructure      │  Services, Data, AI
│   (Services/, Data/, Auth/, Ai/)   │
└─────────────────────────────────────┘
```

## Multi-Tenancy

**Implementation:** EF Core Query Filters + TenantProvider

- Every entity has `TenantId` or `CompanyId` (same concept)
- `AppDbContext` applies global query filters (lines 168-185)
- `ITenantProvider` interface extracts tenant from JWT claims
- `TenantProvider` (Api/Services) resolves current tenant

**Tenant Resolution Order:**
1. JWT claim `TenantId` or `ActiveCompanyId`
2. For webhooks: `X-Tenant-ID` header fallback

## Authentication & Authorization

### JWT Claims
```
sub: UserId
email: user@company.com
TenantId: Guid
ActiveCompanyId: Guid
Role: "CompanyOwner" | "CompanyAdmin" | "Manager" | "Member"
Plan: "Free" | "Trialing" | "Pro"
DisplayName: "John Doe"
```

### Role Hierarchy
```
CompanyOwner (full access)
    └── CompanyAdmin
        └── Manager  
            └── Member
```

### Module Permission System

- **ModuleDefinition**: Registered modules (key, name, isActive)
- **ModuleAssignment**: Company/Department/User level assignments
- **ModulePermissionGrant**: Specific permissions (CanView, CanUse, CanTest, CanConfigure, CanManageIntegrations, CanManagePrompts, CanViewLogs, IsAdmin)

**Scopes:** "Company" | "Department" | "User"

**Gating:** `[RequireModule("module-key")]` attribute on controllers/actions

## AI Integration

**Semantic Kernel Setup:**
- Configured in `Infrastructure/DependencyInjection.cs:38-82`
- Supports: OpenAI, Groq, or Mock (for testing)
- Plugins: KnowledgeBaseSearchPlugin, N8nAutomationPlugin

**Agent Flow:**
1. User prompt → AgentController
2. AgentService.ProcessInternalAsync
3. Semantic Kernel invokes plugins
4. Response + audit + usage recording

## Background Jobs

**Hangfire** with PostgreSQL storage:
- Dashboard: `/hangfire` (local only)
- Jobs registered in `InboxProcessingJob.cs`
- Endpoint: `api/inbox/process`

## Webhook Security

HMAC-SHA256 signature validation:
- Header: `X-Orvix-Signature: sha256=<hex-hmac>`
- Tenant webhook secret stored in `Tenant.WebhookSecret`
- Middleware: `HmacSignatureMiddleware`
- Only applies to `/api/webhook/inbox`
