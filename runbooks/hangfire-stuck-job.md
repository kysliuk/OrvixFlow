# OrvixFlow Hangfire Stuck Job Recovery Runbook

This guide describes how to identify, debug, and recover from stuck, infinitely processing, or repeatedly failing Hangfire background jobs.

---

## Part 1: Symptoms of a Stuck Job
* **Failure Alerts:** Structured logs or Seq alerts report job failures (registered by `JobFailureAlertFilter`).
* **Trial Expiration / Inbox Processing Delays:** Users complain that emails are not being processed, or subscriptions are not transitioning to trial status.
* **CPU spikes:** High database or API CPU utilization caused by a job stuck in an infinite loop.

---

## Part 2: Recovery via Hangfire Dashboard (Recommended)

> [!NOTE]
> Access to the Hangfire dashboard is restricted to users with the `SuperAdmin` global role.

1. **Access the Dashboard:**
   Navigate to `https://api.yourdomain.com/hangfire`. Log in with a `SuperAdmin` account.

2. **Diagnose the Job State:**
   * Go to **Jobs** → **Processing** to see jobs currently running. Check the "Duration" column. Any job running for more than 30 minutes (e.g., `ProcessInboxJob`) is likely stuck.
   * Go to **Jobs** → **Failed** to see jobs that have thrown unhandled exceptions.

3. **Re-queue or Delete the Job:**
   * **To Retry:** Select the stuck/failed job and click **Re-queue Jobs**.
   * **To Terminate:** Select the job and click **Delete Selected**. This stops the scheduled run (but does not kill the active thread if it is stuck on the CPU).

4. **Restart Hangfire Server (If thread is frozen):**
   If the job is locked in an infinite CPU loop or database transaction, deleting it from the dashboard will not kill the underlying process. You must restart the API container:
   ```bash
   cd /opt/orvixflow
   docker compose -f docker-compose.prod.yml restart orvix-api
   ```

---

## Part 3: Recovery via Direct SQL (Emergency Mode)
If the backend API is unresponsive or the dashboard is inaccessible, you can manage the Hangfire tables directly via the PostgreSQL database.

1. **Connect to PostgreSQL:**
   ```bash
   docker compose -f docker-compose.prod.yml exec -it orvix-db psql -U orvix_admin -d orvixflow
   ```

2. **List all tables in the `hangfire` schema:**
   ```sql
   SELECT table_name FROM information_schema.tables WHERE table_schema = 'hangfire';
   ```

3. **Identify Stuck Processing Jobs:**
   Search for jobs that have been in the "Processing" state for an unusually long time:
   ```sql
   SELECT j.id, j.arguments, s.createdat 
   FROM hangfire.job j 
   JOIN hangfire.state s ON j.stateid = s.id 
   WHERE s.name = 'Processing' 
     AND s.createdat < NOW() - INTERVAL '30 minutes';
   ```

4. **Manually Terminate/Delete a Job:**
   To delete a stuck job using its ID:
   ```sql
   -- Delete from job queue table
   DELETE FROM hangfire.jobqueue WHERE jobid = <job_id>;
   
   -- Delete the job record
   DELETE FROM hangfire.job WHERE id = <job_id>;
   ```
   *Replace `<job_id>` with the actual integer ID returned from the query.*

5. **Restart the API to release resource locks:**
   ```bash
   docker compose -f docker-compose.prod.yml restart orvix-api
   ```
