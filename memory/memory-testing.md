# OrvixFlow - Testing Guide

## Test Project Location

`OrvixFlow.Tests/`

## Running Tests

```bash
dotnet test
```

## Test Files & Coverage

| Test File | Coverage |
|-----------|----------|
| `TenantIsolationTests.cs` | Multi-tenancy query filters |
| `TenantProviderTests.cs` | Tenant resolution from JWT |
| `AccessResolverTests.cs` | Module permission resolution |
| `PolicyGateServiceTests.cs` | Workflow policy evaluation |
| `AuthControllerTests.cs` | Auth endpoints |
| `AgentServiceTests.cs` | AI agent processing |
| `IntentClassifierServiceTests.cs` | Email classification |
| `IngestionServiceTests.cs` | Text embedding/ingestion |
| `InboxEventIdempotencyTests.cs` | Message deduplication |
| `N8nAutomationPluginTests.cs` | n8n automation plugin |
| `OrgHierarchyTests.cs` | Department/user hierarchies |
| `AuditTrailTests.cs` | Audit logging |
| `ActionsControllerTests.cs` | Action/policy endpoints |
| `HmacSignatureMiddlewareTests.cs` | Webhook HMAC validation |

## Test Utilities

### In-Memory Database

Tests use EF Core InMemory provider. Note: pgvector extensions ignored:
```csharp
if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
{
    modelBuilder.Entity<KnowledgeBase>().Ignore(k => k.EmbeddingVector);
}
```

### Query Filter Bypass

Use `IgnoreQueryFilters()` to bypass tenant isolation in tests:
```csharp
_db.Users.IgnoreQueryFilters()
```

### Key Test Patterns

1. **Tenant Isolation**: Verify queries only return current tenant's data
2. **Permission Resolution**: Test Company/Department/User scope resolution
3. **Idempotency**: Test duplicate message handling via MessageId
4. **HMAC Validation**: Test webhook signature verification

## Frontend Tests

```bash
cd orvixflow-web && npm run test
```

| Test File | Coverage |
|-----------|----------|
| `app/register/page.test.tsx` | Registration flow |
| `lib/api-client.test.ts` | API client helper |
