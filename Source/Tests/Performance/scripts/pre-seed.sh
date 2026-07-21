#!/bin/bash
# Pre-register key users + promote roles BEFORE k6 seed runs.
# Usage: source pre-seed.sh  (requires $API_PORT and $DC to be set)

API="http://localhost:${API_PORT}/api"

echo "[pre-seed] Registering key users..."
for u in \
  '{"email":"perfadmin@test.com","username":"perfadmin","password":"PerfAdmin123!","confirmPassword":"PerfAdmin123!"}' \
  '{"email":"perfmod@test.com","username":"perfmod","password":"PerfMod123!","confirmPassword":"PerfMod123!"}' \
  '{"email":"perfuser1@test.com","username":"perfuser1","password":"PerfTest123!","confirmPassword":"PerfTest123!"}' \
  '{"email":"perfuser2@test.com","username":"perfuser2","password":"PerfTest123!","confirmPassword":"PerfTest123!"}'; do
  STATUS=$(curl -so /dev/null -w '%{http_code}' -X POST "${API}/profiles/register" \
    -H 'Content-Type: application/json' -d "$u" 2>/dev/null)
  USER=$(echo "$u" | grep -o '"username":"[^"]*"' | cut -d'"' -f4)
  echo "  $USER: $STATUS"
done

echo "[pre-seed] Promoting admin/moderator roles..."
$DC exec -T perf-db psql -U postgres -d tracklistdb_perf -c \
  "UPDATE \"Users\" SET \"Role\" = 'Admin' WHERE \"Email\" = 'perfadmin@test.com' AND \"Role\" != 'Admin';" 2>/dev/null || true
$DC exec -T perf-db psql -U postgres -d tracklistdb_perf -c \
  "UPDATE \"Users\" SET \"Role\" = 'Moderator' WHERE \"Email\" = 'perfmod@test.com' AND \"Role\" != 'Moderator';" 2>/dev/null || true
echo "[pre-seed] Done."
