# OrvixFlow - Production Execution Overview

> Updated: 2026-06-11
> Source of truth: `tasks/production/current-state-audit.md`

## Project Objective

OrvixFlow is a multi-tenant SaaS platform with AI-powered email assistant, RAG knowledge retrieval, workflow automation, and billing/subscription management.

The current production track is focused on turning the existing implementation into a truthful, deployable, production-safe release baseline.

## Current State Summary

The codebase is materially implemented, but the release path is not production-ready yet.

| Area | Status |
|---|---|
| Architecture / core backend | Mostly complete |
| Auth / sessions / RBAC | Mostly complete |
| Billing / entitlements | Partial |
| Mailbox OAuth / Inbox Guardian | Partial |
| Storage | Mostly complete |
| Observability / runbooks | Partial |
| Frontend release build | Broken |
| CI/CD / deployment | Broken |
| Production runtime configuration | Broken |
| Documentation / production tracker accuracy | Broken |

## Verified Baseline

Do not use older phase files or historical notes as the primary truth source when they conflict with the codebase.

Use this priority order:

1. live code and configuration
2. recent commits
3. `tasks/production/current-state-audit.md`
4. memory files
5. older task-planning documents

## Current Release Blockers

1. Frontend production build fails in `orvixflow-web/next.config.ts`.
2. CI depends on that failing build and is therefore not currently trustworthy.
3. `.github/workflows/deploy.yml` is still a stub and can report success without deployment.
4. `docker-compose.prod.yml` is not fully aligned with the workflow and runtime requirements.
5. Production docs currently overstate what is complete.

## Execution Strategy

The active work order is:

1. Phase 0: Stabilization and release blockers
2. Phase 1: Production config correctness
3. Phase 2: Live provider validation
4. Phase 3: Documentation and memory reconciliation
5. Phase 4: Final production proof and operations validation

## Active Phase Definitions

### Phase 0 - Stabilization and release blockers

Goal: restore a truthful buildable and deployable baseline.

Scope:
- fix frontend build
- restore CI truthfulness
- replace fake deploy flow
- align image naming and public API URL handling

### Phase 1 - Production config correctness

Goal: make production runtime configuration internally consistent.

Scope:
- fix `docker-compose.prod.yml`
- wire required AI, automation, mailbox OAuth, and security settings
- remove unsafe defaults for production

### Phase 2 - Live provider validation

Goal: validate external dependencies with real or production-like providers.

Scope:
- Stripe live-mode validation
- email delivery validation
- mailbox OAuth validation
- n8n workflow validation
- storage provider validation

### Phase 3 - Documentation and memory reconciliation

Goal: make the planning surface match reality.

Scope:
- update stale production task files
- update stale memory files
- archive or mark obsolete plans

### Phase 4 - Final production proof

Goal: prove the system is operationally ready.

Scope:
- full deployment validation
- health checks
- backup and restore proof
- observability sink validation

## Rules For Future Updates

1. Do not mark a phase complete unless the code, runtime path, and validation evidence all exist.
2. Do not treat workflow file creation as deployment completion.
3. Do not treat provider-facing code as operationally complete without live validation.
4. If documentation conflicts with code, update the documentation and note the mismatch explicitly.
5. Update `current-state-audit.md` when production status changes materially.
