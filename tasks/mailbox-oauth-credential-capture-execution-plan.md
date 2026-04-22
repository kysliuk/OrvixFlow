# Mailbox OAuth Credential Capture - Multi-Session AI Execution Plan

## 1. Executive Summary

This plan covers the secure implementation of Gmail and Microsoft mailbox OAuth credential capture for OrvixFlow, with credentials stored in OrvixFlow encrypted at rest and then used to provision n8n mailbox credentials/workflows.

This is a separate architecture track from outbound transactional email delivery.

Current project reality:

- OrvixFlow already supports Google and Microsoft sign-in through NextAuth
- `MailboxConnection` already exists as a tenant-scoped mailbox integration record
- `N8nProvisioningService.CreateCredentialAsync()` already maps Gmail and Outlook provider types to n8n credential types
- current mailbox provisioning passes empty credential data
- current OAuth account provisioning sends only identity data to backend and does not persist provider access/refresh tokens

The major constraint is security: mailbox OAuth credential capture must not weaken the existing auth/account-linking model, must not auto-link local and OAuth identities by email, and must store provider credentials encrypted at rest.

This track assumes the following product decisions are already approved:

- Gmail and Microsoft mailbox OAuth capture are in scope
- Microsoft support applies to both outbound email ecosystem planning and mailbox integration
- mailbox credentials will be stored in OrvixFlow
- credentials must be encrypted at rest

## 2. Project Context Relevant To This Work

### Existing OAuth/Auth Surface

- Frontend auth:
  - `orvixflow-web/auth.ts`
  - Google and Microsoft Entra ID providers are configured
- Backend OAuth provisioning:
  - `OrvixFlow.Api/Controllers/AuthController.cs`
  - `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- Current OAuth provisioning behavior:
  - provisions or reuses user account identity
  - does not persist provider tokens/scopes/expiry

### Existing Mailbox / n8n Surface

- Mailbox entity:
  - `OrvixFlow.Core/Entities/MailboxConnection.cs`
- Mailbox controller:
  - `OrvixFlow.Api/Controllers/MailboxConnectionsController.cs`
- n8n provisioning service:
  - `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs`
- Current behavior:
  - mailbox connection stores provider/email/credential/workflow ids
  - provisioning job may create n8n credential id
  - actual provider credential payload is currently empty

### Tenant / Security Constraints

- Tenant isolation is strict and enforced by query filters
- JWT claims drive tenant resolution
- Existing auth rules intentionally prevent unsafe local/OAuth auto-linking by matching email only
- Secrets must come from env/config, not committed files

### Missing Security Infrastructure To Design And Build

- encrypted-at-rest mailbox provider credential storage
- token refresh lifecycle handling
- consent boundaries between login identity and mailbox connection identity
- explicit reconnect/revoke flow
- provider scope model

## 3. Goals

1. Add a secure mailbox-provider connection flow for Gmail and Microsoft.
2. Capture required OAuth credentials for mailbox integration.
3. Store provider credentials in OrvixFlow encrypted at rest.
4. Keep mailbox connection concerns distinct from base login identity concerns.
5. Feed real credential data into n8n credential provisioning.
6. Preserve tenant isolation and current auth-account-linking protections.
7. Leave a sessionized implementation path another AI agent can execute safely.

## 4. Defined Vs Underdefined

### Defined

- Gmail and Microsoft mailbox OAuth capture are required
- credentials must be stored in OrvixFlow
- storage must be encrypted at rest
- resulting credentials are intended for future and current n8n use

### Still Underdefined And Must Be Resolved During Execution

- exact encryption mechanism and key source
- whether tokens live in `MailboxConnection` or a separate credential entity
- which provider scopes are required for Gmail and Microsoft mailbox workflows
- how refresh tokens are rotated and persisted
- whether mailbox connect flow reuses login OAuth session or always performs a separate consent flow
- how to handle mismatch between signed-in user email and mailbox email being connected
- whether multiple mailbox connections per user/tenant can share the same provider identity

### Recommended Defaults For This Track

- Use a separate credential entity rather than storing tokens directly on `MailboxConnection`
- Encrypt token payloads with a dedicated application key from env/config
- Use explicit mailbox-connect consent flow in inbox settings instead of silently harvesting login OAuth tokens
- Persist provider, subject/account identifier, scopes, expiry, access token, refresh token, and refresh metadata
- Treat mailbox disconnection as both application unlink and optional n8n credential cleanup

## 5. Critical Risks

### Technical Risks

- NextAuth login flow may not expose everything needed in a safe reusable way for mailbox-linking
- Provider token refresh handling can become brittle if not modeled explicitly
- n8n credential payload expectations may differ by provider type and version
- encrypted token storage design may require a reusable secure-secret abstraction

### Security Risks

- Refresh tokens are high-sensitivity credentials
- Auto-linking by email could break current account safety rules
- Returning provider tokens to frontend code or API responses would be a severe leak
- Poor encryption-key management would nullify at-rest protection

### Product / UX Risks

- Users may assume login with Google/Microsoft automatically connects their mailbox
- Consent flow may need to distinguish:
  - app login identity
  - mailbox integration identity
- Microsoft naming mismatch exists already:
  - NextAuth uses `microsoft-entra-id`
  - n8n provisioning maps `Outlook` / Microsoft credential types

## 6. No-Break Rules

1. Do not auto-link local and OAuth accounts by matching email.
2. Do not assume a user's login provider identity is automatically approved for mailbox integration.
3. Do not store refresh tokens unencrypted.
4. Do not return provider tokens in API responses.
5. Do not broaden tenant access rules for mailbox connections.
6. Do not let frontend browser code become the long-term storage layer for tokens.
7. Do not break existing sign-in behavior for current users.

## 7. Master Execution Strategy

### Recommended Session Order

1. Architecture/security design finalization
2. Secure credential storage groundwork
3. Backend mailbox OAuth link/reconnect APIs
4. Frontend explicit connect flow
5. n8n credential provisioning integration
6. Validation, docs, and cleanup

### Re-Read Checkpoints

- Before Session 1:
  - `memory/memory-security.md`
  - `memory/auth.md`
  - `memory/memory-risks.md`
- Before Session 2:
  - `MailboxConnectionsController`
  - `N8nProvisioningService`
  - `orvixflow-web/auth.ts`
- Before Session 4:
  - `orvixflow-web/AGENTS.md`
  - relevant Next.js 16 docs under `node_modules/next/dist/docs/`

## 8. Session-By-Session Execution Plan

---

## Session 1 - Finalize Secure Mailbox OAuth Design

### Size

- Medium
- Fits the budget because it is a focused design-and-verification session with no broad implementation churn.

### Goal

Lock down the mailbox OAuth credential architecture so subsequent sessions do not guess.

### Exact Scope

- Define entity boundaries
- Define encryption approach
- Define provider-scope model
- Define connect/reconnect/disconnect UX and API boundaries
- Define how login OAuth and mailbox OAuth relate without conflating them

### Why This Is Its Own Session

- This track is security-sensitive and underdefined
- Wrong assumptions here would create rework or dangerous shortcuts later

### Prerequisites

- Re-read:
  - `memory/memory-security.md`
  - `memory/auth.md`
  - `OrvixFlow.Api/Controllers/MailboxConnectionsController.cs`
  - `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs`
  - `orvixflow-web/auth.ts`

### Files / Components Likely Involved

- design notes only, or minimal task doc amendments if needed

### Implementation Tasks

1. Decide whether mailbox-linking always requires explicit connect action from inbox settings.
2. Decide whether current login session tokens can be reused temporarily during linking or whether a dedicated OAuth link handshake is required.
3. Define the credential persistence model.
4. Define the encryption boundary and required env key(s).
5. Define provider metadata to persist.
6. Define disconnect/reconnect semantics.
7. Define how n8n credential provisioning consumes decrypted provider data.
8. Define role/authorization rules for who can connect a mailbox inside a tenant.

### Architecture Constraints

- Keep login authentication separate from mailbox integration authorization
- Prefer a dedicated mailbox credential aggregate over expanding `MailboxConnection` into a secret store

### Security Concerns

- Must not rely on implicit email matching
- Must define key management before token persistence work begins

### Tests To Add / Update

- None yet unless design artifacts are committed

### Validation Checklist

- token ownership model is explicit
- encryption-at-rest approach is explicit
- provider scope list is explicit
- connect/reconnect/disconnect path is explicit
- no unsafe auto-link shortcuts remain

### Definition Of Done

- Design is explicit enough that a fresh agent can implement without guessing

### Handoff Notes For Next Session

- Next session should implement secure persistence first, not frontend UX first

---

## Session 2 - Secure Credential Storage Groundwork

### Size

- Heavy
- Fits the budget because it is bounded to backend domain/infrastructure/persistence work, but likely includes schema changes and crypto handling.

### Goal

Create the encrypted-at-rest credential storage layer for mailbox OAuth tokens.

### Exact Scope

- Add credential entity/model
- Add encryption/decryption service
- Add config/options for encryption key material
- Add DB migration
- Add service tests for encryption and persistence

### Why This Is Its Own Session

- Secure persistence is foundational for every later step
- It is too security-sensitive to mix with frontend/OAuth callback changes

### Prerequisites

- Session 1 complete
- Re-read decided design notes and `memory-security.md`

### Files / Components Likely Involved

- `OrvixFlow.Core/Entities/` new mailbox credential entity
- `OrvixFlow.Infrastructure/` new encryption/storage services
- `OrvixFlow.Infrastructure/Data/AppDbContext.cs`
- new migration under `OrvixFlow.Infrastructure/Migrations/`
- config files for env variable documentation
- tests under `OrvixFlow.Tests/`

### Implementation Tasks

1. Add a dedicated entity for mailbox OAuth credentials.
2. Define tenant and ownership relationships explicitly.
3. Add encrypted fields or encrypted payload storage.
4. Implement a crypto service using application-managed key material from env/config.
5. Ensure decrypted values are only available inside server-side services.
6. Add migration and update model snapshot.
7. Document required env vars in `.env.example` and local `.env` placeholders.
8. Add tests for:
   - round-trip encryption
   - missing key behavior
   - tenant scoping
   - non-exposure in DTOs

### Architecture Constraints

- Prefer composable secret-handling service rather than controller-level encryption logic
- Avoid tying credential storage too tightly to n8n-specific details

### Security Concerns

- Encryption key must never be committed
- Logging must not print plaintext token fields
- Tests must use fake tokens only

### Tests To Add / Update

- crypto service tests
- persistence/retrieval tests
- entity mapping tests if needed

### Validation Checklist

- refresh token is stored encrypted
- decrypted token is accessible only via server services
- migration applies cleanly
- no API shape exposes secret values

### Definition Of Done

- OrvixFlow can securely persist mailbox OAuth credentials encrypted at rest

### Handoff Notes For Next Session

- Next session should expose backend APIs for connect/reconnect using this storage layer

---

## Session 3 - Backend Mailbox OAuth Link And Reconnect APIs

### Size

- Heavy
- Fits the budget because it is backend-only flow work, but it spans auth boundaries, mailbox ownership, and provider token lifecycle handling.

### Goal

Implement backend endpoints/services that create, update, revoke, and use mailbox OAuth credentials safely.

### Exact Scope

- Add mailbox connect/reconnect/disconnect backend endpoints and services
- Add token refresh handling model
- Link mailbox connection records to secure credential records
- Keep DTOs secret-free

### Why This Is Its Own Session

- This is the core application behavior change and should be validated independently before UI work

### Prerequisites

- Session 2 complete
- Re-read:
  - `MailboxConnectionsController`
  - `AuthController`
  - `AuthService`
  - secure credential storage code added in Session 2

### Files / Components Likely Involved

- `OrvixFlow.Api/Controllers/MailboxConnectionsController.cs`
- new backend services/interfaces for mailbox OAuth orchestration
- possibly auth callback-related backend endpoints
- tests under `OrvixFlow.Tests/`

### Implementation Tasks

1. Define API contract for mailbox connect flow.
2. Add endpoint/service for storing provider credential result.
3. Add endpoint/service for reconnect/refresh.
4. Add endpoint/service for disconnect and credential cleanup.
5. Link stored credentials to `MailboxConnection` safely.
6. Add authorization checks for tenant and acting user.
7. Add support for provider metadata normalization:
   - Google/Gmail naming
   - Microsoft/Outlook/Entra naming
8. Ensure no credential values leak via controller responses.

### Architecture Constraints

- Avoid forcing mailbox OAuth into existing base login endpoints if a dedicated mailbox flow is clearer
- Keep n8n provisioning decoupled enough to retrigger if needed

### Security Concerns

- Only authorized tenant users should link/disconnect mailboxes
- Reconnect flow must not bypass consent rules
- Provider refresh logic must not leak token values in errors

### Tests To Add / Update

- controller/service tests for connect/reconnect/disconnect
- tenant isolation tests
- DTO redaction/non-exposure tests
- token refresh metadata update tests

### Validation Checklist

- mailbox credential record can be created and linked
- reconnect updates credentials correctly
- disconnect removes or invalidates credentials safely
- API responses do not expose secrets

### Definition Of Done

- Backend mailbox credential lifecycle is implemented safely

### Handoff Notes For Next Session

- Next session should add explicit frontend UX, not change backend data model unless gaps are found

---

## Session 4 - Frontend Explicit Provider Connect Flow

### Size

- Heavy
- Fits the budget because it is one bounded UX flow, but it touches NextAuth behavior and mailbox settings UI, which are context-heavy.

### Goal

Let users explicitly connect Gmail and Microsoft mailboxes from the inbox settings UI in a way that aligns with backend security constraints.

### Exact Scope

- Add connect buttons/flow in inbox settings
- Add provider-specific status UX
- Coordinate frontend auth/provider flow with backend mailbox-link endpoints
- Preserve existing sign-in behavior

### Why This Is Its Own Session

- Frontend auth behavior is high-context and must be isolated
- It is easy to introduce accidental auth regressions if mixed with backend changes

### Prerequisites

- Session 3 complete
- Re-read:
  - `orvixflow-web/AGENTS.md`
  - relevant Next.js docs
  - `orvixflow-web/auth.ts`
  - `orvixflow-web/app/(dashboard)/settings/inbox/page.tsx`

### Files / Components Likely Involved

- `orvixflow-web/auth.ts`
- `orvixflow-web/app/(dashboard)/settings/inbox/page.tsx`
- related frontend helper/state files as needed
- backend endpoints from Session 3

### Implementation Tasks

1. Add explicit UI actions for `Connect Gmail` and `Connect Microsoft`.
2. Decide how mailbox-link auth state is carried through callback flow.
3. Ensure the flow distinguishes mailbox linking from primary login.
4. Add connection-state UX:
   - connected
   - reconnect required
   - provisioning in progress
   - failed
5. Add disconnect/reconnect actions.
6. Keep API token/session handling intact for existing login flow.

### Architecture Constraints

- Frontend should orchestrate, not become the secret store
- Keep mailbox integration UX inside existing inbox settings surface unless a dedicated route is clearly required

### Security Concerns

- Avoid exposing provider tokens to browser state any longer than absolutely necessary
- Preserve current account-linking protections

### Tests To Add / Update

- frontend component tests for connect/disconnect/reconnect state
- any auth-flow tests that can reasonably cover mailbox-link callback handling

### Validation Checklist

- existing Google/Microsoft sign-in for app access still works
- mailbox connection can be initiated intentionally from settings
- connection state updates correctly in UI
- secret values are never rendered or returned

### Definition Of Done

- Frontend mailbox-link flow exists and works against backend endpoints

### Handoff Notes For Next Session

- Next session must complete n8n provisioning with real credentials and then validate the full path

---

## Session 5 - n8n Credential Provisioning Integration

### Size

- Medium
- Fits the budget because it is focused on bridging already-captured credentials into n8n provisioning and validating provider payload shape.

### Goal

Use stored mailbox OAuth credentials to provision actual n8n credentials/workflows for Gmail and Microsoft mailboxes.

### Exact Scope

- feed real credential data to `N8nProvisioningService.CreateCredentialAsync()`
- normalize provider mapping
- ensure mailbox activation depends on valid provisioning outcome

### Why This Is Its Own Session

- n8n integration is its own external-boundary risk and should be tested separately from credential capture itself

### Prerequisites

- Session 4 complete
- Re-read `N8nProvisioningService.cs` and mailbox provisioning paths

### Files / Components Likely Involved

- `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs`
- `MailboxConnectionsController.cs`
- any new orchestration services created earlier
- tests

### Implementation Tasks

1. Define provider payload builders for Gmail and Microsoft.
2. Feed decrypted credential values only at the provisioning boundary.
3. Normalize provider naming between frontend/backend/n8n mappings.
4. Ensure provisioning failures leave the mailbox inactive and diagnosable.
5. Ensure disconnect cleans up n8n credential/workflow ids safely.
6. Add tests for provider payload construction and failure handling.

### Architecture Constraints

- Keep n8n-specific payload logic localized
- Do not turn n8n provisioning service into the credential source of truth

### Security Concerns

- Decrypted provider secrets should exist only in-memory at provisioning time
- Error paths must not serialize secret payloads

### Tests To Add / Update

- n8n payload mapping tests
- provisioning success/failure tests
- mailbox activation state tests

### Validation Checklist

- Gmail credentials can be provisioned into n8n
- Microsoft credentials can be provisioned into n8n
- provisioning failures do not mark mailbox active
- disconnect cleans external references safely

### Definition Of Done

- End-to-end mailbox credential capture to n8n provisioning path is implemented

### Handoff Notes For Next Session

- Final session should focus on validation, docs, and memory update only

---

## Session 6 - Validation, Documentation, And Memory Update

### Size

- Medium
- Fits the budget because it is validation and documentation work with only targeted defect fixes.

### Goal

Verify the mailbox OAuth track end-to-end and leave durable guidance for future agents and maintainers.

### Exact Scope

- run backend/frontend tests
- validate Gmail and Microsoft connect flows
- update docs and memory if the implementation materially changed architecture

### Why This Is Its Own Session

- Keeps final verification explicit
- Prevents open-ended scope growth after secure implementation is already complete

### Prerequisites

- Sessions 1-5 complete

### Files / Components Likely Involved

- touched backend/frontend tests
- `.env.example`
- `.env`
- `memory/` docs if applicable

### Implementation Tasks

1. Run targeted backend tests and frontend tests.
2. Validate Gmail mailbox connect flow end-to-end.
3. Validate Microsoft mailbox connect flow end-to-end.
4. Validate reconnect and disconnect.
5. Validate tenant isolation across mailbox operations.
6. Update docs and memory files if architecture/security/ops behavior changed materially.

### Architecture Constraints

- Only fix discovered defects
- Avoid new feature additions here

### Security Concerns

- Use sandbox/test tenants and test provider apps
- Never store real production refresh tokens in repo-managed files

### Tests To Add / Update

- any gap-closing tests discovered during end-to-end validation

### Validation Checklist

- Gmail connect works
- Microsoft connect works
- reconnect works
- disconnect works
- n8n provisioning receives valid provider data
- credential APIs remain secret-free
- tenant isolation holds

### Definition Of Done

- Mailbox OAuth credential capture track is complete, validated, and documented

## 9. Stop Conditions For The Execution Agent

The agent must stop and ask for clarification if any of the following occur:

- mailbox OAuth requires changing core auth account-linking policy
- secure credential storage needs a broader platform-wide secret vault design not yet present
- required provider scopes imply a materially different product consent story than expected
- n8n credential API shape differs enough that provider payload assumptions are invalid
- current NextAuth setup cannot safely support the intended mailbox-link callback flow without larger auth redesign

## 10. Final Acceptance Criteria

- Gmail and Microsoft mailbox connect flows exist
- credentials are stored in OrvixFlow encrypted at rest
- credentials are not exposed in API responses or logs
- mailbox connections remain tenant-safe and authorization-safe
- n8n credential creation uses real provider data instead of empty payloads
- reconnect/disconnect flows work
- existing sign-in behavior still works
- `.env.example` and local `.env` document required config surface
- relevant backend/frontend tests pass
