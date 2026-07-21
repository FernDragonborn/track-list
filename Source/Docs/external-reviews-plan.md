# External Reviews Integration — Plan

**Author:** Claude (autonomous build)
**Started:** 2026-06-02
**Goal:** Pull legitimate external review data + ratings from non-TMDB sources, cache in DB, display in catalog + media detail with clear source attribution. Newly-imported media should never look empty.

---

## Sources (legitimate only)

| Source             | What it gives                                                               | API                                                                           | Auth                                            |
| ------------------ | --------------------------------------------------------------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------- |
| **OMDb**           | IMDb rating + vote count, RT Tomatometer, Metacritic score, parental rating | `https://www.omdbapi.com/?i={imdb_id}&apikey={key}`                           | Free key (1000/day) — registered via temp inbox |
| **Wikipedia**      | "Critical reception" section text (1 paragraph)                             | `https://en.wikipedia.org/api/rest_v1/page/summary/{title}` + section extract | None                                            |
| **Letterboxd RSS** | Long-form text reviews from popular reviewer accounts                       | `https://letterboxd.com/{handle}/rss/` per user                               | None                                            |

**Why these:**

- OMDb cross-references TMDB's `imdb_id`; gives the "IMDb 8.5/10" badge the user explicitly asked for, legally redistributable per OMDb ToS.
- Wikipedia gives literary "Critical reception" prose without spoilers (collapsible — user requested).
- Letterboxd RSS is public, no auth, no rate limit advertised; gives real long reviews from real reviewers.

**Out of scope:** Reddit (anon blocked + OAuth onboarding cost), Trakt (OAuth complexity), direct IMDb scraping (Cloudflare).

---

## Schema (3 new tables)

### `ExternalRating`

Per (media, source) — one row.

| Column                  | Type          | Notes                                          |
| ----------------------- | ------------- | ---------------------------------------------- |
| Id                      | uuid PK       |                                                |
| MediaId                 | uuid FK Media | indexed                                        |
| Source                  | varchar(20)   | `imdb`, `rotten_tomatoes`, `metacritic`        |
| Score                   | double        | normalized 0-10 (RT 92% → 9.2)                 |
| RawScore                | varchar(20)   | what the source reported, e.g. `92%`, `8.5/10` |
| VoteCount               | int?          | nullable — RT/Metacritic don't report          |
| FetchedAt               | timestamp     | for inspection                                 |
| (BaseEntity timestamps) |               |                                                |

Unique: `(MediaId, Source)`.

### `ExternalReview`

Per (source, source's external id) — dedupe by `ExternalRefId`.

| Column                  | Type          | Notes                                                                                                           |
| ----------------------- | ------------- | --------------------------------------------------------------------------------------------------------------- |
| Id                      | uuid PK       |                                                                                                                 |
| MediaId                 | uuid FK Media | indexed                                                                                                         |
| Source                  | varchar(20)   | `wikipedia_reception`, `letterboxd`                                                                             |
| ExternalRefId           | varchar(255)  | unique per source: `letterboxd:david.ehrlich:2024-05-12-dune-part-two` or `wikipedia:Dune_Part_Two_(2024_film)` |
| AuthorHandle            | varchar(50)?  | original handle (e.g. `davidehrlich`)                                                                           |
| AuthorUrl               | varchar(500)? | link to profile/article                                                                                         |
| Content                 | text          | full body (Wikipedia paragraph or Letterboxd review)                                                            |
| Rating                  | int?          | 1-10 if author rated it (Letterboxd has 5-star → ×2)                                                            |
| LikeCountOnSource       | int?          | for prioritization (Letterboxd has "likes")                                                                     |
| SourceUrl               | varchar(500)  | direct link to review on source                                                                                 |
| PublishedAt             | timestamp?    | when posted on source                                                                                           |
| FetchedAt               | timestamp     |                                                                                                                 |
| (BaseEntity timestamps) |               |                                                                                                                 |

Unique: `(Source, ExternalRefId)`.

### `ExternalFetchState`

Per media — drives TTL refresh schedule.

| Column                  | Type                 | Notes                               |
| ----------------------- | -------------------- | ----------------------------------- |
| Id                      | uuid PK              |                                     |
| MediaId                 | uuid FK Media UNIQUE | one row per media                   |
| FetchCount              | int                  | 0 on insert                         |
| LastFetchedAt           | timestamp?           | null until first fetch              |
| NextFetchDueAt          | timestamp?           | computed when LastFetchedAt updates |
| LastErrorAt             | timestamp?           | for backoff                         |
| LastError               | varchar(500)?        | last failure reason                 |
| (BaseEntity timestamps) |                      |                                     |

---

## TTL escalation

Same schedule for ratings + reviews (user explicitly merged them):

```
First fetch  → NextFetchDueAt = now + 24h
2nd  fetch   → NextFetchDueAt = now + 3d
3rd+ fetch   → NextFetchDueAt = now + 7d
```

Implementation: `NextFetchDueAt = now + ttlOf(FetchCount)` where `ttlOf(n)` = `n=1 → 24h, n=2 → 3d, n>=3 → 7d`.

---

## Sync strategy

### First hit on a media (no `ExternalFetchState` row)

- Backend: `GET /api/media/{id}` returns existing media payload **unchanged** — no blocking on external.
- Backend: `ExternalContentService.QueueIfMissingAsync(mediaId)` enqueues a background fetch (fire-and-forget via `Task.Run` + scoped service factory, since we already use scoped DbContext).
- Frontend: media detail page renders. Calls `GET /api/media/{id}/external` separately.
- That endpoint returns `{ status: "loading", ratings: [], reviews: [], wikiReception: null }` immediately if data isn't ready yet.
- Frontend shows skeleton loaders + status text "Завантажуємо зовнішні дані…".
- Frontend polls `/external` every 2 seconds until `status: "ready"`.

### Subsequent hits

- `GET /api/media/{id}/external` returns stale data immediately.
- If `NextFetchDueAt < now`, queue a refresh in the background.
- Increment `FetchCount`, set new `NextFetchDueAt` only after successful refresh.

### Top-50 cron

A hosted `ExternalContentRefreshService : BackgroundService` runs every 24h. Picks top-50 media by our `Review` count (excluding soft-deleted reviews) and refreshes their external data ahead of TTL. This keeps the "showcase" media always fresh.

---

## API

### `GET /api/media/{id:guid}/external`

Response:

```json
{
  "data": {
    "status": "ready" | "loading" | "error",
    "ratings": [
      { "source": "imdb", "score": 8.5, "rawScore": "8.5/10", "voteCount": 458102, "fetchedAt": "..." },
      { "source": "rotten_tomatoes", "score": 9.2, "rawScore": "92%", "voteCount": null, "fetchedAt": "..." }
    ],
    "wikiReception": {
      "content": "The film received critical acclaim...",
      "sourceUrl": "https://en.wikipedia.org/wiki/...",
      "fetchedAt": "..."
    },
    "reviews": [
      {
        "id": "...",
        "source": "letterboxd",
        "authorHandle": "davidehrlich",
        "authorUrl": "https://letterboxd.com/davidehrlich",
        "content": "...",
        "rating": 9,
        "likeCountOnSource": 245,
        "sourceUrl": "https://letterboxd.com/davidehrlich/film/dune-part-two/",
        "publishedAt": "..."
      }
    ]
  }
}
```

Backward-compat: existing `GET /api/media/{id}` unchanged.

---

## Letterboxd RSS strategy

Hardcoded list of ~30 prolific reviewer handles (`config.json` or static const):

```
davidehrlich, karstenrunquist, jaytalksfilm, danpacheco, lucy, schaffrillas,
samjuliano, kurtosis, joe, neithersnow, ScreenZealots, ...
```

Background sweep (independent of media-driven fetches):

- 1× per day, fetch each handle's RSS (`<channel><item>`).
- Parse `<title>` (movie name + year), `<description>` (HTML), `<letterboxd:rating>` (e.g. `4.5`), `<letterboxd:filmYear>`.
- For each item, try to match film against our Media table by title + year (fuzzy, ILIKE on Translations).
- Insert `ExternalReview` if `(Source, ExternalRefId)` not seen. ExternalRefId = `letterboxd:{handle}:{guid-from-item}`.

This is essentially a "feed pull" that opportunistically populates reviews for whichever media gets matched.

---

## Frontend

### Catalog (`/catalog`)

Each card:

- Existing: poster, title, year.
- Add: rating row.
  - Slot 1: **our** rating (e.g. "★ 7.5 / 105 reviews") — black-bordered, primary brand color.
  - Slot 2-3: up to 2 external ratings with **source icon + score** (e.g. tiny IMDb logo "8.5", tiny RT logo "92%").
  - Icons: SVG inline (IMDb yellow, RT red, Metacritic), small 16px.

### Media detail (`/media/[id]`)

Sections, top to bottom:

1. Poster + title + year + genres (existing).
2. **Ratings row** — our + each external rating with icon, score, vote count.
3. **Synopsis** (TMDB description, existing).
4. **Critical reception (Wikipedia)** — collapsible card. **Collapsed by default** (user explicitly asked to avoid spoilers). Click to expand.
5. **Reviews list** — our reviews (existing component), then external. **Not split into tabs.**
   - Prioritization: our (sorted by likes/recency) → external with `LikeCountOnSource >= threshold` → all other external.
   - Each external review card shows: source icon, badge "Letterboxd / Wikipedia / IMDb", author handle + link, content. Visually distinct (subtle background tint, gray border).
6. Existing tracking buttons / playlist / etc.

### Loading state

While `/external` returns `status: "loading"`:

- Skeleton boxes in the ratings row (3 grey pill placeholders).
- Skeleton box where Wikipedia reception would go (with text "Завантажуємо…").
- Skeleton card placeholders in reviews list with shimmer animation.

Animation: CSS-only shimmer, no external dependency. Tailwind utility:

```css
@keyframes shimmer { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
.shimmer { background: linear-gradient(90deg, #2a2a2a 25%, #3a3a3a 50%, #2a2a2a 75%); background-size: 200% 100%; animation: shimmer 1.5s infinite; }
```

---

## Source-attribution UI elements

- IMDb icon: yellow circle "IMDb" wordmark (use official press kit if licensable; otherwise textual "IMDb").
- RT icon: red tomato (Heroicons or text).
- Metacritic: green/yellow/red score badge.
- Letterboxd: orange/green/blue dot triplet.
- Wikipedia: bold "W" or icon.

For initial build, use **textual badges with brand colors** to avoid licensing pitfalls. Can swap to logos later if user supplies licensed assets.

---

## Files to create

```
Backend/track-list-api/
  Models/ExternalRating.cs
  Models/ExternalReview.cs
  Models/ExternalFetchState.cs
  DTOs/ExternalContentDto.cs
  DTOs/ExternalRatingDto.cs
  DTOs/ExternalReviewDto.cs
  Repository/
    IExternalRatingRepository.cs / ExternalRatingRepository.cs
    IExternalReviewRepository.cs / ExternalReviewRepository.cs
    IExternalFetchStateRepository.cs / ExternalFetchStateRepository.cs
    (register in UnitOfWork)
  Services/
    ExternalContentService.cs (+ IServices interface)
    External/OmdbClient.cs
    External/WikipediaClient.cs
    External/LetterboxdRssClient.cs
    ExternalContentRefreshService.cs (HostedService)
  Controllers/MediaController.cs — add endpoint
  DbContext/TrackListDbContext.cs — register DbSets + config
  Migrations/ — EF migration

Frontend/src/
  lib/api.ts — add getExternalContent fn
  lib/types/external.ts — DTOs
  lib/components/ExternalRatingsBar.svelte — rating row for media page
  lib/components/CatalogCard.svelte — extend with mini external ratings
  lib/components/WikiReceptionCard.svelte — collapsible
  lib/components/ExternalReviewCard.svelte — source-tagged review card
  lib/components/Shimmer.svelte — CSS skeleton
  routes/media/[id]/+page.server.ts — pass externalContent in load (initial loading state)
  routes/media/[id]/+page.svelte — wire up

Docs/external-reviews-plan.md — this file
Docs/external-reviews-progress.md — log

.env — add OMDB_API_KEY (gitignored)
```

---

## Phases

1. **OMDb key acquisition** — mail.tm temp inbox + form submit + key parse. Write to `.env`. **Commit:** `chore: add OMDB_API_KEY scaffolding (key in .env, gitignored)`.
2. **BE entities + migration** — add 3 entities, register in DbContext, migration. **Commit:** `feat(be): add ExternalRating/ExternalReview/ExternalFetchState entities`.
3. **BE clients** — OMDb, Wikipedia, Letterboxd RSS client classes with Result<T> pattern. **Commit:** `feat(be): add external content clients`.
4. **BE service + endpoint** — orchestrator service + controller endpoint + DTOs. **Commit:** `feat(be): GET /api/media/{id}/external with stale-while-revalidate`.
5. **BE background service** — refresh top-50 every 24h. **Commit:** `feat(be): nightly external refresh of top-50 hot media`.
6. **FE types + api wrapper** — TS interfaces + fetch fn. **Commit:** `feat(fe): wire external content api client`.
7. **FE catalog card** — add mini ratings. **Commit:** `feat(fe): catalog card external rating badges`.
8. **FE media page integration** — ratings row + collapsible Wiki + loading skeletons + review section. **Commit:** `feat(fe): external ratings + wiki reception + tagged review cards on media page`.
9. **e2e smoke test** — open known media in browser, verify all states. **Commit:** `docs: external reviews integration progress log`.

---

## Risks / open questions

- **OMDb temp-mail blocking**: if mail.tm domains are on a blocklist, fallback to Thunderbird mbox read or ask user. Mitigation: user offered FernDragonborn@gmail.com if needed.
- **Letterboxd UA filtering**: similar to Reddit, may 403 on bot UA. Mitigation: realistic UA + reasonable rate (1 RSS pull per handle per day = 30 requests/day total — well under any threshold).
- **Wikipedia title disambiguation**: e.g. "Dune" could map to many articles. Use TMDB's release year + media type to constrain. Use Wikipedia search API as fallback.
- **OMDb rate limit (1000/day)**: with 300 media in DB, full refresh = 300 requests. Spread across the day (cron at 4am, 1 req/sec).
- **License/attribution display**: Wikipedia content is CC BY-SA — must show "Source: Wikipedia" + link. Letterboxd: link back to original review. OMDb: footer credit.
- **Migration safety**: dev DB seeded with 2723 reviews. New tables are additive — no risk to existing data.

---

## Out of scope

- Modifying existing TMDB import path.
- Replacing/relabeling existing review rows.
- Currency / regional rating support.
- Comments on external reviews.
- Liking external reviews (they're read-only).
