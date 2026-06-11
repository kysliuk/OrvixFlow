# OrvixFlow EF Core Production Migrations Runbook

This guide covers how Entity Framework Core database migrations are executed, monitored, and manually applied in the production environment.

---

## Part 1: Automated Migrations (Standard Behavior)
By default, the backend API container is configured to apply migrations automatically on startup via `db.Database.Migrate()` in `OrvixFlow.Api/Program.cs` (line 323). 

When you pull new Docker images and restart the stack:
1. The `orvix-api` container starts.
2. It attempts to connect to `orvix-db` (PostgreSQL).
3. It checks the table `__EFMigrationsHistory` to determine which migrations have not yet been applied.
4. It applies all pending migrations.
5. If successful, the API starts responding to traffic.

### Monitoring Logs:
To check if the automated migration succeeded on startup:
```bash
docker compose -f docker-compose.prod.yml logs -f orvix-api
```
Look for log lines indicating Entity Framework database commands or successful application startup.

---

## Part 2: Checking Migration Status in Database
To see which migrations have been applied to the database, query the EF Core migrations table directly:

1. **Access PostgreSQL Command Line:**
   ```bash
   docker compose -f docker-compose.prod.yml exec -it orvix-db psql -U orvix_admin -d orvixflow
   ```

2. **Run Query:**
   ```sql
   SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" ASC;
   ```
   *This returns the list of all successfully applied migrations in chronological order.*

---

## Part 3: Manual Migration Application (Disaster Recovery / Troubleshooting)
If the automated migration on startup fails (e.g., due to schema locks, conflicting manual DB alterations, or transient connection issues), you must run migrations manually.

### Option A: Using the Runtime Container
You can force a migration check or manual run from inside the running API container using the application dll assemblies. However, since the production container contains a trimmed .NET runtime without the SDK/EF tools, running commands like `dotnet ef` inside the container is not supported.

Instead, you can run a temporary SDK container attached to the same network to perform migrations.

### Option B: Using a Temporary SDK Container
If you need to manually apply migrations using the .NET SDK toolchain on the production host:

1. **Generate the Migration Script Locally:**
   Instead of applying migrations live, it is safer to generate an idempotent SQL script on your development/CI machine:
   ```bash
   # From local workspace root
   dotnet ef migrations script -i -o migration.sql --project OrvixFlow.Infrastructure --startup-project OrvixFlow.Api
   ```
   *The `-i` flag makes the script idempotent (inserts `IF NOT EXISTS` check clauses).*

2. **Copy the Script to the Production Server:**
   ```bash
   scp migration.sql user@your-server-ip:/tmp/migration.sql
   ```

3. **Run the SQL Script against the Database:**
   ```bash
   # Copy the SQL script into the postgres container
   docker cp /tmp/migration.sql orvix_postgres_prod:/tmp/migration.sql
   
   # Execute the script
   docker compose -f docker-compose.prod.yml exec orvix-db psql -U orvix_admin -d orvixflow -f /tmp/migration.sql
   ```

4. **Verify Table Status:**
   Log back into the database and verify the migrations table.

---

## Part 4: Migration Rollbacks
If a migration introduces breaking changes or fails midway, you may need to revert it.

> [!CAUTION]
> Reverting migrations in production can lead to data loss (e.g., dropping columns or tables). Always back up your database before attempting a manual rollback.

1. **Back up the database:**
   Refer to the [Backup Restore Runbook](file:///media/kysliuk/562CAB9C2CAB7621/Repo/kysliuk/OrvixFlow/OrvixFlow/runbooks/backup-restore.md).

2. **Locate the target migration ID to rollback to:**
   Query `__EFMigrationsHistory` to select the ID of the last known stable migration.

3. **Generate a Rollback Script (Down Script):**
   On your local machine, generate a rollback script starting from the broken migration down to the target stable migration:
   ```bash
   dotnet ef migrations script <broken_migration_id> <target_stable_migration_id> -o rollback.sql --project OrvixFlow.Infrastructure --startup-project OrvixFlow.Api
   ```

4. **Run the Rollback SQL Script:**
   Copy the `rollback.sql` script to the server and execute it via `psql` as described in Part 3.
