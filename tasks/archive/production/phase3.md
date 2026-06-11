# Phase 3 — Mailbox OAuth Credential Capture

> **Obsolete / Historical Plan**
> Superseded by `tasks/production/current-state-audit.md`, `tasks/production/overview.md`, and `tasks/production/progress.md` on 2026-06-11.
> Core implementation from this plan has landed, but this document's status and dependency framing are no longer authoritative.

> **Status:** Not Started  
> **Estimated effort:** 4–6 weeks (6 implementation sessions)  
> **Dependencies:** Phase 0 (n8n secured), Phase 1 (email working)  
> **Blocks:** Full Inbox Guardian self-service provisioning  
> **Reference:** `tasks/mailbox-oauth-credential-capture-execution-plan.md` (full 6-session plan)

---

## Goal

Enable OrvixFlow to capture Gmail and Microsoft OAuth credentials from users, store them encrypted at rest, and use them to provision real n8n credentials/workflows — replacing the current empty-payload provisioning that silently fails.

---

## Why

Currently, when a user connects a mailbox, `N8nProvisioningService.CreateCredentialAsync()` is called with an **empty credential payload**. n8n workflows therefore cannot authenticate with the provider and cannot process any emails. The Inbox Guardian is non-functional for real mailboxes without manually pre-configured n8n credentials.

This phase implements the complete mailbox OAuth credential lifecycle:
1. User explicitly connects their Gmail or Microsoft mailbox from inbox settings
2. OrvixFlow captures the OAuth access/refresh tokens from the provider
3. Tokens are stored in OrvixFlow encrypted at rest (AES-256-GCM)
4. When n8n credential provisioning runs, it receives decrypted provider data
5. n8n workflows can authenticate with Gmail/Microsoft and process real emails

---

## Scope

- `MailboxCredential` entity (new) — encrypted token storage
- `IMailboxCredentialService` and implementation — encrypt/decrypt/store/retrieve
- Backend OAuth link/reconnect/disconnect endpoints
- Frontend connect flow in inbox settings UI
- n8n credential provisioning with real provider data

---

## Out of Scope

- Do NOT auto-link OAuth login identity with mailbox credential
- Do NOT reuse login OAuth session tokens for mailbox credentials — they are separate consent flows
- Do NOT change existing `AuthService`, `ProvisionOAuthUserAsync`, or the login flow
- Do NOT modify the existing `MailboxConnection` entity structure beyond adding a `CredentialId` FK
- Do NOT change `NotificationProcessorJob`, `AuthController`, or email delivery code
- Do NOT change `InboxGuardianService` logic — it already calls through `MailboxConnection`

---

## Dependencies

- **Phase 0** — n8n must be secured (authenticated) before provisioning real credentials to it
- **Phase 1** — email delivery must work for credential-linked notification emails (e.g., "Your mailbox connection needs to be renewed")
- **Existing components:**
  - `OrvixFlow.Api/Controllers/MailboxConnectionsController.cs` — extend, do not replace
  - `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs` — extend `CreateCredentialAsync`
  - `orvixflow-web/app/(dashboard)/settings/inbox/page.tsx` — add connect UI here
  - `orvixflow-web/auth.ts` — read before any auth.ts changes

---

## Files / Components Likely Involved

| File | Change |
|---|---|
| `OrvixFlow.Core/Entities/MailboxCredential.cs` | NEW — encrypted token entity |
| `OrvixFlow.Core/Interfaces/IMailboxCredentialService.cs` | NEW — service interface |
| `OrvixFlow.Core/Entities/MailboxConnection.cs` | MODIFY — add `CredentialId` FK (nullable) |
| `OrvixFlow.Infrastructure/Services/MailboxCredentialService.cs` | NEW — AES-256-GCM encrypt/decrypt |
| `OrvixFlow.Infrastructure/Services/MailboxCredentialEncryptionService.cs` | NEW — crypto helper |
| `OrvixFlow.Infrastructure/Data/AppDbContext.cs` | MODIFY — add DbSet<MailboxCredential> |
| `OrvixFlow.Infrastructure/Migrations/` | NEW — migration for MailboxCredential |
| `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs` | MODIFY — feed real credential data |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | MODIFY — register new services |
| `OrvixFlow.Api/Controllers/MailboxConnectionsController.cs` | MODIFY — add connect/reconnect/disconnect endpoints |
| `orvixflow-web/app/(dashboard)/settings/inbox/page.tsx` | MODIFY — add connect UI |
| `orvixflow-web/auth.ts` | READ ONLY — do not modify unless absolutely necessary |
| `.env.example` | MODIFY — add `MAILBOX_CREDENTIAL_ENCRYPTION_KEY` |

---

## Architecture Decisions (Locked — Do Not Re-Analyze These)

These decisions were made during the audit phase. Do not re-litigate them.

| Decision | Choice | Rationale |
|---|---|---|
| Encryption algorithm | AES-256-GCM | Industry standard; provides confidentiality + integrity |
| Key source | `MAILBOX_CREDENTIAL_ENCRYPTION_KEY` from env | Never committed; rotatable |
| Token storage | Separate `MailboxCredential` entity | Separation of concerns; `MailboxConnection` stays clean |
| MailboxConnection link | `MailboxConnection.CredentialId` → FK to `MailboxCredential.Id` (nullable) | Existing connections remain valid; null = no OAuth credential |
| Gmail OAuth scope | `https://mail.google.com/` | Full mailbox access (IMAP) as required by n8n Gmail credential |
| Microsoft OAuth scope | `https://outlook.office.com/IMAP.AccessAsUser.All` | Delegated IMAP access as required by n8n Microsoft credential |
| Connect flow | Explicit UI action in inbox settings, separate from login | Never reuse login tokens; never auto-link |
| Disconnect | Delete `MailboxCredential`, clear `MailboxConnection.CredentialId`, call n8n cleanup | Complete cleanup |

---

## Implementation Tasks (by Session)

### Session 1 — Design Finalization

> **Read before starting:** `memory/memory-security.md`, `memory/auth.md`, `memory/memory-risks.md`, `OrvixFlow.Api/Controllers/MailboxConnectionsController.cs`, `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs`, `orvixflow-web/auth.ts`

- [ ] Verify the architecture decisions above against the current codebase (do any need adjusting?)
- [ ] Define provider scope lists:
  - Gmail: `https://mail.google.com/ email profile`
  - Microsoft: `https://outlook.office.com/IMAP.AccessAsUser.All offline_access email profile`
- [ ] Define `MailboxCredential` entity shape (see below)
- [ ] Define the connect/reconnect/disconnect API contract
- [ ] Define how the frontend initiates the OAuth flow (redirect to provider → callback → OrvixFlow stores tokens)
- [ ] Confirm that n8n Gmail and Microsoft credential payload shapes are correct (check n8n docs or existing `N8nProvisioningService.cs` credential type mappings)
- [ ] Document any gaps found in a `sessions/s1-design-notes.md` inside this phase folder (optional)

**`MailboxCredential` entity shape:**
```csharp
public class MailboxCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }          // EF query filter
    public Guid MailboxConnectionId { get; set; }
    public string Provider { get; set; } = string.Empty;  // "Gmail" | "Microsoft"
    public string ProviderAccountId { get; set; } = string.Empty; // subject claim from provider
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime TokenExpiresAtUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public MailboxConnection MailboxConnection { get; set; } = null!;
}
```

### Session 2 — Encrypted Credential Storage

> **Read before starting:** Session 1 design notes, `memory/memory-security.md`

- [ ] Create `OrvixFlow.Core/Entities/MailboxCredential.cs` (entity as designed above)
- [ ] Create `OrvixFlow.Core/Interfaces/IMailboxCredentialService.cs`:
  ```csharp
  public interface IMailboxCredentialService
  {
      Task<MailboxCredential> StoreCredentialAsync(Guid tenantId, Guid mailboxConnectionId, 
          string provider, string providerAccountId, string accessToken, 
          string refreshToken, IEnumerable<string> scopes, DateTime expiresAt);
      Task<(string accessToken, string refreshToken)?> GetDecryptedTokensAsync(Guid credentialId);
      Task UpdateTokensAsync(Guid credentialId, string accessToken, string refreshToken, DateTime expiresAt);
      Task DeleteCredentialAsync(Guid credentialId);
  }
  ```
- [ ] Create `OrvixFlow.Infrastructure/Services/MailboxCredentialEncryptionService.cs`:
  - Constructor: reads `MAILBOX_CREDENTIAL_ENCRYPTION_KEY` from config; throws if missing
  - `Encrypt(string plaintext)` → base64-encoded IV + GCM ciphertext + auth tag
  - `Decrypt(string ciphertext)` → original plaintext
  - Key derivation: treat env var as base64-encoded 32-byte key (or use PBKDF2 if var is a passphrase)
  - Throw `InvalidOperationException` if key is missing or malformed
- [ ] Create `OrvixFlow.Infrastructure/Services/MailboxCredentialService.cs` (implements `IMailboxCredentialService`)
  - Uses `MailboxCredentialEncryptionService` internally
  - Never exposes plaintext tokens in return types except through `GetDecryptedTokensAsync`
- [ ] Modify `OrvixFlow.Core/Entities/MailboxConnection.cs`: add `CredentialId Guid? CredentialId { get; set; }`
- [ ] Modify `OrvixFlow.Infrastructure/Data/AppDbContext.cs`: add `DbSet<MailboxCredential> MailboxCredentials { get; set; }` and apply EF global query filter with `TenantId`
- [ ] Create EF Core migration: `dotnet ef migrations add AddMailboxCredential --project OrvixFlow.Infrastructure --startup-project OrvixFlow.Api`
- [ ] Register in `DependencyInjection.cs`: `services.AddScoped<IMailboxCredentialService, MailboxCredentialService>()`; `services.AddSingleton<MailboxCredentialEncryptionService>()`
- [ ] Add to `.env.example`:
  ```
  # Mailbox OAuth Credential Encryption Key (REQUIRED if using Inbox Guardian)
  # Must be a base64-encoded 32-byte (256-bit) random key.
  # Generate with: openssl rand -base64 32
  MAILBOX_CREDENTIAL_ENCRYPTION_KEY=REPLACE-WITH-BASE64-32-BYTE-RANDOM-KEY
  ```
- [ ] Add tests (`OrvixFlow.Tests/MailboxCredentialEncryptionServiceTests.cs`):
  - Round-trip encrypt/decrypt returns original value
  - Different IVs produce different ciphertexts for same plaintext
  - Missing key throws `InvalidOperationException`
  - Tampered ciphertext throws on decrypt
- [ ] Add tests (`OrvixFlow.Tests/MailboxCredentialServiceTests.cs`):
  - Store and retrieve: decrypted tokens match originals
  - Stored entity has encrypted (not plaintext) token fields
  - Delete removes credential and clears `MailboxConnection.CredentialId`
  - Tenant isolation: cannot retrieve another tenant's credential

### Session 3 — Backend OAuth Link/Reconnect/Disconnect APIs

> **Read before starting:** `MailboxConnectionsController.cs`, `AuthService.cs` (for OAuth patterns), Session 2 code

- [ ] Add `POST /api/mailbox/{connectionId}/credential/authorize` — initiates OAuth flow, returns provider authorization URL
  - Validates: caller is tenant-scoped CompanyAdmin+ or the specific user whose mailbox it is
  - Generates OAuth state parameter (CSRF protection) — store in server-side cache or short-lived DB record
  - Returns: `{ authorizationUrl: "https://accounts.google.com/o/oauth2/auth?..." }`
- [ ] Add `POST /api/mailbox/{connectionId}/credential/callback` — receives authorization code, exchanges for tokens, stores credential
  - Validates state parameter (CSRF check)
  - Exchanges code for access+refresh tokens via provider token endpoint
  - Calls `IMailboxCredentialService.StoreCredentialAsync`
  - Updates `MailboxConnection.CredentialId`
  - Does NOT return token values in response
- [ ] Add `POST /api/mailbox/{connectionId}/credential/refresh` — forces token refresh
  - For cases where the access token is near expiry or has expired
  - Uses stored refresh token to get new access token
  - Updates credential via `IMailboxCredentialService.UpdateTokensAsync`
- [ ] Add `DELETE /api/mailbox/{connectionId}/credential` — disconnect/revoke
  - Calls provider revocation endpoint (best effort — don't fail if provider revocation fails)
  - Calls `IMailboxCredentialService.DeleteCredentialAsync`
  - Clears `MailboxConnection.CredentialId`
  - Does NOT delete the `MailboxConnection` itself (user may reconnect)
- [ ] Authorization rule: all credential endpoints require CompanyAdmin+ OR the specific user whose mailbox it is (read `MailboxConnection.UserId`)
- [ ] Ensure no credential values appear in any API response body
- [ ] Add tests:
  - Connect stores credential and returns no tokens
  - Reconnect updates credential
  - Disconnect removes credential, leaves MailboxConnection
  - Tenant isolation: cannot access another tenant's credential endpoints
  - CSRF: callback with wrong state returns 400

### Session 4 — Frontend Connect Flow

> **Read before starting:** `orvixflow-web/AGENTS.md`, `orvixflow-web/app/(dashboard)/settings/inbox/page.tsx`, `orvixflow-web/auth.ts`, relevant Next.js docs under `node_modules/next/dist/docs/`

- [ ] In `orvixflow-web/app/(dashboard)/settings/inbox/page.tsx`, add connection state UI per mailbox:
  - Connected: show provider icon + email + "Disconnect" button
  - Reconnect required: show warning + "Reconnect" button (when credential is expired/revoked)
  - Not connected: show "Connect Gmail" / "Connect Microsoft" buttons
  - Provisioning in progress: spinner
- [ ] Implement connect flow:
  1. User clicks "Connect Gmail"
  2. Frontend calls `POST /api/mailbox/{connectionId}/credential/authorize`
  3. Redirect user to `authorizationUrl` from response
  4. Provider redirects back to a new page: `orvixflow-web/app/mailbox-callback/page.tsx`
  5. Callback page extracts `code` and `state` from URL params
  6. Calls `POST /api/mailbox/{connectionId}/credential/callback`
  7. On success, redirect to inbox settings with success message
- [ ] Implement disconnect flow:
  1. User clicks "Disconnect"
  2. Confirm dialog
  3. Calls `DELETE /api/mailbox/{connectionId}/credential`
  4. UI updates to "Not connected" state
- [ ] Implement reconnect flow (same as connect, but for existing connection)
- [ ] No provider tokens should be stored in frontend state or localStorage — only connection status
- [ ] Add Vitest tests for connection state UI components

### Session 5 — n8n Credential Provisioning with Real Provider Data

> **Read before starting:** `OrvixFlow.Infrastructure/Ai/N8nProvisioningService.cs`, Session 3 code

- [ ] In `N8nProvisioningService.CreateCredentialAsync`, replace empty payload with decrypted provider data:
  ```csharp
  // Before calling n8n, retrieve decrypted tokens
  var tokens = await _credentialService.GetDecryptedTokensAsync(mailboxConnection.CredentialId.Value);
  if (tokens == null) throw new InvalidOperationException("No credential available for mailbox connection");
  
  // Build provider-specific payload
  var credentialData = provider switch
  {
      "Gmail" => BuildGmailCredentialPayload(tokens.accessToken, tokens.refreshToken),
      "Microsoft" => BuildMicrosoftCredentialPayload(tokens.accessToken, tokens.refreshToken),
      _ => throw new InvalidOperationException($"Unknown provider: {provider}")
  };
  // Pass credentialData to n8n create credential API
  ```
- [ ] Implement `BuildGmailCredentialPayload` and `BuildMicrosoftCredentialPayload` based on n8n credential type schema
  - Gmail n8n credential type: check `N8nProvisioningService.cs` for the type name currently used; payload must include access/refresh token + client ID/secret
  - Microsoft n8n credential type: similar structure
- [ ] Ensure `GetDecryptedTokensAsync` is called only at provisioning time — tokens must not be held in memory longer than the provisioning call
- [ ] Provisioning failure: leave `MailboxConnection.Status` as "CredentialError" (not "Active") — existing `MailboxConnection.Status` field
- [ ] Disconnect: call n8n credential delete API to clean up n8n-side credential ID (`MailboxConnection.N8nCredentialId`)
- [ ] Add tests:
  - Gmail payload builder constructs correct n8n credential shape
  - Microsoft payload builder constructs correct n8n credential shape
  - Provisioning without credential throws/logs clearly
  - Disconnect cleans up n8n credential ID

### Session 6 — End-to-End Validation, Documentation, Memory Update

> **Read before starting:** All previous session code, `.env.example`, `memory/` folder

- [ ] Run `dotnet test` — all tests must pass
- [ ] Run `npm run build && npm run lint && npm run test` — all must pass
- [ ] Manual end-to-end validation:
  - Connect a Gmail mailbox from inbox settings
  - Confirm `MailboxCredential` row is created with encrypted token fields (not plaintext)
  - Confirm `N8nCredentialId` is set on `MailboxConnection` after provisioning
  - Send a test email to the connected mailbox
  - Confirm `InboxEvent` is created via n8n webhook
  - Confirm `InboxGuardianService` processes the event and generates a draft
  - Disconnect the mailbox
  - Confirm `MailboxCredential` row is deleted
  - Confirm n8n credential is removed
- [ ] Repeat for Microsoft
- [ ] Update `memory/memory-architecture.md` to describe `MailboxCredential` entity and credential flow
- [ ] Update `memory/memory-security.md` to describe encrypted credential storage and no-break rules
- [ ] Update `memory/memory-risks.md` to mark R2 (empty mailbox credentials) as resolved

---

## Architecture Rules

- `MailboxCredential.EncryptedAccessToken` and `EncryptedRefreshToken` fields must NEVER be returned in any API response DTO
- `MailboxCredentialEncryptionService` must be registered as `Singleton` (holds key in memory)
- `IMailboxCredentialService` must be `Scoped` (uses `AppDbContext`)
- Decrypted tokens must only be in memory for the duration of a single service method call
- The `MailboxCredential` entity must have the same EF global query filter as all tenant-scoped entities: `TenantId == _tenantProvider.GetTenantId()`
- The connect/callback OAuth flow must use server-side state validation (not just URL params) — CSRF protection is mandatory
- The new `MailboxCredential` table must be in the same PostgreSQL database as the rest of OrvixFlow — no separate credential store
- Provider OAuth apps (Google Cloud Console, Azure Portal) must be separate from the login OAuth apps — separate client IDs/secrets for mailbox-specific scopes

---

## Security Requirements

1. `MAILBOX_CREDENTIAL_ENCRYPTION_KEY` must never be committed
2. Access tokens and refresh tokens must never appear in logs, API responses, or error messages
3. OAuth state parameter must be validated on callback to prevent CSRF
4. Revocation endpoint must be called on disconnect (best effort)
5. Provider OAuth app client ID/secret must be separate from login OAuth client credentials
6. Tenant isolation: `MailboxCredential` must be filtered by `TenantId` in all queries

---

## Tests Required

### New Unit Tests

- `MailboxCredentialEncryptionServiceTests.cs` (Session 2)
- `MailboxCredentialServiceTests.cs` (Session 2)
- `MailboxConnectionCredentialControllerTests.cs` (Session 3)
- `N8nProvisioningWithCredentialTests.cs` (Session 5)

### Integration Tests

- Tenant isolation: verify cross-tenant credential access returns 404/403
- CSRF state validation: verify callback with wrong state returns 400

### Frontend Tests (Vitest)

- Connection state rendering for each state (connected, reconnect, not-connected, provisioning)

### Test Commands

```bash
dotnet test --filter "FullyQualifiedName~MailboxCredential"
dotnet test --filter "FullyQualifiedName~MailboxConnection"
dotnet test --filter "FullyQualifiedName~N8nProvisioning"
dotnet test
```

---

## Validation Checklist

- [ ] `MailboxCredential` entity and migration applied cleanly
- [ ] Encryption service round-trips correctly (test passes)
- [ ] Access/refresh tokens in DB are encrypted (not plaintext)
- [ ] No token values appear in any API response
- [ ] No token values appear in application logs
- [ ] Gmail mailbox connect flow works end-to-end
- [ ] Microsoft mailbox connect flow works end-to-end
- [ ] Reconnect updates tokens correctly
- [ ] Disconnect deletes credential and cleans n8n credential
- [ ] n8n receives real credential payload (not empty)
- [ ] InboxGuardianService processes real email from connected mailbox
- [ ] Existing login flow (Google/Microsoft sign-in) is not affected
- [ ] Tenant isolation holds: cannot access another tenant's credentials
- [ ] `dotnet test` — all tests pass
- [ ] `npm run build && npm run lint && npm run test` — all pass
- [ ] `memory/` files updated

---

## Definition of Done

1. Gmail and Microsoft mailboxes can be connected from inbox settings UI
2. Credentials are stored encrypted at rest
3. n8n receives real provider credentials (not empty payloads)
4. Inbox Guardian processes emails from a real connected mailbox end-to-end
5. Reconnect and disconnect flows work
6. All tests pass
7. Memory files updated

---

## Common Mistakes

1. **Reusing login OAuth tokens for mailbox** — the login OAuth flow does not request mailbox-level scopes. A separate explicit consent flow is required. Do not shortcut this.
2. **Storing tokens in frontend state** — tokens must never live in browser memory/localStorage beyond the OAuth callback redirect. Call the backend immediately.
3. **Forgetting CSRF state validation on callback** — OAuth callback without state validation is a security vulnerability.
4. **Storing plaintext tokens in `MailboxCredential`** — encrypt before saving, decrypt only in `IMailboxCredentialService.GetDecryptedTokensAsync`
5. **Not cleaning up n8n credentials on disconnect** — orphaned n8n credentials are a security risk (they contain encrypted tokens on the n8n side)
6. **Using the login OAuth client ID for mailbox scopes** — the login client only has identity scopes. Create a separate OAuth app for mailbox scopes.

---

## Handoff to Phase 4 / Phase 5

Phase 3 does not block Phase 4 (CI/CD) or Phase 5 (observability). These can proceed in parallel.

After Phase 3 is complete:
- Update `tasks/production/progress.md` with completion date
- Ensure `memory/` files are updated (Session 6)
- Confirm all tests pass before closing Phase 3
