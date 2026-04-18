# Plan 02 - Invite And Verification Flows

Status: proposed
Priority: P0
Scope: large identity-proofing and onboarding repair

## Goal

Make registration, email verification, invitations, and invite acceptance complete, truthful, and secure.

## Issues Covered

### Issue 1. Invite flow does not actually deliver the invitation

Problem:
- Invite token is created and stored, but no invitation email is sent.
- API response claims invitation was sent.

Proposed fix:
- Extend `InviteUserAsync` to send or queue an invitation email using `IEmailService` or notification queue.
- Include invite accept URL and token in the delivery channel, not in API response.
- Fail the request if the system cannot create a durable delivery path.

Acceptance criteria:
- Sending an invite produces a real email or queued notification.
- API response only reports success after durable invite delivery setup.

### Issue 2. Verification tokens never expire

Problem:
- Email verification uses a token with no expiry field or expiry check.

Proposed fix:
- Add `VerificationTokenExpiresAt` to `User` or move verification into a dedicated table.
- Set expiry at registration time.
- Reject expired tokens in `VerifyEmailAsync`.
- Clear token and expiry after successful verification.

Acceptance criteria:
- Expired verification links are rejected.
- Fresh verification links succeed once and only once.

### Issue 3. Registration flow is non-transactional

Problem:
- User and tenant are saved before email send and before all account setup completes.

Proposed fix:
- Wrap registration persistence in a transaction if email send is part of the same success contract, or
- move outbound email into a durable queued notification model so DB commit completes first and sending becomes retryable.
- Ensure owner membership and subscription setup are part of the same durable account-provisioning flow.

Acceptance criteria:
- Registration does not leave half-created auth state on outbound email failure.

### Issue 4. Invite acceptance bypasses local-password policy

Problem:
- New invited local users may be created with weak password or no password.

Proposed fix:
- Decide explicit invite-accept modes:
  - local-password accept flow requires password and applies complexity validation, or
  - invite accept only activates membership and requires separate password-set flow.
- Remove ambiguous optional-password behavior for new local accounts.

Acceptance criteria:
- New local invited accounts cannot be created without passing defined password rules.

### Issue 5. Invitation and verification tokens are stored in plaintext

Problem:
- DB compromise exposes live tokens directly.

Proposed fix:
- Store hashed invite tokens and hashed verification tokens.
- Compare hashes on accept/verify.
- Keep raw token only at issuance time for email link construction.

Acceptance criteria:
- Database rows no longer contain usable raw invitation or verification tokens.

### Issue 6. Legacy `OrganizationController.Invite` bypasses the real invite workflow

Problem:
- Duplicate invite path creates inconsistent invited users/memberships directly.

Proposed fix:
- Remove the endpoint if unused, or
- reimplement it as a thin adapter that delegates to the canonical invite service and returns the same behavior.

Acceptance criteria:
- Exactly one canonical invitation flow exists.

## Implementation Phases

### Phase 1. Verification token expiration model
- Add expiry field
- Enforce in verify logic
- Add tests

### Phase 2. Durable registration flow
- Decide transactional vs queued email strategy
- Make user, tenant, membership, and subscription setup atomic from business perspective

### Phase 3. Canonical invitation delivery
- Send/queue invite emails
- Remove misleading success semantics
- Add acceptance-link path verification

### Phase 4. Token storage hardening
- Hash invite tokens
- Hash verification tokens
- Update accept/verify queries and tests

### Phase 5. Remove duplicate invite path
- Retire or delegate `OrganizationController.Invite`

## Tests To Add Or Update

- `AuthServiceTests`
  - verification rejects expired token
  - verification clears token after success
  - registration handles email failure without partial account leakage
  - invite send triggers email/notification delivery
  - accept invite enforces password rules for new local accounts
- Add invitation-flow tests
  - duplicate pending invite revocation behavior still works
  - hashed token lookup works for accept flow
- Frontend/e2e
  - register -> verify -> login
  - invite send -> email link -> accept
