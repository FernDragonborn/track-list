# DB Seed Plan — Real Reviews from Reddit + TMDB

**Author:** Claude (autonomous overnight run)
**Date:** 2026-06-02
**Goal:** Populate dev DB with realistic, demonstration-quality data using REAL reviews scraped from public sources.

---

## Targets

| Bucket | Count | Source |
|---|---|---|
| Accounts | 50–100 (real author handles) | Reddit usernames |
| Media | ~300+ diverse movies + series across genres/years | TMDB `/discover` |
| Long reviews (>500 chars) | ≥ 500 | Reddit r/movies, r/television, r/TrueFilm, r/MovieReviews + TMDB `/{id}/reviews` |
| Short / empty reviews | ≥ 2000 | Reddit short comments ("8/10, watch it"), low-effort TMDB reviews, rating-only |
| Language | EN primarily, UA when found | as-is, no auto-translation |
| Password | `password` (all users) | hardcoded |
| Avatar | DiceBear API URL `https://api.dicebear.com/7.x/avataaars/svg?seed={username}` | external, free |

## Constraints (BRL-4 + schema)

- **One review per (user, media)** — DB unique index. Seeder must dedupe before POST.
- **Username**: `[MaxLength(25)]` — slugify Reddit handle, truncate.
- **Email**: `[MaxLength(50)]` — `{username}@example.com`.
- **Content**: `[MaxLength(10000)]` — truncate long reviews to 9900 chars + ellipsis.
- **Rating**: int 1–10 (range-validated).
- **ProfilePicUrl**: `[MaxLength(2000)]` — DiceBear URL well under that.
- **No 1-2 hot media**: cap per-media to ≤ 50 reviews. Distribute breadth.

## Source strategy

### Reddit (anon JSON, ~60 req/min limit)

- Top-rated posts in: `r/movies`, `r/television`, `r/MovieReviews`, `r/TelevisionReviews`, `r/TrueFilm`, `r/horror`, `r/animesuggest` (variety!).
- Endpoints: `https://www.reddit.com/r/{sub}/top.json?t=all&limit=100`, then `https://www.reddit.com/comments/{id}.json` for comment threads.
- Parse `selftext` (post body) + top comments for review-shaped text.
- Title parsing for media name: regex strip `[Review]`, `Discussion:`, etc. → fuzzy match to TMDB.

### TMDB (already integrated, key in `.env`)

- `/movie/{id}/reviews` + `/tv/{id}/reviews` — returns real registered TMDB reviews with author handles.
- Use as primary fallback if Reddit yields too few per media.
- Already legal + free + structured (no parsing).

### Rating extraction

- Reddit: regex `(\d+)\s*[/]?(?:out\s*of)?\s*10` or `★+/10` patterns. If none → estimate from sentiment (positive title words: "amazing", "loved" → 8; "boring", "worst" → 3) OR random in 6–9 (skewed positive, like real Reddit).
- TMDB: `author_details.rating` (1–10) when present. Else as above.

## Architecture

```
seeder/
  main.py              # entry, orchestrates phases
  config.py            # API base URL, TMDB key, paths
  api_client.py        # ASP.NET API wrapper (register/login/post review)
  tmdb_client.py       # TMDB API wrapper
  reddit_scraper.py    # Reddit JSON scraper
  text_utils.py        # slugify, rating extract, length classify
  state.py             # idempotency: persist completed steps to JSON (resumable)
  data/
    media_seeded.json  # {tmdb_id: media_guid}
    users.json         # {handle: {username, email, token, user_id}}
    reviews_posted.json  # set of (user_id, media_id) tuples
  scraped/             # raw scrape dumps for inspection
```

### Idempotency / Resumability

- All steps write to JSON state files.
- On rerun: skip work already done.
- Critical for overnight: if process dies, restart from where we left off.

### Rate limiting

- Reddit: `time.sleep(1.1)` between requests (~55/min).
- TMDB: 40 req / 10 sec — `time.sleep(0.3)`.
- App API: no sleep — local network.

### Commit cadence

- Commit code (seeder package, plan, any tweaks) at start.
- Commit each major phase complete: `chore(seed): phase X complete (Y rows inserted)`.
- DB state itself is NOT committed (Postgres volume).
- Final summary doc committed at end: `Docs/seed-results.md` with counts.

## Backend changes (if any)

- **Avoid** if possible. App offers register/login/review endpoints — sufficient.
- **If needed**:
  - Maybe widen `Review.Content MaxLength` if real reviews exceed 10k. Action: truncate client-side instead (safer than schema change).
  - Skip backend changes unless absolutely blocked.

## Execution phases

1. **Bring up stack** — `docker compose -f docker-compose.dev.yaml up -d --build`. Wait healthcheck. Confirm admin login.
2. **Seed media** — pull 30 popular TMDB IDs × 10 genre+year combos. For each: `GET /api/media/Tmdb:{type}:{id}` (auto-imports). Save GUID mapping.
3. **Scrape Reddit** — top posts/comments from ~10 subreddits. Filter by length, extract author handles + ratings.
4. **Pull TMDB reviews** — for each seeded media, fetch `/reviews`.
5. **Register users** — for each unique handle, POST `/api/profiles/register`. Save tokens.
6. **Post reviews** — for each (user, media, content, rating): POST as user. Skip dupes.
7. **Verify** — query API counts. Sample 5 reviews look real.
8. **Final commit** — code + summary doc.

## Failure modes & handling

| Failure | Response |
|---|---|
| Reddit 429 | exponential backoff up to 60s, then continue |
| TMDB 429 | sleep 10s |
| API timeout / 500 | log, skip that review, continue |
| BCrypt slow (register) | accept ~100ms/user, total ~10s for 100 users |
| Network blip | retry 3× |
| Container restart | resume from state files |

## Out of scope

- No frontend changes
- No DB schema migrations (truncate instead)
- No auto-translation
- No comments/likes/follows seed (reviews only per user spec)
- No production deployment data

---

**Status updates** will be appended to this file as phases complete.
