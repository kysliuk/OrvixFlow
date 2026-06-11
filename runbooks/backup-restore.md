# OrvixFlow Backup Restore Runbook

This guide covers how to restore the OrvixFlow database from an encrypted backup.

> [!WARNING]
> Never perform database restoration testing on the active production database instance directly. Always use a separate environment or a temporary database schema.

---

## Part 1: Periodic Backup Verification (Staging/Test Restore)
Follow these steps to decrypt and restore a backup file to a temporary database to verify backup integrity.

1. **Set Environment Variables:**
   Load production or staging environment credentials:
   ```bash
   export BACKUP_ENCRYPTION_KEY="your-backup-encryption-key"
   export POSTGRES_HOST="localhost" # or container hostname
   export POSTGRES_USER="orvix_admin"
   export POSTGRES_PASSWORD="your-db-password"
   ```

2. **Download the Target Backup:**
   Retrieve the encrypted backup from MinIO/S3 using the MinIO client (`mc`):
   ```bash
   mc cp orvix/orvixflow-backups/daily/orvixflow_20260611_020000.dump.gpg /tmp/restore.dump.gpg
   ```

3. **Decrypt the Backup File:**
   Symmetrically decrypt the file using GPG:
   ```bash
   gpg --batch --passphrase "${BACKUP_ENCRYPTION_KEY}" \
       --decrypt /tmp/restore.dump.gpg > /tmp/restore.dump
   ```

4. **Create a Test Database:**
   ```bash
   PGPASSWORD="${POSTGRES_PASSWORD}" createdb \
       -h "${POSTGRES_HOST}" \
       -U "${POSTGRES_USER}" \
       orvixflow_restore_test
   ```

5. **Restore the Schema and Data:**
   Run `pg_restore` using format-custom (`-Fc`) against the test database:
   ```bash
   PGPASSWORD="${POSTGRES_PASSWORD}" pg_restore \
       -h "${POSTGRES_HOST}" \
       -U "${POSTGRES_USER}" \
       -d orvixflow_restore_test \
       --no-owner \
       /tmp/restore.dump
   ```

6. **Verify Row Counts & Table Integrity:**
   Compare the row counts of key tables to verify restore success:
   ```bash
   PGPASSWORD="${POSTGRES_PASSWORD}" psql \
       -h "${POSTGRES_HOST}" \
       -U "${POSTGRES_USER}" \
       -d orvixflow_restore_test \
       -c "SELECT relname, n_live_tup FROM pg_stat_user_tables ORDER BY n_live_tup DESC LIMIT 20;"
   ```

7. **Clean Up Temporary Files & Test DB:**
   ```bash
   rm /tmp/restore.dump.gpg /tmp/restore.dump
   PGPASSWORD="${POSTGRES_PASSWORD}" dropdb \
       -h "${POSTGRES_HOST}" \
       -U "${POSTGRES_USER}" \
       orvixflow_restore_test
   ```

---

## Part 2: Disaster Recovery (Production Restore)
In the event of database corruption, data loss, or server failure, follow these steps to restore the database in production.

1. **Stop Application Containers:**
   Stop the services that write to the database to prevent write conflicts:
   ```bash
   cd /opt/orvixflow
   docker compose -f docker-compose.prod.yml stop orvix-api orvix-web orvix-n8n
   ```

2. **Download and Decrypt the Latest Backup:**
   Identify the latest healthy backup file and decrypt it inside the database container or host:
   ```bash
   # Download
   mc cp orvix/orvixflow-backups/daily/orvixflow_LATEST.dump.gpg /tmp/restore.dump.gpg
   
   # Decrypt
   gpg --batch --passphrase "${BACKUP_ENCRYPTION_KEY}" \
       --decrypt /tmp/restore.dump.gpg > /tmp/restore.dump
   ```

3. **Re-create the Database:**
   Drop the current corrupt database and create a clean, empty one:
   ```bash
   # Terminate existing connections and drop
   docker compose -f docker-compose.prod.yml exec orvix-db dropdb -U orvix_admin orvixflow
   
   # Create new DB
   docker compose -f docker-compose.prod.yml exec orvix-db createdb -U orvix_admin orvixflow
   ```

4. **Restore Database from File:**
   ```bash
   # Copy dump into database container
   docker cp /tmp/restore.dump orvix_postgres_prod:/tmp/restore.dump
   
   # Run pg_restore in container
   docker compose -f docker-compose.prod.yml exec orvix-db pg_restore -U orvix_admin -d orvixflow --no-owner /tmp/restore.dump
   ```

5. **Restart Application Containers:**
   Start all stopped services back up:
   ```bash
   docker compose -f docker-compose.prod.yml start
   ```

6. **Verify System Status:**
   Confirm that all services are back online and healthy:
   ```bash
   docker compose -f docker-compose.prod.yml ps
   curl -i https://api.yourdomain.com/health/rag
   ```
