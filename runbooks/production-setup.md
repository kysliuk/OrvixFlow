# OrvixFlow Production Setup Runbook

This guide describes how to configure, bootstrap, and deploy OrvixFlow in a production environment using Docker Compose and Traefik.

## Prerequisites
* **Server:** Ubuntu 22.04+ LTS Linux server (minimum 2 vCPUs, 4GB RAM recommended).
* **Docker & Compose:** Docker Engine and Docker Compose plugin installed.
* **Network & DNS:** 
  * Port 80 (HTTP) and Port 443 (HTTPS) must be open to the public internet.
  * DNS A-records pointing to the server's public IP:
    * `yourdomain.com` (Frontend app)
    * `api.yourdomain.com` (Backend API)
    * `n8n.yourdomain.com` (Automation engine)

## First Deployment

1. **SSH into the Production Server:**
   ```bash
   ssh user@your-server-ip
   ```

2. **Clone the Repository:**
   Clone the code to `/opt/orvixflow` (or your preferred deployment directory):
   ```bash
   sudo git clone https://github.com/your-org/orvixflow.git /opt/orvixflow
   sudo chown -R $USER:$USER /opt/orvixflow
   cd /opt/orvixflow
   ```

3. **Configure Environment Variables:**
   Create a production environment file from the template:
   ```bash
   cp .env.example .env
   nano .env
   ```
   *Fill out all fields. Make sure to generate secure, cryptographically random strings for `JWT_SECRET`, `NEXTAUTH_SECRET`, `AUTH_SECRET`, `N8N_ENCRYPTION_KEY`, and `BACKUP_ENCRYPTION_KEY`.*

4. **Start the Services:**
   Start all containers in detached mode:
   ```bash
   docker compose -f docker-compose.prod.yml up -d
   ```

5. **Verify Startup:**
   Check container statuses to ensure everything is running and healthy:
   ```bash
   docker compose -f docker-compose.prod.yml ps
   ```

6. **Verify TLS and Application Health:**
   Wait up to 5 minutes for Traefik to perform the ACME TLS challenge and retrieve SSL certificates.
   ```bash
   curl -i https://api.yourdomain.com/health/rag
   curl -i https://yourdomain.com
   ```
   Expected responses should be `HTTP/2 200` (or `307 Redirect` to SSL/NextAuth endpoints).

## EF Core Migrations
EF Core migrations are automatically applied on startup by the backend API (`db.Database.Migrate()` inside `Program.cs`).
* If the API fails to start or health checks return 500/Connection errors, inspect the API logs:
  ```bash
  docker compose -f docker-compose.prod.yml logs orvix-api
  ```
* To run migrations manually outside of container start, refer to the [Migration Production Runbook](file:///media/kysliuk/562CAB9C2CAB7621/Repo/kysliuk/OrvixFlow/OrvixFlow/runbooks/migration-production.md).

## Updating to a New Version
To deploy a new release (when images are built and pushed to the container registry):
```bash
cd /opt/orvixflow
# Pull latest production images
docker compose -f docker-compose.prod.yml pull

# Re-deploy containers with minimal downtime
docker compose -f docker-compose.prod.yml up -d --remove-orphans

# Clean up unused/dangling Docker images to save space
docker system prune -f
```

## TLS Certificate Renewal
Traefik handles TLS certificate renewal automatically via Let's Encrypt, checking for renewal 30 days before expiration.
* Verify the TLS certificate expiration manually:
  ```bash
  curl -v https://api.yourdomain.com 2>&1 | grep "expire date"
  ```
