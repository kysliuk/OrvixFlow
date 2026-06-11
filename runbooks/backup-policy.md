# OrvixFlow Database Backup Policy

## RPO (Recovery Point Objective)
Maximum acceptable data loss: **24 hours**
*Basis:* Automated daily backups are scheduled to run at 02:00 UTC.

## RTO (Recovery Time Objective)
Maximum acceptable recovery time: **4 hours**
*Basis:* Time required for backup download, decryption, and restoration on standard production hardware.

## Retention Policy
* **Daily Backups:** Retained for **30 days**. Automatically pruned by the backup script (`scripts/backup.sh`).
* **Weekly Backups (Manual):** Keep the **4 most recent** weekly backups. These are run manually or flagged on Sundays.
* **Monthly Backups (Manual):** Retained for **1 year**. Run manually on the 1st of each month.

## Backup Location
* **Storage Provider:** S3-compatible storage or dedicated backup MinIO instance (physically separate from the primary application server to prevent single point of failure).
* **MinIO Bucket:** `orvixflow-backups`
* **Path Structure:** `daily/orvixflow_YYYYMMDD_HHMMSS.dump.gpg`

## Encryption
* **Algorithm:** AES-256 (GPG symmetric encryption).
* **Key Management:** Configured via `BACKUP_ENCRYPTION_KEY` environment variable. The key must be stored securely in the production vault/secret manager and NOT on the same backup storage service.
* **Key Rotation:** Rotate the passphrase annually. Re-encrypt all currently retained backups with the new key when rotated.

## Backup Verification
* **Frequency:** Monthly.
* **Procedure:** Restore the latest backup to a test database and verify the row counts match the production database metadata. Refer to the [Backup Restore Runbook](file:///media/kysliuk/562CAB9C2CAB7621/Repo/kysliuk/OrvixFlow/OrvixFlow/runbooks/backup-restore.md) for step-by-step instructions.
