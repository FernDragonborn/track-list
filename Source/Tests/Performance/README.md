# Performance Tests (k6 + dotnet-counters)

## Prerequisites

- [k6](https://k6.io/docs/getting-started/installation/) installed
- Docker + Docker Compose running
- Containers up: `docker compose -f Source/docker-compose.dev.yaml up -d`

## Quick Start

```bash
cd Source/Tests/Performance/scripts

# Smoke test (2 VUs, 1 min) — baseline check
bash run-smoke.sh

# Load test (50 VUs, 5 min) — expected traffic
bash run-load.sh

# Stress test (10→200 VUs, 10 min) — find breaking point
bash run-stress.sh
```

## What the orchestrator does

1. Starts Docker containers (if not running)
2. Waits for API health check
3. Promotes admin/moderator roles via SQL
4. Runs seed script (creates 24 users, 20 media, 60 reviews, etc.)
5. Starts `dotnet-counters` in API container (background)
6. Runs k6 scenario
7. Collects results → `results/` directory

## Seed Data

Generated via `data/seed.js`:

| Entity | Count | Notes |
|--------|-------|-------|
| Users | 24 | 4 personas + 20 generated |
| Follow relationships | ~26 | Overlapping social graph |
| Media | 20 | 4 types, Ukrainian translations |
| Reviews | 60 | 3 per generated user |
| Comments | ~120 | 2 per review |
| Likes | ~100 | 1-2 per review |
| Collections | 10 | 7 public, 3 private |
| Tracking statuses | 40 | 2 per user |

## Endpoints Covered

**Anonymous**: feed/global, media/search, media/{id}, collections/public
**Authenticated**: feed/personal, reviews (CRUD), likes, comments, tracking, collections
**Admin**: stats, CSV export
**Moderator**: pending translations

## Thresholds

| Scenario | p95 | p99 | Max Error Rate |
|----------|-----|-----|----------------|
| Smoke | < 500ms | < 1s | < 1% |
| Load | < 1s | < 2s | < 5% |
| Stress | < 2s | < 5s | < 10% |

## Results

After running, check `results/`:
- `*_report.html` — k6 HTML report with charts
- `*_summary_*.json` — machine-readable summary
- `counters_*.csv` — server-side metrics (CPU, memory, GC, DB connections)

## dotnet-counters Metrics

| Counter | What it shows |
|---------|---------------|
| cpu-usage | CPU % used by API process |
| working-set | RAM usage (bytes) |
| gc-heap-size | Managed heap size |
| gen-0/1/2-gc-count | GC pressure per generation |
| threadpool-thread-count | Active threads |
| threadpool-queue-length | Queued work items (>0 = thread starvation) |
| requests-per-second | Server-side throughput |
| current-requests | Concurrent request count |
| failed-requests | 5xx responses |

## Manual Run (without orchestrator)

```bash
# Seed only
k6 run data/seed.js

# Run specific scenario
cd Source/Tests/Performance
k6 run scenarios/smoke.js

# Override base URL
k6 run -e BASE_URL=http://my-server scenarios/load.js
```
