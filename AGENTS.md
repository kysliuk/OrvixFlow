# OrvixFlow — Agent Instructions

## Project Overview

Multi-tenant SaaS platform with AI-powered email assistant ("Inbox Guardian"), RAG knowledge retrieval, workflow automation (n8n), and billing/subscription management.

**Stack:** .NET 9.0 (ASP.NET Core, EF Core, PostgreSQL+pgvector, Semantic Kernel) + Next.js 16 (React 19, TypeScript 5, Tailwind, NextAuth 5).

**Architecture:** Clean Architecture — `OrvixFlow.Core` (Domain) → `OrvixFlow.Infrastructure` (Data/Services) → `OrvixFlow.Api` (Web API) + `orvixflow-web/` (Next.js frontend).

---

## Build / Lint / Test Commands

### Backend (.NET)
```
dotnet build                              # Build entire solution
dotnet build OrvixFlow.Api                # Build specific project
dotnet run --project OrvixFlow.Api        # Run API server
dotnet test                               # Run all tests
dotnet test --filter "FullyQualifiedName~PlanTemplateTests"           # Single test class
dotnet test --filter "FullyQualifiedName~PlanTemplateTests.Create_ValidPlanTemplate_SavesToDb"  # Single test method
dotnet test --filter "DisplayName~Create"                            # Tests by display name
```

### Frontend (Next.js) — run from `orvixflow-web/`
```
npm run dev                               # Dev server
npm run build                             # Production build
npm run lint                              # ESLint
npm run test                              # Vitest tests
npx vitest run -t "registration"          # Single test by name pattern
```

### Docker
```
docker compose up -d                      # Start all services
docker compose up -d orvix-db             # Start PostgreSQL only
```

---

## Code Style

### C# Backend

**Imports & Namespaces:**
- File-scoped namespaces: `namespace OrvixFlow.Core.Entities;`
- Explicit `using` statements at top (not implicit)
- Always import types via `using` — never use fully-qualified names inline (e.g., no `Microsoft.Extensions.Logging.ILogger<T>`)

**Naming:**
- Classes/Interfaces/Methods/Properties: `PascalCase`
- Interfaces: `I` prefix (`IAuthService`)
- Private fields: `_camelCase` (`_db`, `_logger`, `_tenantProvider`)
- Records/DTOs: `PascalCase`, defined in same file as controller when request-specific
- Enums: `PascalCase` values (`SuperAdmin`, `CompanyOwner`)

**Types:**
- `<Nullable>enable</Nullable>` everywhere — use `string?`, `Guid?` properly
- Default strings to `string.Empty`, not `null`
- Use `Guid.NewGuid()` for entity IDs with initializer: `Guid Id { get; set; } = Guid.NewGuid();`

**Error Handling:**
- Result pattern: return typed results (`AuthResult`, `InviteResult`) with `IsSuccess` + `Error`
- Controllers: return proper `IActionResult` (`Ok`, `BadRequest`, `Conflict`, `Unauthorized`, `StatusCode(500)`)
- Throw `System.Exception` only for configuration errors (e.g., missing secrets)
- Use `ILogger<T>`, never `System.Console.WriteLine`

**EF Core:**
- Collection nav properties: `new List<T>()` or `[]`
- Use `IgnoreQueryFilters()` explicitly for admin-level access
- Multi-tenant query filters applied in `OnModelCreating`

**DI:**
- Extension method: `AddInfrastructure(this IServiceCollection, IConfiguration)`
- Scoped lifetime for most services

### TypeScript Frontend

- `"use client"` for client components
- Strict TypeScript enabled — avoid `as any` unless unavoidable (NextAuth session typing)
- Path alias `@/*` maps to project root
- Tailwind utility classes exclusively — no CSS modules
- lucide-react for icons

---

## Lessons Learned (from `tasks/lessons.md`)

**L1 — Domain Constants: Use Enums, Not Hardcoded Strings**
Any domain concept repeated 3+ times (roles, statuses, scopes) → make it an `enum` in `OrvixFlow.Core`. Store only serialized string in DB/JWT; parse back to enum at boundary. `UserRole` enum exists in `OrvixFlow.Core/Authorization/Roles.cs` with `ParseRole()` and `ToClaimValue()`.

**L2 — Workflow Orchestration**
Enter Plan Mode for 3+ step tasks. Use subagents to keep context clean. Verify before marking done. Demand elegance — no hacky fixes.

**L3 — Task Management**
Simplicity first, minimal impact. No laziness — find root causes, no temporary fixes.

---

## Project References

- **Memory folder** (`memory/`): Architecture, feature map, file map, risks, testing guide — read for project context
- **Tasks folder** (`tasks/`): Design docs (admin panel, RAG extension, inbox guardian) + lessons
- **Frontend agent rules**: `orvixflow-web/AGENTS.md` — Next.js 16 has breaking changes; read `node_modules/next/dist/docs/` before coding

---

## Testing Patterns

**Backend (xUnit):**
- EF Core InMemory DB with unique name per test: `Guid.NewGuid().ToString()`
- `IDisposable` test classes for cleanup
- `FluentAssertions` for assertions
- Private mock classes within test files (e.g., `MockTenantProvider`)
- Global implicit `using Xunit;` in test project

**Frontend (Vitest):**
- Tests co-located with pages: `app/register/page.test.tsx`
- Node environment configured

---

## Critical Rules

1. **Review `memory-risks.md`** before touching auth, multi-tenancy, or billing code
2. **Run tests** after any change — `dotnet test` or `npm run test`
3. **No `.editorconfig`** exists — follow conventions above
4. **No Prettier** on frontend — ESLint only
5. **Update memory after major changes** — After any significant feature, refactor, or architectural change, update the relevant files in `memory/` to reflect what was done (architecture, feature map, file map, risks as applicable)
6. **Commit only when explicitly asked**
