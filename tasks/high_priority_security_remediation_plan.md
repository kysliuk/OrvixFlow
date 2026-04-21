# Goal

Remediate three high-priority security issues with minimal, repo-consistent diffs:
1. block unrestricted local file reads in the Local -> MinIO migration job,
2. reduce unsafe MinIO exposure and remove weak example credentials from local config,
3. patch known frontend dependency vulnerabilities, especially `next@16.2.1`.

# Scope

In scope:
- `OrvixFlow.Infrastructure/Storage/LocalToMinioMigrationJob.cs`
- `OrvixFlow.Infrastructure/DependencyInjection.cs`
- `OrvixFlow.Api/Controllers/StorageMigrationController.cs` if status/help text needs adjustment
- `docker-compose.yml`
- `.env.example`
- `orvixflow-web/package.json`
- `orvixflow-web/package-lock.json`
- targeted backend/frontend tests
- relevant task doc update under `tasks/`

Out of scope:
- storage architecture redesign
- replacing MinIO
- auth/RBAC redesign
- broad frontend modernization beyond what is needed to clear current high-severity findings

# Constraints

- Prefer minimal diffs and existing project patterns.
- Preserve current migration workflow: super-admin-triggered Hangfire job with explicit `IgnoreQueryFilters()`.
- Keep `ForcePathStyle = true` for MinIO.
- Before frontend changes, review relevant Next.js docs under `orvixflow-web/node_modules/next/dist/docs/`.
- Avoid broad dependency churn unless targeted updates do not clear the audit.

# Workstreams

## 1. Storage migration file-read hardening

Primary files:
- `OrvixFlow.Infrastructure/Storage/LocalToMinioMigrationJob.cs`
- `OrvixFlow.Infrastructure/DependencyInjection.cs`
- new/updated tests under `OrvixFlow.Tests/`

Intent:
- ensure migration only reads files under the configured local storage root
- reject absolute paths outside that root, normalized traversal paths, and malformed inputs

## 2. MinIO compose/config hardening

Primary files:
- `docker-compose.yml`
- `.env.example`

Intent:
- reduce host exposure of MinIO API/console
- stop advertising weak example credentials
- keep local dev ergonomics acceptable

## 3. Frontend dependency patching

Primary files:
- `orvixflow-web/package.json`
- `orvixflow-web/package-lock.json`

Intent:
- patch `next` DoS advisory and related high-severity audit findings with the smallest safe package changes

# Step-by-step Tasks

## Workstream 1. Fix unrestricted local file read in migration job

1. Pass the configured local upload base path into `LocalToMinioMigrationJob`.
- Use `Storage:Local:BasePath` with the existing default `/app/uploads`.
- This keeps migration validation aligned with `LocalFileStorage`.

2. Add a centralized path normalization and containment helper in `LocalToMinioMigrationJob`.
- Require non-empty absolute path.
- Resolve with `Path.GetFullPath`.
- Normalize the base dir with a trailing separator.
- Allow only paths rooted under the configured base dir.

3. Route all filesystem operations through the validated path.
- Use the resolved safe path for `File.Exists`, `FileInfo`, `File.OpenRead`, and SHA-256 computation.
- Do not leave any raw `localPath` filesystem calls behind.

4. Decide failure behavior for invalid paths.
- Recommended: count out-of-root paths as `failed`, continue processing, and log them with entity id.
- Do not silently migrate or quietly skip tampered rows.

5. Preserve idempotency.
- Do not change key generation, DB update order, or re-run behavior for valid rows.

6. Add focused tests.
- Valid file inside base path migrates.
- Path outside base path is rejected.
- Normalized traversal path is rejected.
- Dry-run does not bypass validation.
- Missing file remains non-fatal.

## Workstream 2. Harden MinIO exposure and example credentials

7. Reduce MinIO host exposure in `docker-compose.yml`.
- Preferred minimal diff: bind `9000` and `9001` to `127.0.0.1` only.
- Stronger option: remove published ports entirely if host access is unnecessary.

8. Add comments clarifying MinIO API/console are local-dev only.
- Prevent unsafe copy/paste into shared or staging environments.

9. Replace MinIO example credentials in `.env.example`.
- Change realistic starter values to explicit placeholders like `CHANGE_ME_MINIO_ACCESS_KEY`.

10. Decide whether compose should fail fast on unset credentials.
- Optional: use strict interpolation for MinIO vars.
- Safer by default, but stricter for onboarding.

11. Check collateral docs/examples.
- Ensure runbooks and task docs still match the chosen exposure model.

## Workstream 3. Patch frontend dependency vulnerabilities

12. Patch the direct Next.js advisory first.
- Update `next` from `16.2.1` to the patched `16.2.4`.
- Update `eslint-config-next` to the matching patch version.

13. Refresh vulnerable transitive dev dependencies only as needed.
- Start with targeted patch bumps such as `vitest` if audit still reports `vite`-related issues.

14. If audit findings remain, use the narrowest fix.
- Prefer targeted parent-package bumps first.
- Use scoped `overrides` only for stubborn transitive issues.

15. Regenerate lockfile and verify audit output.
- Stop once there are no `high` or `critical` findings.

# Verification

## Backend / migration hardening

- Add or run targeted tests for `LocalToMinioMigrationJob`.
- Confirm valid paths under `/app/uploads` still migrate.
- Confirm `/etc/passwd`-style and traversal paths are rejected.
- Confirm dry-run uses the same validation logic.

Suggested commands during implementation:
- `dotnet test --filter "FullyQualifiedName~LocalToMinioMigrationJob"`
- `dotnet test`

## Compose / config hardening

- `docker compose config`
- Verify MinIO is still reachable from `orvix-api` on the internal Docker network.
- If loopback binding is chosen, verify host access works only via localhost.

## Frontend dependency remediation

- Review relevant Next.js docs before changing versions.
- Run:
- `npm audit --json`
- `npm run lint`
- `npm run test`
- `npm run build`

Success target:
- no `high` or `critical` findings in `orvixflow-web`

# Risks / Rollbacks

- Path validation may block legacy rows outside `/app/uploads`.
- Keep the security fix and surface those rows to operators; do not relax the root check.

- Loopback-only MinIO ports may break shared-host workflows.
- If needed, revert only the port-binding choice and keep credential hardening.

- Strict compose variable enforcement may slow onboarding.
- If needed, keep placeholders in `.env.example` but skip fail-fast interpolation.

- Next.js or Vitest patch upgrades may expose build/test regressions.
- Revert only the secondary package bump first; keep the direct Next.js security patch if stable.

- Scoped npm overrides may hide upstream drift.
- Replace them later with upstream package upgrades once validated.

# Open Questions

1. Is localhost-only MinIO access acceptable, or do you need host/LAN access to the MinIO console/API?
-localhost only
2. Should compose fail fast when MinIO credentials are unset or placeholder?
-yes
3. Should invalid migration paths count as `failed` or `skipped`?
- Recommended: `failed`
4. Is the goal to clear only `high/critical` findings, or a fully clean frontend audit?
-full clean frontend audit


# Tradeoffs Where User Input Helps

- Loopback bind vs no published MinIO ports
- Placeholder creds vs fail-fast compose vars
- Targeted package bumps plus overrides vs broader dependency refresh
- Clear only high severity vs clear all audit findings
