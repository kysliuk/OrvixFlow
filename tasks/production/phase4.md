# Phase 4 - Final Production Proof And Operations Validation

> Status: Planned
> Depends on: Phases 0, 1, 2, and 3 complete
> Blocks: production signoff
> Source of truth: `tasks/production/current-state-audit.md`

## Goal

Prove that the system is operationally ready for production use.

## Why This Phase Exists

Even after implementation and provider validation, the project should not be called production-ready until deployment, health, backup, restore, and observability paths are demonstrated end-to-end.

## In Scope

- full deployment validation
- health check validation
- backup and restore proof
- observability sink validation
- final production readiness signoff checklist

## Out Of Scope

- no new product features
- no speculative platform migration

## Audit Findings Covered

From `tasks/production/current-state-audit.md`:

- current-state matrix items still marked broken or partial in CI/CD, production operations, and observability
- final recommendation to complete release, runtime, and operational truth before claiming readiness

## Implementation Tasks

### P4-1 Validate Full Deployment Path

Files:
- `.github/workflows/deploy.yml`
- `docker-compose.prod.yml`
- runbooks if applicable

Action:
- execute or simulate the full intended deployment path closely enough to prove it works

Validation:
- deployment completes and services start in the intended configuration

Exit criteria:
- deployment path is operationally proven

### P4-2 Validate Health Checks And Startup Readiness

Files:
- health check surfaces and production compose as needed for reference

Action:
- verify service readiness checks and operational health endpoints

Validation:
- health endpoints respond correctly after deployment

Exit criteria:
- health checks are meaningful and passing

### P4-3 Validate Backup And Restore

Files:
- backup scripts
- restore runbooks

Action:
- prove backups can be created and restored successfully

Validation:
- restore evidence exists, not just backup script existence

Exit criteria:
- backup and restore are operationally proven

### P4-4 Validate Observability Sinks

Files:
- API and frontend observability config
- any sink/runtime config involved

Action:
- confirm logs, traces, metrics, and error reporting reach their intended sinks

Validation:
- sink-side evidence exists for at least one controlled event or request path

Exit criteria:
- observability is not just configured, but actually flowing

### P4-5 Complete Final Readiness Signoff

Files:
- `tasks/production/progress.md`
- `tasks/production/current-state-audit.md` if final status changes materially

Action:
- complete a final release checklist grounded in evidence from all earlier phases

Validation:
- no blocker remains open without explicit acceptance

Exit criteria:
- production readiness can be stated with evidence, limitations, and known residual risks

## Risks And Edge Cases

- A green deploy workflow is not enough if restore and observability are unproven.
- Health checks that only test process liveness are weaker than checks that verify dependencies.

## Required Tests And Manual Checks

Manual checks:

- successful deploy evidence
- health endpoint evidence
- backup creation evidence
- restore evidence
- observability sink evidence

## Evidence Required Before Marking Complete

- deployment logs or equivalent proof
- health check results
- backup and restore proof
- observability proof
- final checklist with any residual risks documented

## Completion Checklist

- [ ] deployment path validated
- [ ] health checks validated
- [ ] backup validated
- [ ] restore validated
- [ ] observability sinks validated
- [ ] final signoff checklist completed

## Definition Of Done

Phase 4 is complete only when operational readiness is proven end-to-end and supported by explicit evidence, not inferred from configuration or source code alone.
