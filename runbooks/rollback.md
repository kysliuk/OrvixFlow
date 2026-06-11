# OrvixFlow Rollback Runbook

This guide covers how to roll back a broken production deployment quickly.

## Option A: Rollback via Docker Image Tags (Recommended)
If you have a known stable version tag or previous Git commit SHA, you can explicitly point the production compose file to that tag to roll back immediately.

1. **Log in to the Production Server:**
   ```bash
   ssh user@your-server-ip
   cd /opt/orvixflow
   ```

2. **Pull the Known Stable Images:**
   Replace `<previous-tag>` with the last stable tag or SHA (e.g., `v1.2.3` or `sha-abcdef`):
   ```bash
   docker pull ghcr.io/your-github-username/orvixflow-api:<previous-tag>
   docker pull ghcr.io/your-github-username/orvixflow-web:<previous-tag>
   ```

3. **Pin the Tags in the Docker Compose / Env Environment:**
   If your `docker-compose.prod.yml` is configured to use environment variables for tags, update your `.env` file.
   Alternatively, edit `docker-compose.prod.yml` directly:
   ```yaml
   # Under orvix-api:
   image: ghcr.io/your-github-username/orvixflow-api:<previous-tag>
   
   # Under orvix-web:
   image: ghcr.io/your-github-username/orvixflow-web:<previous-tag>
   ```

4. **Apply the Changes:**
   Re-deploy the containers:
   ```bash
   docker compose -f docker-compose.prod.yml up -d
   ```

5. **Verify Application Status:**
   Ensure the application starts up correctly and the error/alert rate drops back to normal:
   ```bash
   docker compose -f docker-compose.prod.yml ps
   docker compose -f docker-compose.prod.yml logs --tail=50 orvix-api
   ```

---

## Option B: Rollback via Git Revert (Standard Pipeline)
If direct SSH or tag manipulation is not preferred, or to keep git history aligned, you can revert the bad commit in your git history.

1. **Locate the Bad Commit and Revert It:**
   On your local machine:
   ```bash
   git log --oneline
   # Revert the commit (creates a new commit that undoes the changes)
   git revert <bad-commit-sha>
   ```

2. **Push the Revert Commit to GitHub:**
   ```bash
   git push origin main
   ```

3. **Verify CI/CD Pipeline:**
   Wait for the CI/CD pipeline to build the reverted images, run tests, and automatically trigger the deployment flow to production (Phase 4).

4. **Verify Health:**
   Check the observability dashboard (Seq, Sentry, BetterStack) to ensure the system has stabilized.
