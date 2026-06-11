# Phase 1 - Production Config Correctness

> Status: Planned
> Depends on: Phase 0 complete
> Blocks: Phases 2 and 4
> Source of truth: `tasks/production/current-state-audit.md`

## Goal

Make production runtime configuration internally consistent with the current codebase.

## Why This Phase Exists

The audit confirmed that `docker-compose.prod.yml` does not fully match what the code actually reads at runtime. A deployment that starts containers successfully can still be operationally broken if required options are missing or misnamed.

This phase exists to eliminate config drift in:

1. AI provider wiring
2. automation key wiring
3. mailbox OAuth configuration
4. virus scan provider defaults
5. other required runtime settings surfaced by option binding

## In Scope

- audit backend option binding against production compose
- add missing runtime env vars
- correct misnamed env vars
- remove unsafe production defaults
- document required runtime secrets/settings in the active production docs if needed

## Out Of Scope

- no live provider validation
- no new infrastructure platform
- no storage architecture redesign
- no speculative config knobs not used by code

## Audit Findings Covered

From `tasks/production/current-state-audit.md`:

- Critical 6: complete `docker-compose.prod.yml` runtime configuration
- Immediate action 5: audit backend option binding against `docker-compose.prod.yml` and add missing env wiring
- Immediate action 6: change unsafe production defaults such as virus scan provider fallback

## Implementation Tasks

### P1-1 Audit Backend Option Binding Against Production Compose

Files:
- `docker-compose.prod.yml`
- `OrvixFlow.Api/Program.cs`
- `OrvixFlow.Infrastructure/DependencyInjection.cs`
- any relevant options classes or service constructors

Action:
- identify every production-required setting the API actually reads
- compare those names to `docker-compose.prod.yml`
- record missing or mismatched variables before editing

Validation:
- every required setting used by code is mapped in production compose or explicitly documented as intentionally external

Exit criteria:
- a complete code-to-compose mapping exists with no unexplained gaps

### P1-2 Add Missing AI Provider Runtime Configuration

Files:
- `docker-compose.prod.yml`

Action:
- add any missing AI provider variables required by the current configured provider path

Validation:
- the compose file contains the AI settings the code will read in production

Exit criteria:
- production compose no longer omits required AI configuration

### P1-3 Fix Automation Key Wiring

Files:
- `docker-compose.prod.yml`

Action:
- align the production env name with the actual code path reading `AutomationKey`

Validation:
- compose uses the same key name the code reads

Exit criteria:
- no `Automation__Key` vs `AutomationKey` mismatch remains for the active runtime path

### P1-4 Add Missing Mailbox OAuth Runtime Configuration

Files:
- `docker-compose.prod.yml`
- any active auth/provider options files if needed for confirmation only

Action:
- wire all env vars required for mailbox OAuth and callback flow in production

Validation:
- production compose exposes the mailbox OAuth settings needed by the current implementation

Exit criteria:
- mailbox OAuth runtime settings are not partially wired

### P1-5 Remove Unsafe Production Defaults

Files:
- `docker-compose.prod.yml`

Action:
- remove or replace unsafe defaults such as `VirusScan__Provider=Noop`
- keep production-safe behavior explicit rather than optional by accident

Validation:
- no unsafe default remains active in the production compose path without deliberate opt-in

Exit criteria:
- production defaults fail closed or point to real required services

## Risks And Edge Cases

- Do not invent new env names if the code already expects existing names.
- Some secrets may be intentionally injected outside compose; distinguish missing mapping from intentionally external secret injection.
- AI provider requirements may differ by selected provider; document which provider assumption the compose file is built around.

## Required Tests And Manual Checks

Commands:

```bash
dotnet build
```

Manual checks:

- compare every changed env var name against code reads
- inspect startup/config paths for any remaining missing required values

## Evidence Required Before Marking Complete

- code-to-compose mapping notes or equivalent diff evidence
- updated production compose with corrected names and required settings
- confirmation that unsafe defaults were removed or replaced

## Completion Checklist

- [ ] required backend options audited against production compose
- [ ] missing AI config added
- [ ] automation key wiring corrected
- [ ] mailbox OAuth runtime config added
- [ ] unsafe production defaults removed

## Definition Of Done

Phase 1 is complete only when production compose matches the settings the code actually reads and no known unsafe runtime defaults remain in the active production path.
