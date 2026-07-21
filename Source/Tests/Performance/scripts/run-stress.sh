#!/bin/bash
# Orchestrator: seed → counters → k6 stress test → report
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PERF_DIR="$(dirname "$SCRIPT_DIR")"
RESULTS_DIR="$PERF_DIR/results"
COMPOSE_FILE="$PERF_DIR/docker-compose.perf.yaml"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
DC="docker compose -f $COMPOSE_FILE"
API_PORT=8090
LOG_FILE="$RESULTS_DIR/stress_full_${TIMESTAMP}.log"
mkdir -p "$RESULTS_DIR"

exec > >(tee "$LOG_FILE") 2>&1

echo "=== Stress Test Orchestrator ==="

# 1. Ensure containers
echo "[1/6] Starting perf containers..."
$DC down -v --remove-orphans
$DC up -d --build

# 2. Wait for API
echo "[2/6] Waiting for API on port ${API_PORT}..."
WAIT_SEC=0
MAX_WAIT=120
until curl -so /dev/null -w '%{http_code}' http://localhost:${API_PORT}/api/feed/global 2>/dev/null | grep -qE '^[2-4]'; do
  WAIT_SEC=$((WAIT_SEC + 3))
  if [ $WAIT_SEC -ge $MAX_WAIT ]; then
    echo "  TIMEOUT after ${MAX_WAIT}s. Container logs:"
    $DC logs --tail 30 perf-api
    exit 1
  fi
  # Show container state + last log line every tick
  STATUS=$($DC ps perf-api --format '{{.Status}}' 2>/dev/null || echo "unknown")
  LAST_LOG=$($DC logs --tail 1 perf-api 2>&1 | tail -1)
  echo "  ...${WAIT_SEC}s | status: ${STATUS} | ${LAST_LOG}"
  sleep 3
done
echo "[2/6] API ready (${WAIT_SEC}s)."

# 3. Pre-register key users + promote roles
echo "[3/6] Pre-registering users and promoting roles..."
source "$SCRIPT_DIR/pre-seed.sh"

# 4. Seed
if [ ! -f "$PERF_DIR/data/seed-data.json" ]; then
  echo "Seeding test data..."
  k6 run -e BASE_URL=http://localhost:${API_PORT} "$PERF_DIR/data/seed.js" 2>&1 | tee "$RESULTS_DIR/seed_${TIMESTAMP}.log"
  sed -n '/SEED_DATA_JSON_START/,/SEED_DATA_JSON_END/p' "$RESULTS_DIR/seed_${TIMESTAMP}.log" \
    | grep -v 'SEED_DATA_JSON' > "$PERF_DIR/data/seed-data.json" || true
else
  echo "Using existing seed-data.json"
fi

# 5. Start dotnet-counters (10 min + buffer)
echo "Starting dotnet-counters (660s)..."
bash "$SCRIPT_DIR/collect-counters.sh" "$RESULTS_DIR/counters_stress_${TIMESTAMP}.csv" 660 &
COUNTER_PID=$!

# 6. Run k6 stress test
echo "Running k6 stress test (10→200 VUs, 10 min)..."
cd "$PERF_DIR"
STRESS_LOG="$RESULTS_DIR/stress_${TIMESTAMP}.log"
k6 run \
  -e BASE_URL=http://localhost:${API_PORT} \
  --out json="$RESULTS_DIR/stress_${TIMESTAMP}.json" \
  --summary-export="$RESULTS_DIR/stress_summary_${TIMESTAMP}.json" \
  scenarios/stress.js 2>&1 | tee "$STRESS_LOG" || true

# Cleanup
kill $COUNTER_PID 2>/dev/null || true
$DC exec -T perf-api sh -c 'kill $(pidof dotnet-counters) 2>/dev/null; sleep 2' 2>/dev/null || true
$DC cp perf-api:/tmp/counters.csv "$RESULTS_DIR/counters_stress_${TIMESTAMP}.csv" 2>/dev/null || true

echo ""
echo "=== Stress test complete ==="
echo "Report: $RESULTS_DIR/stress_report.html"
echo "Counters: $RESULTS_DIR/counters_stress_${TIMESTAMP}.csv"
