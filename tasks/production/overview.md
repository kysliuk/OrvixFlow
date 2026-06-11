# OrvixFlow — Production Execution Overview

> **Created:** 2026-06-11  
> **Audit baseline:** 561 tests passing · 0 failures · Last commit: `893b69f` (2026-04-24)  
> **Source:** `tasks/production_readiness_roadmap.md` (full audit document)  
> **Target:** All files in `tasks/production/` are execution documents for AI agents.

---

## Project Objective

OrvixFlow is a multi-tenant SaaS platform with AI-powered email assistant (Inbox Guardian), RAG knowledge retrieval, workflow automation (n8n), and billing/subscription management.

The goal of this track is to bring OrvixFlow from ~70% production-ready to **fully production-deployable with all critical features operational**.

---

## Current State Summary

The backend architecture, auth, RBAC, billing, RAG, and admin panel are all implemented and test-covered. The remaining gaps are:

| Area | Status |
|---|---|
| Auth / Sessions / RBAC | ✅ Complete |
| Knowledge Base / RAG | ✅ Complete |
| Admin Panel | ✅ Complete |
| Billing (test-mode) | ✅ Complete |
| Inbox Guardian (backend) | ✅ Complete |
| Storage (MinIO/Azure/Local) | ✅ Complete |
| Email delivery (code) | ✅ Complete |
| **Security hardening gaps** | ❌ n8n unauthenticated, CSP missing, no register rate limit |
| **Email delivery (e2e proven)** | ❌ Not validated in production |
| **Stripe live-mode** | ❌ Not configured; missing webhook secret in .env.example |
| **Mailbox OAuth credential capture** | ❌ 0% complete — largest missing feature |
| **CI/CD pipeline** | ❌ Missing |
| **Production deployment config** | ❌ Missing |
| **Observability / monitoring** | ❌ Missing |
| **Database backup** | ❌ Missing |

---

## Production Target State

1. n8n is secured and using a production encryption key
2. CSP headers are present across all surfaces
3. Email delivery is proven end-to-end in production
4. Stripe is operating in live-mode with real subscriptions
5. Mailbox OAuth credentials are captured, encrypted, and provisioned to n8n
6. CI/CD pipeline runs on every PR and deploys `main` automatically
7. Observability covers API traces, job failures, and error alerting
8. Database has automated daily backups with tested restore

---

## Phase Ordering

```
Phase 0 — Security & Stability Hardening
    ↓
Phase 1 — Production Email Validation
    ↓
Phase 2 — Stripe Live-Mode & Subscription Completeness
    ↓
Phase 3 — Mailbox OAuth Credential Capture  (can start in parallel with Phase 4)
    ↓
Phase 4 — CI/CD Pipeline  (can run in parallel with Phase 2 or 3)
    ↓
Phase 5 — Observability, Database Backup & Production Operations
```

**Phases 3 and 4 may run in parallel.** Phase 4 (CI/CD) has no dependency on Phase 3 (mailbox OAuth) and can be executed concurrently by a separate agent or developer stream.

**Minimum launch path (without mailbox OAuth):** Phase 0 → 1 → 2 → 4 → 5 (~5 weeks)  
**Full feature completeness (with mailbox OAuth):** Phases 0–5 (~10–12 weeks)

---

## Dependencies Between Phases

| Phase | Depends On | Reason |
|---|---|---|
| Phase 0 | None | All fixes are standalone |
| Phase 1 | Phase 0 | Needs secure env before testing real email delivery |
| Phase 2 | Phase 0 | Needs `STRIPE_WEBHOOK_SECRET` in env before live-mode |
| Phase 3 | Phase 0, Phase 1 | n8n must be secured; email delivery needed for mailbox credential notifications |
| Phase 4 | Phase 0 | Env must be documented before wiring GitHub Secrets |
| Phase 5 | Phase 0, Phase 4 | Needs CI/CD and infrastructure before production ops setup |

---

## Roadmap Corrections Made

The original roadmap listed Phase 4 (CI/CD) after Phase 3 (Mailbox OAuth). This ordering is incorrect because:
- CI/CD has zero dependency on mailbox OAuth
- CI/CD should be established before (or alongside) any feature work in Phase 3
- Executing CI/CD after mailbox OAuth leaves 4–6 weeks of feature work unprotected by automated tests

**Correction:** Phase 4 is now runnable in parallel with Phase 3. The phase files reflect this.

No other structural corrections were made. The blocker ordering, security priorities, and dependency chain are verified consistent.

---

## Architectural Constraints (ALL agents must respect these)

### Clean Architecture — Layer Rules
- `OrvixFlow.Core` — domain entities, interfaces, enums only. NO infrastructure dependencies.
- `OrvixFlow.Infrastructure` — implements Core interfaces. EF Core, external services. NO Api dependencies.
- `OrvixFlow.Api` — controllers, jobs, middleware, health checks. Depends on Core and Infrastructure.
- `orvixflow-web/` — Next.js frontend. No backend code.

### EF Core
- All tenant-scoped entities have global query filters applied in `AppDbContext.OnModelCreating`
- Any `IgnoreQueryFilters()` call requires an explicit justification comment and must be admin-level or background-job-scoped only
- Never remove global query filters from tenant-scoped entities
- Use `Guid.NewGuid()` for entity IDs initialized in field initializer
- Use `string.Empty` not `null` for required string fields

### DI Pattern
- Extension method in `OrvixFlow.Infrastructure/DependencyInjection.cs`: `AddInfrastructure(this IServiceCollection, IConfiguration)`
- Scoped lifetime for most services
- Never register infrastructure services directly in `Program.cs` — always via `AddInfrastructure`

### JWT / Auth Claims (exact names — do not rename)
```
sub              → UserId (Guid)
email            → user@company.com
TenantId         → Guid (same as CompanyId)
ActiveCompanyId  → Guid (legacy alias — both resolved by TenantProvider)
Role             → CompanyOwner | CompanyAdmin | CompanyMember | SuperAdmin | InternalOperator
Plan             → "Free" | "Trialing" | "Pro" | ...
DisplayName      → display string
```

### Three-Layer RBAC
- Layer 0 (Global): `User.Role` → SuperAdmin, InternalOperator, or empty
- Layer 1 (Company): `UserCompanyMembership.CompanyRole` → CompanyOwner, CompanyAdmin, CompanyMember
- Layer 2 (Department): `UserDepartmentMembership.DepartmentRole` → DepartmentManager, DepartmentOperator
- JWT `Role` claim = Layer 1 company role (or Layer 0 if platform admin)
- Department role is NEVER trusted from JWT; always resolved from `UserDepartmentMembership` DB rows
- Legacy roles (`DepartmentManager` as company role, `Operator`, `Viewer`) are migrated aliases only — do NOT reintroduce

### Tenant Isolation
- Every tenant-scoped DB query must go through EF query filters
- No controller may accept a `tenantId` from user input — always resolve from JWT claims via `TenantProvider`
- `BackgroundTenantProvider` is used ONLY in Hangfire jobs with explicit per-job tenant context
- `IgnoreQueryFilters()` is only valid in: SuperAdmin admin controllers, background jobs, and `NotificationProcessorJob` (cross-tenant batch)

### Naming Conventions
- Classes/Interfaces/Methods/Properties: `PascalCase`
- Interfaces: `I` prefix (e.g., `IAuthService`)
- Private fields: `_camelCase`
- File-scoped namespaces: `namespace OrvixFlow.Core.Entities;`
- Explicit `using` statements — no implicit usings

---

## No-Break Rules (ANY agent must refuse to violate these)

1. **Never auto-link OAuth login identity with mailbox credential** — OAuth login and mailbox OAuth are separate consent flows
2. **Never set `User.Role` to a company role value** — `User.Role` is platform-only
3. **Never compare `User.Role` against company role values** — use `UserCompanyMembership.CompanyRole`
4. **Never weaken global EF query filters** — tenant isolation is the most critical invariant
5. **Never commit secrets** — all secrets load from `.env` (gitignored) or environment variables
6. **Never log verification tokens, invite tokens, SMTP passwords, or provider OAuth tokens**
7. **Never return provider OAuth tokens in API responses**
8. **Never store refresh tokens or OAuth tokens unencrypted at rest**
9. **Never bypass the `[RequireModule]` billing + permission gate for module-level endpoints**
10. **Never merge OAuth accounts silently by email** — `ProvisionOAuthUserAsync` rejects mismatched providers

---

## Security Requirements

- All secrets via environment variables — no hardcoded values in tracked files
- Password: min 12 chars, lower + upper + digit + special char
- JWT: 60-minute lifetime, validated issuer/audience/signing key
- Rate limiting: 5/min on login (per IP), 10/hour on register (per IP), 30/min on AI endpoints (per tenant+IP)
- HMAC-SHA256 validation on n8n webhook ingestion (`HmacSignatureMiddleware`)
- Security headers: X-Content-Type-Options, X-Frame-Options, Referrer-Policy, X-XSS-Protection, HSTS (prod)
- CSP: must be implemented (currently deferred — Phase 0 task)
- n8n admin UI must be authenticated in all non-development deployments
- Virus scanning (ClamAV) must be enabled in production (`Security:VirusScan:Provider=ClamAv`)
- Hangfire dashboard protected by SuperAdmin JWT filter

---

## Testing Requirements

### Backend (xUnit)
- Run: `dotnet test` from repo root
- Each test class must use unique InMemory DB: `Guid.NewGuid().ToString()` as DB name
- Use `IDisposable` for cleanup
- Use `FluentAssertions` for assertions
- Private mock classes within test files (e.g., `MockTenantProvider`)
- Implicit global `using Xunit;` applies — no per-file import needed
- Baseline: 561 passing, 0 failing. **Any PR that breaks this baseline must not be merged.**

### Frontend (Vitest)
- Run: `npm run test` from `orvixflow-web/`
- Run: `npm run lint` — ESLint only, no Prettier
- Run: `npm run build` — must produce no TypeScript errors
- Tests co-located with pages: `app/register/page.test.tsx`

---

## Deployment Requirements

- Docker Compose is the deployment mechanism (current)
- `docker-compose.yml` — development/local
- `docker-compose.prod.yml` — production target (to be created in Phase 5)
- All services must have health checks configured
- EF Core migrations run automatically on startup via `db.Database.Migrate()` in `Program.cs`
- n8n admin UI must be authenticated in production
- TLS termination via Traefik or Caddy (to be configured in Phase 5)

---

## AI Agent Execution Rules

1. **Read `memory/` before touching auth, multi-tenancy, or billing code** — specifically `memory-security.md`, `memory-risks.md`, `memory-architecture.md`
2. **Run `dotnet test` after ANY backend change** — zero failures must be maintained
3. **Run `npm run build && npm run lint && npm run test` after ANY frontend change**
4. **Do not guess on unclear requirements** — mark as `[UNVERIFIED]` or ask
5. **Do not implement Phase N+1 work during Phase N** — stay in scope
6. **Do not remove or modify existing tests** without explicit justification
7. **Follow Clean Architecture layer rules** — no layer violations
8. **Update `memory/` files after any major architectural change**
9. **Do not commit** unless explicitly instructed by the user
10. **Read `tasks/lessons.md`** before starting any multi-step implementation task

---

## Known Risks to Watch

| Risk | Where documented | Action |
|---|---|---|
| R1: n8n encryption key is dev placeholder | docker-compose.yml | Phase 0: rotate before deploy |
| R2: Empty mailbox credentials to n8n | N8nProvisioningService | Phase 3: implement real provisioning |
| R3: Stripe webhook secret not in .env.example | .env.example | Phase 0: add immediately |
| R4: Email delivery never proven in prod | NotificationProcessorJob | Phase 1: e2e test |
| R5: No CI pipeline | — | Phase 4: create |
| R6: No DB backup | docker-compose.yml | Phase 5: configure |
| R7: Dual admin route structure | app/admin/ vs app/(admin)/ | Phase 0: audit and consolidate |
| R8: AzureBlob not integration-tested | AzureBlobFileStorage | Phase 5: verify |
| R9: BackgroundTenantProvider concurrency | InboxProcessingJob | Phase 5: verify |
| R10: memory-security.md says "no refresh token" (wrong) | memory/memory-security.md | Phase 1: update doc |
