# DB Seeder

Populates the TrackList DB with realistic data: real reviews scraped from Reddit + pulled from TMDB, registered against real author handles, posted via the public API.

End state (default targets in `config.py`): ~100 users, ~300 media items, ~2700 reviews (≈ 500 long + 2000 short + synthesized rating-only fillers), DiceBear avatars on every user.

---

## Prerequisites

- Python 3.11+
- TrackList stack running locally (see root `docker-compose.dev.yaml`)
- TMDB API key (free): https://www.themoviedb.org/settings/api
- First admin credentials in `SEED_ADMIN_*`. On an empty DB the seeder creates
  this account through `/api/setup/admin`, then logs in with it.

### Install deps

Only the `requests` and (optional) `python-dotenv` packages are needed:

```bash
pip install requests python-dotenv
```

`python-dotenv` is optional — without it, set env vars in your shell instead of using `.env`.

---

## Setup

1. Copy the example env file and fill in your TMDB key:

   ```bash
   cd Source/seeder
   cp .env.example .env
   # edit .env, set TMDB_API_KEY
   ```

2. Bring up the API:

   ```bash
   docker compose -f Source/docker-compose.dev.yaml up -d --build
   ```

3. Confirm the API is reachable:

   ```bash
   curl http://localhost/api/stats/public
   ```

---

## Run

```bash
cd Source/seeder

# Run all phases (1 → 5)
py main.py

# Or a specific phase / phase list
py main.py 1       # seed media only
py main.py 2,3     # pull TMDB reviews + scrape Reddit
py main.py 4       # register users
py main.py 5       # post reviews
```

All phases are **idempotent**: re-running picks up where it left off via state files (`data/*.json`). Safe to interrupt and resume.

For a production-like config, enable demo seeding explicitly before phases that
register users or import TMDB media:

```env
TRACKLIST_PUBLIC_REGISTRATION=true
TRACKLIST_MAX_USERS=unlimited
TRACKLIST_ENABLE_TMDB=true
```

### Helper scripts

```bash
py set_avatars.py      # apply DiceBear PNG avatars to all seeded users
py post_leftovers.py   # post remaining real reviews not yet posted (clean-up pass)
```

---

## Phases

| # | Name | What it does |
|---|---|---|
| 1 | **Seed media** | TMDB `/discover` across genres + years, auto-imports each via `GET /api/media/Tmdb:{type}:{id}`. |
| 2 | **TMDB reviews** | `/movie/{id}/reviews` + `/tv/{id}/reviews` for each seeded media. |
| 3 | **Reddit scrape** | Anonymous JSON, top posts/comments from movie/TV subreddits; matches thread title → media (auto-imports if missing). |
| 4 | **Register users** | For each unique author handle (most prolific first), `POST /api/profiles/register`; sets DiceBear avatar URL. |
| 5 | **Post reviews** | Splits pool into long (≥ 500 chars) targeting 500 posts + short (< 200 chars + synthesized rating-only) targeting 2000 posts. Per-media cap 50. |

> **Note on Reddit / IMDb.** Both started returning aggressive 403/429 to anon scrapers during the TrackList window; Phase 3 falls back to whatever cached pulls already exist in `scraped/`. The TMDB-only path (Phase 2) is enough to fill the targets on its own.

---

## Config knobs (`config.py`)

| Knob | Default | Meaning |
|---|---|---|
| `TARGET_USERS` | 100 | Max users registered (Phase 4) |
| `TARGET_MEDIA_COUNT` | 300 | Media discovered (Phase 1) |
| `TARGET_LONG_REVIEWS` | 500 | Long-form review target (Phase 5) |
| `TARGET_SHORT_REVIEWS` | 2000 | Short-form review target (Phase 5) |
| `PER_MEDIA_REVIEW_CAP` | 50 | Max reviews per media (distribution) |
| `SUBREDDITS` | 10 subs | Reddit sources for Phase 3 |
| `MOVIE_GENRES` / `TV_GENRES` | TMDB IDs | Genres queried in Phase 1 |
| `DISCOVER_YEARS` | 11 years | Release-year buckets for Phase 1 |

### Env vars (loaded from `.env` or shell)

| Var | Default | Purpose |
|---|---|---|
| `SEED_API_BASE` | `http://localhost/api` | API endpoint |
| `SEED_ADMIN_USERNAME` | `admin` | Admin login |
| `SEED_ADMIN_EMAIL` | `admin@tracklist.local` | Admin email |
| `SEED_ADMIN_PASSWORD` | `seedPassword1` | Dev-only first account password; override locally |
| `SEED_SETUP_TOKEN` | — | Setup token for production-like `/api/setup/admin` |
| `TMDB_API_KEY` | — | **Required** for phases 1 + 2 |

EF migrations manage the application schema. `GenreSeeder` inserts stable
reference genres at API startup. This Python seeder is only for demo/test
content and should not be treated as production bootstrap.

---

## State files (gitignored)

`seeder/data/`:

| File | Contents |
|---|---|
| `media.json` | `{external_id: {guid, type, title, year}}` |
| `raw_reviews.json` | `{items: [...], tmdb_pulled: [...], reddit_subs_done: [...]}` |
| `users.json` | `{handle: {username, email, token, refresh, avatar_url, avatar_set}}` |
| `posted.json` | `{keys: [...], stats: {long, short}}` |

`seeder/scraped/`: raw Reddit JSON dumps per subreddit.

To reset: delete `data/` and `scraped/`, re-run `py main.py`.

---

## Defaults applied to seeded users

- Password: `password1` (validator requires 8+ chars, letter + digit; plain `password` is rejected)
- Email: `{slugified-username}@example.com`
- Avatar: `https://api.dicebear.com/7.x/avataaars/svg?seed={username}` (Phase 4) → optionally PNG via `set_avatars.py`

---

## Watchdog (optional, Windows-only)

`watchdog.ps1` is unrelated to seeding — it watches a Claude Code terminal window via UI Automation and sends `продовжуй` if Claude has been idle past a threshold, or hits ENTER on rate-limit dialogs. Useful for long unattended sessions.

```powershell
pwsh -File seeder\watchdog.ps1
pwsh -File seeder\watchdog.ps1 -WindowTitle "*Claude*" -PollSeconds 60 -IdleThresholdSec 600
pwsh -File seeder\watchdog.ps1 -DryRun
```

Stop with `Ctrl+C`.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `401 admin login failed` | Admin credentials wrong | Verify `SEED_ADMIN_*` in `.env` matches a real Admin-role user in the DB |
| `TMDB returned 401` | No / invalid API key | Set `TMDB_API_KEY` in `.env` |
| `Connection refused` | API not running | `docker compose up -d` from `Source/`; check `curl http://localhost/api/stats/public` |
| Phase 3 hangs / 429s | Reddit rate-limiting | Wait, or skip Phase 3 — Phase 2 alone fills targets |
| Reviews phase 5xx on some media | Backend duplicate constraint | Idempotent — re-run; already-posted reviews skipped via `posted.json` |
| Slow re-seed | SQLite WAL bloat | Stop API, delete `Source/data/tracklist.db-wal`, restart |
