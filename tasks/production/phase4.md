# Phase 4 — CI/CD Pipeline

> **Status:** Not Started  
> **Estimated effort:** 1 week  
> **Dependencies:** Phase 0 complete (env documented); production deployment target selected  
> **Parallel:** Can run alongside Phase 3 (Mailbox OAuth) — they are independent  
> **Blocks:** Phase 5 (observability assumes CI/CD exists)

---

## Goal

Create a fully automated CI/CD pipeline so that every pull request triggers automated tests and every merge to `main` triggers a production deployment. No code change should ever reach production without passing the full test suite.

---

## Why

The project has 561 backend tests and a Vitest frontend suite. Currently there is no automated runner — all tests must be run manually before committing. With 7+ weeks of inactivity followed by potentially multiple contributors, the absence of CI is a regression time bomb. A single untested commit could silently break auth, RBAC, or billing in production.

Two additional gaps compound this risk:
- There is no `docker-compose.prod.yml` (production compose is undefined)
- There is no automated deployment mechanism (no deploy script, no push-to-registry workflow)

This phase closes all three gaps.

---

## Scope

- GitHub Actions CI workflow (build + test on PR and push to `main`)
- GitHub Actions deploy workflow (Docker build + push + deploy on `main`)
- GitHub Secrets configuration for all production env vars
- `.env.example` validation in CI
- Docker image build test on PRs

---

## Out of Scope

- No `docker-compose.prod.yml` here (that is Phase 5)
- No observability setup (that is Phase 5)
- No Kubernetes or Helm charts — Docker Compose remains the deployment model
- No self-hosted GitHub runner setup (use GitHub-hosted runners)
- No staging environment setup (assume single production environment for now)
- No branch protection rules (optional — agent can set them up but it is not required)

---

## Dependencies

- **Phase 0 complete** — `.env.example` must be complete before wiring GitHub Secrets
- **Production deployment target selected** — agent must know the target host (SSH host, Docker registry URL) before writing the deploy workflow. If not selected yet, create CI workflow only and leave deploy workflow as a stub.
- **Docker registry** — either Docker Hub, GitHub Container Registry (ghcr.io), or a self-hosted registry. GHCR is recommended (free, integrates with GitHub Actions)

---

## Files / Components Likely Involved

| File | Task |
|---|---|
| `.github/workflows/ci.yml` | NEW — CI workflow |
| `.github/workflows/deploy.yml` | NEW — deploy workflow |
| `OrvixFlow.Api/Dockerfile` | Reference — must exist and be buildable |
| `orvixflow-web/Dockerfile` | Reference — must exist and be buildable |
| `docker-compose.yml` | Reference — service definitions |
| `.env.example` | Reference — all env vars to wire as GitHub Secrets |

---

## Pre-Flight Check

Before writing workflow files, verify:

```bash
# 1. Backend builds cleanly
dotnet build OrvixFlow.sln

# 2. All tests pass
dotnet test

# 3. Frontend builds cleanly
cd orvixflow-web && npm install && npm run build

# 4. Frontend tests pass
npm run test

# 5. API Dockerfile builds
docker build -t orvixflow-api:test ./OrvixFlow.Api/

# 6. Web Dockerfile builds
docker build -t orvixflow-web:test ./orvixflow-web/
```

If any step fails before the CI workflow exists, fix it first. Do not write a CI workflow that wraps broken build commands.

> If the Dockerfiles do not exist yet, they must be created as part of this phase. See P4-5 for guidance.

---

## Implementation Tasks

### P4-1 — Create .github/workflows/ci.yml

**File:** `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  backend:
    name: Backend — Build & Test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal

  frontend:
    name: Frontend — Build, Lint & Test
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: orvixflow-web
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: orvixflow-web/package-lock.json

      - name: Install dependencies
        run: npm ci

      - name: Lint
        run: npm run lint

      - name: Build
        run: npm run build
        env:
          # Build needs these to be set; use placeholder values for CI
          NEXTAUTH_SECRET: ci-placeholder-secret
          NEXTAUTH_URL: http://localhost:3000
          NEXT_PUBLIC_API_BASE_URL: http://localhost:5000

      - name: Test
        run: npm run test
```

**Critical notes:**
- `dotnet test` must produce 0 failures. If any test fails, the PR is blocked.
- `npm run build` requires env vars that reference API URL and auth secrets. Use placeholder values for CI — the build should not call the API at build time.
- Check what env vars `orvixflow-web/next.config.ts` and `orvixflow-web/auth.ts` require at build time. Add all required vars to the frontend build step with safe CI placeholder values.
- Do NOT add `--no-restore` to `dotnet test` — the test runner needs the restored packages.

### P4-2 — Create .github/workflows/deploy.yml

**File:** `.github/workflows/deploy.yml`

> ⚠️ **This workflow depends on the production target.** If the production host is not yet configured (server IP, SSH key, registry URL), create the workflow as a stub with placeholder values and mark it `[CONFIGURE BEFORE USE]`. Do not block Phase 4 completion on this.

```yaml
name: Deploy to Production

on:
  push:
    branches: [main]
  workflow_dispatch:  # Allow manual trigger

jobs:
  deploy:
    name: Build, Push & Deploy
    runs-on: ubuntu-latest
    needs: []  # No dependency on CI job — CI is a separate workflow
    # Only deploy if CI passed (use branch protection to enforce this)
    
    steps:
      - uses: actions/checkout@v4

      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push API image
        uses: docker/build-push-action@v5
        with:
          context: .
          file: OrvixFlow.Api/Dockerfile
          push: true
          tags: ghcr.io/${{ github.repository_owner }}/orvixflow-api:latest,ghcr.io/${{ github.repository_owner }}/orvixflow-api:${{ github.sha }}

      - name: Build and push Web image
        uses: docker/build-push-action@v5
        with:
          context: orvixflow-web
          file: orvixflow-web/Dockerfile
          push: true
          tags: ghcr.io/${{ github.repository_owner }}/orvixflow-web:latest,ghcr.io/${{ github.repository_owner }}/orvixflow-web:${{ github.sha }}

      - name: Deploy to production server
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.PROD_SSH_HOST }}
          username: ${{ secrets.PROD_SSH_USER }}
          key: ${{ secrets.PROD_SSH_KEY }}
          script: |
            cd /opt/orvixflow
            docker compose -f docker-compose.prod.yml pull
            docker compose -f docker-compose.prod.yml up -d --remove-orphans
            docker system prune -f
```

**Notes:**
- The deploy workflow triggers on every push to `main`. Use branch protection rules to require CI to pass before merging.
- `docker-compose.prod.yml` must exist on the production server (create in Phase 5).
- The `appleboy/ssh-action` requires the production server to have Docker and Docker Compose installed.
- Rollback: re-trigger deploy with a previous SHA or use `docker compose -f docker-compose.prod.yml up -d --image <previous-tag>`

### P4-3 — Set Up GitHub Secrets

Navigate to the GitHub repository → Settings → Secrets and variables → Actions.

Add the following secrets (values come from your production `.env`):

**Infrastructure:**
| Secret Name | Value Source |
|---|---|
| `PROD_SSH_HOST` | Production server IP or hostname |
| `PROD_SSH_USER` | SSH username (e.g., `deploy`) |
| `PROD_SSH_KEY` | Private SSH key for the deploy user |

**Application (backend):**
| Secret Name | Maps From |
|---|---|
| `JWT_SECRET` | `JWT_SECRET` in `.env` |
| `STRIPE_SECRET_KEY` | `STRIPE_SECRET_KEY` |
| `STRIPE_WEBHOOK_SECRET` | `STRIPE_WEBHOOK_SECRET` |
| `MINIO_ACCESS_KEY` | `MINIO_ACCESS_KEY` |
| `MINIO_SECRET_KEY` | `MINIO_SECRET_KEY` |
| `N8N_ADMIN_USER` | `N8N_ADMIN_USER` |
| `N8N_ADMIN_PASSWORD` | `N8N_ADMIN_PASSWORD` |
| `N8N_ENCRYPTION_KEY` | `N8N_ENCRYPTION_KEY` |
| `POSTGRES_PASSWORD` | `POSTGRES_PASSWORD` |
| `EMAIL_RESEND_API_KEY` | `EMAIL_RESEND_API_KEY` |
| `MAILBOX_CREDENTIAL_ENCRYPTION_KEY` | `MAILBOX_CREDENTIAL_ENCRYPTION_KEY` (after Phase 3) |

**Application (frontend):**
| Secret Name | Maps From |
|---|---|
| `NEXTAUTH_SECRET` | `NEXTAUTH_SECRET` |
| `AUTH_SECRET` | `AUTH_SECRET` |
| `GOOGLE_CLIENT_ID` | `GOOGLE_CLIENT_ID` |
| `GOOGLE_CLIENT_SECRET` | `GOOGLE_CLIENT_SECRET` |
| `AZURE_AD_CLIENT_ID` | `AZURE_AD_CLIENT_ID` |
| `AZURE_AD_CLIENT_SECRET` | `AZURE_AD_CLIENT_SECRET` |

> ⚠️ Do NOT add secrets as repository variables (those are not encrypted). Always use Secrets.

### P4-4 — Add .env.example Validation in CI

Add a step to `ci.yml` to verify `.env.example` is kept in sync with `docker-compose.yml` and `appsettings.json`:

```yaml
  validate-config:
    name: Config Validation
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Verify .env.example is complete
        run: |
          # Check that all required env vars referenced in docker-compose.yml
          # have a corresponding entry in .env.example
          missing=0
          while IFS= read -r var; do
            if ! grep -q "^${var}=" .env.example && ! grep -q "^# ${var}" .env.example; then
              echo "MISSING from .env.example: ${var}"
              missing=$((missing + 1))
            fi
          done < <(grep -oP '\$\{\K[A-Z_]+(?=\}|\:)' docker-compose.yml | sort -u)
          
          if [ $missing -gt 0 ]; then
            echo "Error: $missing env vars referenced in docker-compose.yml are not documented in .env.example"
            exit 1
          fi
          echo "All env vars documented in .env.example"
```

This script extracts all `${VAR_NAME}` references from `docker-compose.yml` and verifies each appears in `.env.example`. This prevents the Phase 0 bug (STRIPE_WEBHOOK_SECRET missing) from recurring.

### P4-5 — Add Docker Image Build Test in CI

If Dockerfiles do not exist yet, create them. Then add a build-only step to CI for PRs:

**Check if Dockerfiles exist:**
```bash
ls OrvixFlow.Api/Dockerfile
ls orvixflow-web/Dockerfile
```

**If `OrvixFlow.Api/Dockerfile` does not exist:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrvixFlow.Api/OrvixFlow.Api.csproj", "OrvixFlow.Api/"]
COPY ["OrvixFlow.Core/OrvixFlow.Core.csproj", "OrvixFlow.Core/"]
COPY ["OrvixFlow.Infrastructure/OrvixFlow.Infrastructure.csproj", "OrvixFlow.Infrastructure/"]
RUN dotnet restore "OrvixFlow.Api/OrvixFlow.Api.csproj"
COPY . .
RUN dotnet publish "OrvixFlow.Api/OrvixFlow.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrvixFlow.Api.dll"]
```

**If `orvixflow-web/Dockerfile` does not exist:**
```dockerfile
FROM node:20-alpine AS base
WORKDIR /app

FROM base AS deps
COPY package*.json ./
RUN npm ci --only=production

FROM base AS builder
COPY package*.json ./
RUN npm ci
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1
ARG NEXTAUTH_URL
ARG NEXT_PUBLIC_API_BASE_URL
ENV NEXTAUTH_URL=$NEXTAUTH_URL
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
RUN npm run build

FROM base AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/public ./public
COPY --from=builder /app/package.json ./
EXPOSE 3000
CMD ["npm", "start"]
```

**Add to `ci.yml`:**
```yaml
  docker-build:
    name: Docker Build Test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Build API Docker image
        run: docker build -t orvixflow-api:ci -f OrvixFlow.Api/Dockerfile .

      - name: Build Web Docker image
        run: |
          docker build \
            --build-arg NEXTAUTH_URL=http://localhost:3000 \
            --build-arg NEXT_PUBLIC_API_BASE_URL=http://localhost:5000 \
            -t orvixflow-web:ci \
            -f orvixflow-web/Dockerfile \
            orvixflow-web/
```

---

## Architecture Rules

- CI workflow must fail fast — any test failure blocks the PR
- `dotnet test` must produce 0 failures (not just 0 errors)
- The deploy workflow must NOT run on PRs — only on `main` push
- Docker images must be tagged with both `latest` and the commit SHA — the SHA tag enables rollbacks
- GitHub Secrets are the only acceptable secrets store for CI/CD — no secrets in workflow files
- Workflow files must use pinned action versions (e.g., `actions/checkout@v4`, not `@main` or `@master`)

---

## Tests Required

There are no code tests for CI/CD configuration. Validation is by observation:

### Manual Validation

- [ ] Open a PR with a trivial change → CI workflow triggers automatically
- [ ] Verify backend job passes (shows green checkmark on PR)
- [ ] Verify frontend job passes
- [ ] Verify docker-build job passes
- [ ] Introduce a deliberate test failure (e.g., `Assert.True(false)` in any test) → CI must block the PR
- [ ] Revert the deliberate failure → CI passes again
- [ ] Merge PR to `main` → deploy workflow triggers
- [ ] Verify Docker images appear in GitHub Container Registry (ghcr.io)
- [ ] Verify deploy SSH step completes successfully

---

## Validation Checklist

- [ ] `.github/workflows/ci.yml` exists and triggers on PR and push to main
- [ ] `.github/workflows/deploy.yml` exists (or exists as a documented stub if host not configured)
- [ ] All GitHub Secrets are configured (verify in Settings → Secrets — values not visible, but names must match)
- [ ] `ci.yml` runs: `dotnet test` (backend), `npm run lint` + `npm run build` + `npm run test` (frontend)
- [ ] `ci.yml` runs: Docker build test for both images
- [ ] `ci.yml` runs: `.env.example` validation step
- [ ] A deliberate test failure blocks the PR
- [ ] Merging to `main` triggers deploy workflow (or stub is clearly marked for future configuration)
- [ ] `OrvixFlow.Api/Dockerfile` exists and builds cleanly
- [ ] `orvixflow-web/Dockerfile` exists and builds cleanly

---

## Definition of Done

1. Every PR triggers CI automatically
2. CI checks backend tests, frontend build/lint/test, Docker build, and env var completeness
3. A broken test prevents PR merge
4. `main` push triggers (or is ready to trigger) deployment
5. All GitHub Secrets are configured
6. Both Dockerfiles exist and produce working images

---

## Common Mistakes

1. **Using `--no-build` on `dotnet test` in CI** — the test project must be built before running; remove this flag in CI to ensure fresh builds
2. **Not setting `NEXTAUTH_SECRET` for the frontend build step** — Next.js auth requires this even at build time; use a placeholder value
3. **Pinning to `@master` or `@latest` action refs** — always pin to a specific version tag (e.g., `@v4`) for security and reproducibility
4. **Deploy workflow runs on PRs** — deploy must only run on `main` push; add a branch condition
5. **Committing secrets to workflow files** — always use `${{ secrets.SECRET_NAME }}` syntax, never hardcode values
6. **Skipping the Docker build test** — the app could pass all unit tests but fail to build into a Docker image; always test the build

---

## Handoff to Phase 5

Before Phase 5 starts, confirm:

1. CI triggers on every PR
2. At least one full CI run has passed green (all jobs)
3. Dockerfiles exist and build successfully
4. GitHub Secrets are configured
5. Deploy workflow is created (even if target is not yet fully configured)

Phase 5 requires infrastructure (production server, domain, TLS) to be selected and accessible before it can be completed.
