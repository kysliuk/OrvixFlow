#!/bin/bash
# OrvixFlow — Automated PostgreSQL Backup
# Runs daily via cron. Encrypts and uploads to MinIO.
# Required env: POSTGRES_HOST, POSTGRES_USER, POSTGRES_DB, POSTGRES_PASSWORD,
#               MINIO_ENDPOINT, MINIO_ACCESS_KEY, MINIO_SECRET_KEY, MINIO_BACKUP_BUCKET,
#               BACKUP_ENCRYPTION_KEY

set -euo pipefail

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="orvixflow_${TIMESTAMP}.dump"
ENCRYPTED_FILE="${BACKUP_FILE}.gpg"

echo "[backup] Starting backup at ${TIMESTAMP}"

# Dump database
PGPASSWORD="${POSTGRES_PASSWORD}" pg_dump \
    -h "${POSTGRES_HOST}" \
    -U "${POSTGRES_USER}" \
    -d "${POSTGRES_DB}" \
    -Fc \
    -f "/tmp/${BACKUP_FILE}"

echo "[backup] Dump complete: /tmp/${BACKUP_FILE}"

# Encrypt with GPG symmetric encryption
gpg --batch --yes --passphrase "${BACKUP_ENCRYPTION_KEY}" \
    --symmetric --cipher-algo AES256 \
    -o "/tmp/${ENCRYPTED_FILE}" \
    "/tmp/${BACKUP_FILE}"

echo "[backup] Encryption complete"

# Upload to MinIO using mc (MinIO client)
mc alias set orvix "${MINIO_ENDPOINT}" "${MINIO_ACCESS_KEY}" "${MINIO_SECRET_KEY}"
mc cp "/tmp/${ENCRYPTED_FILE}" "orvix/${MINIO_BACKUP_BUCKET}/daily/${ENCRYPTED_FILE}"

echo "[backup] Uploaded to MinIO: ${MINIO_BACKUP_BUCKET}/daily/${ENCRYPTED_FILE}"

# Cleanup temp files
rm "/tmp/${BACKUP_FILE}" "/tmp/${ENCRYPTED_FILE}"

# Prune backups older than 30 days
mc find "orvix/${MINIO_BACKUP_BUCKET}/daily/" \
    --older-than 720h \
    --exec "mc rm {}"

echo "[backup] Old backups pruned. Backup complete."
