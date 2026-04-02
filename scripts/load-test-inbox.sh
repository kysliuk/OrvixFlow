#!/bin/bash
# Load Test Script for Inbox Processing
# Usage: ./scripts/load-test-inbox.sh <api-url> <tenant-id> [count]
# Example: ./scripts/load-test-inbox.sh http://localhost:5000 00000000-0000-0000-0000-000000000001 100

API_URL="${1:-http://localhost:5000}"
TENANT_ID="${2:-00000000-0000-0000-0000-000000000001}"
COUNT="${3:-100}"

echo "========================================="
echo "Inbox Guardian Load Test"
echo "========================================="
echo "API URL: $API_URL"
echo "Tenant ID: $TENANT_ID"
echo "Jobs to enqueue: $COUNT"
echo "========================================="
echo ""

START_TIME=$(date +%s)

echo "Enqueuing $COUNT inbox processing jobs..."
for i in $(seq 1 $COUNT); do
  curl -s -X POST "$API_URL/api/inbox/process" \
    -H "Content-Type: application/json" \
    -H "X-Tenant-ID: $TENANT_ID" \
    -d "{\"messageId\":\"load-test-$i\",\"senderEmail\":\"test$i@example.com\",\"subject\":\"Load Test $i\",\"bodyText\":\"This is a load test message $i\"}" > /dev/null 2>&1 &
done

wait

END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

echo ""
echo "========================================="
echo "All $COUNT jobs enqueued in ${DURATION}s"
echo "========================================="
echo ""
echo "Next steps:"
echo "1. Check Hangfire dashboard: $API_URL/hangfire"
echo "2. Monitor Postgres connections:"
echo "   psql -c 'SELECT count(*) FROM pg_stat_activity;'"
echo "3. Check job completion rate in Hangfire"
echo "4. Monitor API response times during processing"
echo ""
echo "Expected behavior:"
echo "- Jobs should process sequentially (Hangfire default)"
echo "- Each job takes ~2-5s (LLM classification + RAG + draft generation)"
echo "- Total processing time: ~$((COUNT * 3))s for $COUNT jobs"
echo ""
