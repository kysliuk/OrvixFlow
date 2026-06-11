# OrvixFlow - Verified Current-State Audit

> Date: 2026-06-11
> Basis: verified against live code, workflow files, production compose, memory, tasks, and recent commits

## Executive Summary

OrvixFlow is not production-ready yet.

The core product is materially implemented: backend architecture is coherent, auth and refresh flows exist, RBAC is substantially complete, billing/storage/mailbox foundations are present, and the automated test base is strong.

The main risks are no longer basic feature gaps. The main risks are release integrity, deployment correctness, incomplete production configuration, and stale task documents that overstate completion.

## Verified Repo Findings

### Confirmed strengths

- Clean Architecture structure is intact across Core, Infrastructure, Api, and frontend.
- JWT auth, refresh flow, and no-org session handling are implemented.
- Three-layer access model exists across global, company, and department scopes.
- Billing and entitlement plumbing exists.
- Mailbox OAuth credential capture and encrypted storage were recently implemented.
- Storage abstraction exists with multiple providers.
- Observability and production runbook files were added recently.
- Recent commits show real implementation progress in RBAC, mailbox OAuth, CI/CD, and production operations.

### Confirmed blockers

1. Frontend production build is broken.
   - File: `orvixflow-web/next.config.ts`
   - Verified issue: Sentry config still includes `hideSourceMaps`, which matches the reported build failure source.

2. CI is not trustworthy as green.
   - File: `.github/workflows/ci.yml`
   - Verified issue: CI runs `npm run build`, so the frontend failure breaks the release gate.

3. Deploy workflow is still a stub.
   - File: `.github/workflows/deploy.yml`
   - Verified issue: the SSH deploy step does not execute the real deployment commands and still prints success.

4. Production image naming is inconsistent.
   - Files: `.github/workflows/deploy.yml`, `docker-compose.prod.yml`
   - Verified issue: workflow pushes `ghcr.io/${{ github.repository }}/...` while prod compose pulls `ghcr.io/${GITHUB_REPO_OWNER}/...`.

5. Production web build currently bakes a localhost API URL.
   - File: `.github/workflows/deploy.yml`
   - Verified issue: web image build arg still sets `NEXT_PUBLIC_API_URL=http://localhost:8080`.

6. `docker-compose.prod.yml` is incomplete for production runtime.
   - Verified issues:
   - automation key is wired as `Automation__Key`
   - virus scan defaults to `Noop`
   - mailbox OAuth API envs are not fully wired
   - AI provider runtime envs are not visibly wired in the API block

7. Production task docs are stale and contradictory.
   - `tasks/production/progress.md` marks phases 1-5 complete.
   - `tasks/production/overview.md` still describes several of those same areas as missing.

8. Some memory is stale.
   - `memory/auth.md` still says refresh tokens are stored in plaintext at rest, which conflicts with the newer implementation claims from recent work.

## Recent Commit Read

- `177ae6f`: production deployment config, Sentry, observability, runbooks
- `edd64ee`: CI/CD workflow files
- `58b6d6d`: mailbox credential management and OAuth flow
- `089de77`: production email validation support work
- `a448b4f`: admin route consolidation and security middleware work
- `893b69f`: RBAC permission resolution fix and tests

These commits support the conclusion that the codebase has moved forward materially, but the release path and production documentation did not keep pace.

## Current-State Matrix

| Area | Status | Notes |
|---|---|---|
| Architecture / Clean Architecture | Mostly complete | Structure is coherent and enforced in code layout |
| Auth and sessions | Mostly complete | JWT, refresh, switching, and no-org session behavior exist |
| Organizations / tenants / departments / roles | Mostly complete | RBAC redesign appears materially implemented |
| Module access and entitlements | Mostly complete | Access resolution and module gating are present |
| Billing / Stripe | Partial | Core code exists; live operational proof is still missing |
| Knowledge Base / RAG | Mostly complete | Broad implementation exists; full production proof is still limited |
| Inbox Guardian / email workflows | Partial | Core plumbing exists; production end-to-end proof is still missing |
| Production email sending | Partial | Provider code exists; real delivery validation remains unproven here |
| Storage / MinIO / S3 migration | Mostly complete | Code is ahead of the old migration task documents |
| Security remediation | Mostly complete | Significant hardening exists, but prod config still has gaps |
| Admin panel | Mostly complete | Backend/frontend admin work exists |
| UI / navigation | Partial | Core UI exists, but release build is broken |
| Tests | Mostly complete | Strong test posture, but build gate is not healthy |
| CI/CD / deployment | Broken | CI depends on broken build; deploy is still a stub |
| Logging / observability | Partial | Instrumentation exists, but release and sink validation remain incomplete |
| Production operations | Broken | Compose and runbooks exist, but runtime correctness is not proven |

## Production Blockers

### Critical

1. Fix `orvixflow-web/next.config.ts` so `npm run build` passes.
2. Restore CI truthfulness after the frontend build is fixed.
3. Replace the fake-success deploy step in `.github/workflows/deploy.yml` with a real deployment flow.
4. Standardize image tags between deploy workflow and `docker-compose.prod.yml`.
5. Stop building the production web image with `NEXT_PUBLIC_API_URL=http://localhost:8080`.
6. Complete `docker-compose.prod.yml` runtime configuration, especially AI, automation, mailbox OAuth, and virus scanning.

### High

1. Reconcile stale production docs in `tasks/production/`.
2. Reconcile stale memory files where they contradict current implementation.
3. Validate live provider flows for Stripe, email, mailbox OAuth, and n8n before claiming production readiness.

## Immediate Next Actions

1. Fix the Sentry/Next config regression in `orvixflow-web/next.config.ts`.
2. Re-run frontend build, lint, and tests.
3. Align `.github/workflows/deploy.yml` with `docker-compose.prod.yml` image references.
4. Correct production web API URL handling.
5. Audit backend option binding against `docker-compose.prod.yml` and add missing env wiring.
6. Change unsafe production defaults such as virus scan provider fallback.
7. Update `tasks/production/progress.md` and `tasks/production/overview.md` so they reflect the verified state.
8. Update stale memory after the runtime/deploy truth is restored.

## Recommendation

Do not treat the existing production tracker as authoritative.

Use this file as the current verified baseline, then execute a Phase 0 stabilization pass focused on:

1. frontend build health
2. CI and deploy correctness
3. production compose/runtime correctness
4. documentation truthfulness
