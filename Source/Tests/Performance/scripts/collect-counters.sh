#!/bin/bash
# Collect dotnet-counters from the perf API container during performance tests.
# Usage: ./collect-counters.sh [output-file] [duration-seconds]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PERF_DIR="$(dirname "$SCRIPT_DIR")"
COMPOSE_FILE="$PERF_DIR/docker-compose.perf.yaml"

OUTPUT_FILE="${1:-$PERF_DIR/results/counters.csv}"
DURATION="${2:-300}"

DC="docker compose -f $COMPOSE_FILE"

echo "Installing dotnet-counters in API container (standalone, no SDK needed)..."
$DC exec -T perf-api sh -c '\
  if [ ! -f /tmp/dotnet-counters ]; then \
    wget -q -O /tmp/dotnet-counters https://aka.ms/dotnet-counters/linux-musl-x64 && \
    chmod +x /tmp/dotnet-counters; \
  fi'

echo "Finding dotnet process..."
PID=$($DC exec -T perf-api sh -c 'pidof dotnet | awk "{print \$1}"')
echo "PID: $PID"

echo "Collecting counters for ${DURATION}s → $OUTPUT_FILE"
$DC exec -T perf-api sh -c "\
  /tmp/dotnet-counters collect \
    --process-id $PID \
    --format csv \
    --output /tmp/counters.csv \
    --duration $DURATION \
    --counters \
      'System.Runtime[cpu-usage,working-set,gc-heap-size,gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,threadpool-thread-count,threadpool-queue-length,exception-count]' \
      'Microsoft.AspNetCore.Hosting[requests-per-second,total-requests,current-requests,failed-requests]'" &

COUNTER_PID=$!
echo "$COUNTER_PID" > /tmp/k6-counter-pid
echo "dotnet-counters collecting in background (shell PID: $COUNTER_PID)"
