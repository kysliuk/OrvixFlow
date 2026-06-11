# Runbook: Storage Provider Switch

> **Historical Runbook Draft**
> This runbook was written for the older storage-migration track and may not reflect the current runtime path.
> Validate against current code, deployment config, and `tasks/production/current-state-audit.md` before operational use.

## Purpose
This runbook explains how to switch OrvixFlow storage between Local, MinIO, and Azure Blob, with verification and rollback steps for each path.

## Before You Start
1. Confirm the current provider in runtime configuration: `Storage:Provider`.
2. Confirm `dotnet test` is green on the release candidate build.
3. Confirm database backups are current.
4. Confirm the migration job status is reviewed before any Local -> MinIO cutover.
5. **Do not remove the `uploads_data` Docker volume until the migration tool reports status = `Complete` in production and production has remained stable after the switch.**

## Switch: Local -> MinIO
1. Verify MinIO is running and healthy.
2. Verify these settings are available to the API service:
   - `Storage__Provider=MinIO`
   - `Storage__MinIO__Endpoint`
   - `Storage__MinIO__Bucket`
   - `MINIO_ACCESS_KEY`
   - `MINIO_SECRET_KEY`
3. Start the application with MinIO reachable, but keep the legacy local files and `uploads_data` volume intact.
4. Run the local-to-MinIO migration job from the approved admin path.
5. Wait for the migration job to finish and confirm the reported status is `Complete`.
6. Verify a sample of migrated documents:
   1. Open document metadata in the database.
   2. Confirm `StoragePath` now contains an object key, not a filesystem path.
   3. Confirm a matching `StoredObject` row exists.
   4. Download at least one migrated file through the API.
7. Switch the API deployment/runtime configuration to `Storage:Provider = MinIO` if not already active.
8. Restart or redeploy the API.
9. Run a smoke test:
   1. Upload a file.
   2. Download the file.
   3. Delete the file.
   4. Confirm the object exists or is removed in MinIO as expected.

### Rollback: Local <- MinIO
1. Stop new uploads if possible.
2. Change runtime configuration back to `Storage:Provider = Local`.
3. Restart or redeploy the API.
4. Confirm the API can still read legacy files from local storage.
5. Keep MinIO data intact for investigation; do not delete migrated objects during rollback.
6. Record the reason for rollback and capture logs from the API and MinIO.

## Switch: MinIO -> Azure Blob
1. Verify Azure storage account access and the target container name.
2. Verify these settings are available to the API service:
   - `Storage__Provider=AzureBlob`
   - `Storage__AzureBlob__ContainerName`
   - `AZURE_STORAGE_CONNECTION_STRING`
3. Confirm the Azure container is private and created with no public access.
4. Confirm the migration/copy procedure from MinIO to Azure Blob has completed for the release window.
5. Verify a sample of files exists in Azure Blob using the expected storage key pattern.
6. Update the production deployment to `Storage:Provider = AzureBlob`.
7. Restart or redeploy the API.
8. Run a smoke test:
   1. Upload a file.
   2. Download the file.
   3. Delete the file.
   4. Confirm the object lifecycle matches the database state.
9. Monitor storage health checks, upload failures, and download failures for the full deployment window.

### Rollback: MinIO <- Azure Blob
1. Stop new uploads if possible.
2. Change runtime configuration back to `Storage:Provider = MinIO`.
3. Restart or redeploy the API.
4. Re-run smoke tests against MinIO.
5. Keep Azure data intact for investigation; do not delete copied blobs during rollback.
6. Record the reason for rollback and capture logs from the API and Azure storage diagnostics.

## Post-Switch Verification Checklist
1. If the active provider is `Local` or `MinIO`, confirm `/health/storage` reports healthy.
2. If the active provider is `AzureBlob`, verify storage reachability through the upload/download/delete smoke test and Azure diagnostics because `/health/storage` currently checks only Local and MinIO.
3. Upload succeeds.
4. Download succeeds.
5. Delete succeeds.
6. `StoredObject` rows are created with the correct provider and storage key.
7. No unexpected increase in 5xx responses or virus scan failures.
