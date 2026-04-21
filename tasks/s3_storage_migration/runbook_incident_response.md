# Runbook: Storage Incident Response

## Purpose
This runbook explains how to respond to common storage and file-safety incidents in OrvixFlow.

## General Rules
1. Treat missing files and hash mismatches as data integrity incidents.
2. Do not delete storage objects during the first response phase unless the procedure below explicitly says to do so.
3. Capture timestamps, tenant IDs, document IDs, storage keys, and recent deployment changes before making fixes.

## Incident: MinIO Unreachable
1. Confirm the failure scope:
   1. Check API errors for upload/download failures.
   2. Check `/health/storage`.
2. Check the MinIO container/service status.
3. Check network connectivity from the API container/host to MinIO.
4. Verify MinIO credentials and endpoint values.
5. If MinIO is down, restore service first.
6. After recovery, run an upload/download smoke test.
7. If recovery is delayed, decide whether to roll back to a known-good provider using the provider-switch runbook.

## Incident: File Not Found in Storage
1. Capture the document ID, tenant ID, and `StoragePath`/storage key.
2. Look up the document row and the related `StoredObject` row.
3. Verify the provider recorded in metadata matches the active runtime provider.
4. Check whether the object exists in the configured bucket/container.
5. If metadata exists but the object is missing:
   1. Mark the incident as data loss or incomplete migration.
   2. Restore the object from backup or from the previous provider if available.
6. If the object exists but the API still fails, review provider credentials and storage path handling.
7. After remediation, verify download through the API, not only through storage tooling.

## Incident: Orphan Objects Detected
1. Record the bucket/container name and the orphan object keys.
2. Confirm the warning came from the orphan detection job or manual inspection.
3. For each object, search for a matching `StoredObject` row and related document/image record.
4. Do not delete immediately.
5. Classify each orphan:
   1. Failed upload with leftover object.
   2. Failed delete with stale metadata cleanup.
   3. Migration artifact.
   4. Unknown origin.
6. If the object is confirmed unused, schedule cleanup during a controlled maintenance window.
7. If the object should have metadata, repair the metadata first and document the root cause.

## Incident: SHA-256 Mismatch
1. Treat this as a potential corruption or incomplete migration incident.
2. Capture the stored hash, newly computed hash, storage key, and provider.
3. Re-download the object and recompute the hash to rule out a transient read issue.
4. Compare the hash stored on the document/`StoredObject` metadata with the source-of-truth copy.
5. If the mismatch remains:
   1. Quarantine the affected file from normal use.
   2. Restore a known-good copy from backup or the previous provider.
   3. Re-run verification before returning the file to service.
6. Check for any recent migration, copy, or storage SDK changes.

## Incident: ClamAV Daemon Down
1. Confirm uploads are failing because virus scanning is fail-closed.
2. Check the ClamAV container/service status.
3. Check whether the daemon is healthy and accepting connections on port 3310.
4. Review API logs for scanner connection errors.
5. Restore ClamAV service.
6. Re-test with a normal file upload.
7. Do not bypass virus scanning in production as an incident shortcut.
8. If the outage is prolonged, communicate that uploads are intentionally blocked until scanning is restored.
