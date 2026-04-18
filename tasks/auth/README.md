# Auth Plans Index

Last updated: 2026-04-18

This folder stores the current auth-system audit and the concrete fix plans derived from it.

Files:
- `auth-audit-2026-04-18.md`: Saved source-based audit of the current auth/authz system.
- `plan-01-session-and-token-hardening.md`: Large auth/session/token repair plan.
- `plan-02-invite-and-verification-flows.md`: Large identity-proofing and invitation-flow repair plan.
- `plan-03-org-context-and-auth-api-consistency.md`: Large multi-company context and API/frontend contract repair plan.
- `plan-04-small-fixes-and-cleanup.md`: Smaller auth fixes grouped into short phases.

Recommended execution order:
1. `plan-01-session-and-token-hardening.md`
2. `plan-02-invite-and-verification-flows.md`
3. `plan-03-org-context-and-auth-api-consistency.md`
4. `plan-04-small-fixes-and-cleanup.md`

Rules for future agents:
- Read `auth-audit-2026-04-18.md` before changing auth-sensitive code.
- Keep global roles and company roles separate.
- Treat `User.TenantId` as legacy compatibility data, not automatic authorization truth.
- Re-test login, refresh, switch-company, invite, verify-email, and admin access after every auth change.
