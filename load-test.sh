#!/bin/bash

# Simple load test script for RAG ingestion endpoint
# Usage: ./load-test.sh <url> <token> <tenant-id>

URL=$1
TOKEN=$2
TENANT_ID=$3

if [ -z "$URL" ] || [ -z "$TOKEN" ] || [ -z "$TENANT_ID" ]; then
  echo "Usage: ./load-test.sh <url> <token> <tenant-id>"
  exit 1
fi

echo "Starting load test: 10 concurrent uploads..."

for i in {1..10}
do
  # Create a dummy file for each request
  echo "This is test file content for chunking and embedding #$i" > "test_$i.txt"
  
  curl -X POST "$URL/api/v1/knowledge/upload" \
    -H "Authorization: Bearer $TOKEN" \
    -H "X-Tenant-ID: $TENANT_ID" \
    -F "file=@test_$i.txt" \
    -s -o /dev/null -w "Request $i: %{http_code}\n" &
done

wait
echo "Load test complete. Cleaning up..."
rm test_*.txt
