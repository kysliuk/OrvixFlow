# Phase 0 - Stabilization And Release Blockers

> Status: Active
> Depends on: none
> Blocks: all later production phases
> Source of truth: `tasks/production/current-state-audit.md`

## Goal

Restore a truthful, buildable, deployable baseline.

## Why This Phase Exists

The verified audit shows the current release path is broken even though large parts of the product are implemented.

This phase exists to remove the blockers that prevent the repository from being safely treated as releasable:

1. frontend production build failure
2. CI that depends on the broken build
3. deploy workflow that can report success without deploying
4. inconsistent image naming between workflow and production compose
5. production web builds that still bake `http://localhost:8080`

Do not start provider validation or production signoff while these issues remain open.

## In Scope

- fix the frontend build blocker in `orvixflow-web/next.config.ts`
- re-establish truthful frontend verification
- replace deploy stub behavior with real deployment commands and checks
- align container image references
- correct production web API URL handling

## Out Of Scope

- no provider validation
- no backup/restore validation
- no mailbox OAuth operational proof
- no Stripe live-mode proof
- no documentation-only completion claims
- no unrelated frontend refactors

## Audit Findings Covered

From `tasks/production/current-state-audit.md`:

- Critical 1: fix `orvixflow-web/next.config.ts` so `npm run build` passes
- Critical 2: restore CI truthfulness after frontend build is fixed
- Critical 3: replace fake-success deploy step in `.github/workflows/deploy.yml`
- Critical 4: standardize image tags between deploy workflow and `docker-compose.prod.yml`
- Critical 5: stop building the web image with `NEXT_PUBLIC_API_URL=http://localhost:8080`

## Implementation Tasks

### P0-1 Fix Frontend Build Blocker

Files:
- `orvixflow-web/next.config.ts`
- frontend Sentry config files if required by the real fix

Action:
- remove or replace the invalid Sentry/Next option causing the production build failure
- keep the change minimal and compatible with the current Next.js/Sentry versions in the repo

Validation:
- run `npm run build` in `orvixflow-web/`

Exit criteria:
- production build succeeds without patching around the error elsewhere

### P0-2 Re-Run Frontend Verification

Files:
- none required unless failures force additional targeted fixes

Action:
- run the normal frontend verification commands after P0-1

Validation:
- `npm run build`
- `npm run lint`
- `npm run test`

Exit criteria:
- all three commands pass, or any remaining failures are documented as separate blockers with evidence

### P0-3 Align Production Image References

Files:
- `.github/workflows/deploy.yml`
- `docker-compose.prod.yml`

Action:
- standardize image naming so the workflow pushes exactly what production compose pulls
- prefer one canonical registry path format across API and web services

Validation:
- manually compare pushed image names to compose image references

Exit criteria:
- no repo-owner vs repo-name mismatch remains

### P0-4 Replace Fake Deploy Success With Real Deploy Flow

Files:
- `.github/workflows/deploy.yml`

Action:
- replace placeholder success output with a real deployment sequence
- include at least pull, restart, and basic verification steps
- fail the workflow if deployment verification fails

Validation:
- workflow file clearly performs real remote deployment actions
- no step claims success before verification

Exit criteria:
- deploy workflow is no longer a stub

### P0-5 Correct Production Web API URL Handling

Files:
- `.github/workflows/deploy.yml`
- `orvixflow-web/Dockerfile` if needed
- `orvixflow-web/next.config.ts` or runtime config surface if needed

Action:
- stop baking `NEXT_PUBLIC_API_URL=http://localhost:8080` into production builds
- use the correct externally reachable production API URL strategy for the current deployment model

Validation:
- deploy workflow no longer hardcodes localhost for the production web image

Exit criteria:
- production web build uses a correct production-facing API base URL strategy

## Risks And Edge Cases

- Sentry config changes can fix the build while silently disabling desired instrumentation; confirm the fix is minimal and explicit.
- Deploy workflow changes must not pretend to verify deployment by printing static output.
- API URL handling must match how the frontend actually reaches the backend in production; do not swap to a new runtime model without evidence.

## Required Tests And Manual Checks

Commands:

```bash
cd orvixflow-web && npm run build
cd orvixflow-web && npm run lint
cd orvixflow-web && npm run test
```

Manual checks:

- inspect `.github/workflows/deploy.yml` for real deployment commands
- inspect `docker-compose.prod.yml` for exact image-name alignment
- confirm no production localhost API URL remains in the web build path

## Evidence Required Before Marking Complete

- successful `npm run build`
- successful `npm run lint`
- successful `npm run test`
- diff evidence showing aligned image references
- deploy workflow evidence showing real remote deploy and verification steps

## Completion Checklist

- [ ] `orvixflow-web/next.config.ts` build blocker fixed
- [ ] frontend build passes
- [ ] frontend lint passes
- [ ] frontend tests pass
- [ ] deploy workflow is no longer a stub
- [ ] image references are aligned
- [ ] production localhost API URL is removed from the deploy path

## Definition Of Done

Phase 0 is complete only when the repo has a truthful frontend verification path and a non-fake deployment path. Code presence alone is not enough.
