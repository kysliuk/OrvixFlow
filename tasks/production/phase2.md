# Phase 2 - Live Provider Validation

> Status: Planned
> Depends on: Phases 0 and 1 complete
> Blocks: Phase 4
> Source of truth: `tasks/production/current-state-audit.md`

## Goal

Validate external integrations with real or production-like providers so the system is operationally proven, not just implemented in code.

## Why This Phase Exists

The audit found several areas where code exists but production readiness is still unproven. This phase turns those areas from “implemented” into “verified.”

## In Scope

- Stripe live-mode validation
- production email delivery validation
- Gmail OAuth validation
- Microsoft OAuth validation
- n8n end-to-end validation
- storage provider validation in production-like conditions

## Out Of Scope

- no broad feature redesigns
- no new providers
- no documentation-only completion claims

## Audit Findings Covered

From `tasks/production/current-state-audit.md`:

- High 3: validate live provider flows for Stripe, email, mailbox OAuth, and n8n
- Current-state matrix items marked partial for billing, production email sending, Inbox Guardian/email workflows, and storage validation

## Implementation Tasks

### P2-1 Validate Stripe Live-Mode Flow

Files:
- billing-related runtime config
- active billing controllers/services as needed for reference

Action:
- validate live-mode keys, webhook handling, and a real subscription flow path

Validation:
- provider-side evidence and application-side evidence both exist

Exit criteria:
- Stripe is proven operational in the intended production flow

### P2-2 Validate Production Email Delivery

Files:
- email provider config surface
- notification processing path as needed for reference

Action:
- prove real auth or notification emails are delivered through the configured provider

Validation:
- real inbox receipt or equivalent provider/dashboard evidence

Exit criteria:
- email sending is operationally verified, not just implemented

### P2-3 Validate Gmail Mailbox OAuth

Files:
- mailbox OAuth config and callback path as needed for reference

Action:
- validate connect, callback, token storage, refresh, and disconnect behavior for Gmail

Validation:
- end-to-end successful flow with evidence

Exit criteria:
- Gmail mailbox OAuth works in the real runtime path

### P2-4 Validate Microsoft Mailbox OAuth

Files:
- mailbox OAuth config and callback path as needed for reference

Action:
- validate connect, callback, token storage, refresh, and disconnect behavior for Microsoft

Validation:
- end-to-end successful flow with evidence

Exit criteria:
- Microsoft mailbox OAuth works in the real runtime path

### P2-5 Validate n8n Provisioning And Workflow Execution

Files:
- n8n-related runtime config
- provisioning and callback paths as needed for reference

Action:
- prove credential provisioning and workflow execution work end-to-end with the current app/runtime wiring

Validation:
- workflow execution evidence plus application-side state updates or logs

Exit criteria:
- n8n is operationally validated, not just wired in code

### P2-6 Validate Storage Provider Behavior

Files:
- storage runtime config
- file upload/download/ingestion surfaces as needed for reference

Action:
- validate file storage behavior in the intended production-like provider/runtime path

Validation:
- successful upload, retrieval, and dependent processing evidence

Exit criteria:
- storage behavior is proven in a production-like environment

## Risks And Edge Cases

- Provider setup can fail for reasons outside code; collect provider-side evidence, not just app logs.
- OAuth validation must treat login identity and mailbox identity as separate consent flows.
- Stripe validation should not be inferred from test-mode behavior.

## Required Tests And Manual Checks

Manual checks are required for this phase. Automated tests alone are insufficient.

Examples:

- provider dashboard confirmation
- successful callback or webhook traces
- successful mailbox connect and disconnect flows
- successful storage upload/retrieval in the target provider path

## Evidence Required Before Marking Complete

- runtime logs or screenshots for each provider flow
- provider dashboard or inbox evidence where applicable
- explicit notes distinguishing real validation from simulated/test-only paths

## Completion Checklist

- [ ] Stripe live flow validated
- [ ] production email delivery validated
- [ ] Gmail mailbox OAuth validated
- [ ] Microsoft mailbox OAuth validated
- [ ] n8n provisioning/execution validated
- [ ] storage provider behavior validated

## Definition Of Done

Phase 2 is complete only when each provider-facing area has operational evidence, not just code coverage or local stubs.
