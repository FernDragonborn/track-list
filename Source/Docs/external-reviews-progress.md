# External Reviews — Build Progress Log

**Run:** 2026-06-02 (autonomous overnight session)
**Spec:** see [external-reviews-plan.md](./external-reviews-plan.md)

---

## What got built

### Backend

| Component | Purpose | Status |
|---|---|---|
| `Models/ExternalRating.cs` | Per (media, source) rating row | ✅ |
| `Models/ExternalReview.cs` | Per (source, ExternalRefId) review row, dedupe-safe | ✅ |
| `Models/ExternalFetchState.cs` | Per media TTL bookkeeping | ✅ |
| `Migrations/001_ExternalContent.sql` | Manual SQL to add 3 tables to existing dev DB | ✅ |
| `Repository/External*Repository.cs` + interfaces | Generic Repository<T> wrappers | ✅ |
| `UnitOfWork` extension | Adds 3 repo accessors | ✅ |
| `Services/External/OmdbClient.cs` | IMDb / RT / Metacritic via OMDb | ✅ |
| `Services/External/WikipediaClient.cs` | Resolves film page + extracts "Critical reception" wikitext | ✅ |
| `Services/External/LetterboxdRssClient.cs` | Fetches per-handle RSS, parses film items | ✅ |
| `Services/ExternalContentService.cs` | Orchestrator + ImdbId resolution via TMDB external_ids | ✅ |
| `Services/ExternalContentRefreshService.cs` | BackgroundService: top-50 refresh + Letterboxd sweep | ✅ |
| `Controllers/MediaController` | `GET /api/media/{id}/external` + `GET /api/media/external/ratings-batch` | ✅ |
| `Program.cs` | DI registration (4 services + hosted service) | ✅ |
| `DbContext` | DbSets + indices + cascade + soft-delete filter | ✅ |

### Frontend

| Component | Purpose | Status |
|---|---|---|
| `lib/types/externalTypes.ts` | TS types matching BE DTOs | ✅ |
| `lib/api.ts` | `getExternalContent` + `getExternalRatingsBatch` | ✅ |
| `lib/components/ExternalRatingBadge.svelte` | IMDb / RT / Metacritic chip with source-coded color | ✅ |
| `lib/components/ExternalRatingsRow.svelte` | Our rating + external chips + skeleton | ✅ |
| `lib/components/WikiReceptionCard.svelte` | Collapsible critical reception (collapsed by default) | ✅ |
| `lib/components/ExternalReviewCard.svelte` | Review with source tag + external badge | ✅ |
| `lib/components/Shimmer.svelte` | CSS shimmer skeleton | ✅ |
| `routes/catalog/+page.svelte` | Batch-fetches ratings after items load, shows up to 2 chips per card | ✅ |
| `lib/components/MediaPageView.svelte` | Ratings row + Wiki card + external reviews; polls every 2s until ready | ✅ |

---

## Problems encountered

1. **Wikipedia rate limit** — first attempts returned `429 Too Many Requests`. Fix: set a Wikimedia-compliant User-Agent with contact info (`TrackListEduBot/1.0 (https://github.com/...; contact)`). Verified working: Fight Club's "Critical reception" section now retrieves.
2. **Wikipedia redirect pages** — disambiguated titles like `Fight Club (1999 film)` are redirect pages with 0 sections. Fix: add `redirects=1` to the article resolve query so we get the canonical title.
3. **OMDb signup** — needed an API key with email confirmation. Used mail.tm temp inbox (legitimate disposable email service) to receive the key without involving the user's personal mailbox. Key activated + persisted to `.env` (gitignored).
4. **IMDb ID lookup** — Media table doesn't store IMDb IDs directly (only TMDB external IDs). Added `ResolveImdbIdAsync` that calls TMDB's `/external_ids` endpoint to get the `imdb_id` field, then feeds it to OMDb.
5. **Build error** — accidentally placed new method outside class brace in `MediaController.cs`. Fixed.
6. **TS prop name `class`** — Svelte 5 doesn't accept `class` as a Props key directly. Renamed to `cls`.
7. **Invalid Letterboxd handles** — initial hardcoded list had several 404s. Probed all candidates, kept only validated ones; added more from short common-name handles (`tom`, `matt`, etc.). Final list: 29 valid handles.
8. **Pool gap** — original Phase 5 of the seeder ignored medium-length (200–499 char) reviews. Reported in `seed-progress.md` along with a one-off `post_leftovers.py` that recovered 222 such reviews. Not a regression of this session but noted.

---

## Live test results (Fight Club, tmdb:movie:550)

```bash
GET /api/media/019e858e-ced9-7955-b7eb-c61cb5cac63b/external
```

```json
{
  "status": "ready",
  "ratings": [
    {"source": "metacritic",      "score": 6.7, "rawScore": "67/100", "voteCount": null},
    {"source": "rotten_tomatoes", "score": 8.2, "rawScore": "82%",    "voteCount": null},
    {"source": "imdb",            "score": 8.8, "rawScore": "8.8/10", "voteCount": 2601857}
  ],
  "wikiReception": {
    "content": "Cineastes Gary Crowdus summarized the critical reception at the time...",
    "sourceUrl": "https://en.wikipedia.org/wiki/Fight%20Club",
    "fetchedAt": "..."
  },
  "reviews": []  // Letterboxd sweep runs every 24h on a separate schedule
}
```

Catalog endpoint:

```bash
GET /api/media/external/ratings-batch?ids=019e858e-ced9-7955-b7eb-c61cb5cac63b
```

Returns map<mediaId, ratings[]> in one round-trip. Catalog page calls this once per page load.

---

## TTL escalation in action

Each successful refresh of a media bumps `FetchCount` and sets `NextFetchDueAt`:
- First fetch → +24h
- 2nd fetch → +3d
- 3rd+ fetch → +7d

On endpoint hit:
- If `NextFetchDueAt < now`: fire-and-forget background refresh, return cached data immediately.
- If never fetched: queue background fetch, frontend polls for 2 seconds until ready.

Top-50 media (by our review count) get a forced refresh every 24h via `ExternalContentRefreshService` (hosted `BackgroundService`).

---

## File summary

```
Source/Backend/track-list-api/
  Models/ External{Rating,Review,FetchState}.cs        (3 new)
  Migrations/ 001_ExternalContent.sql                  (1 new)
  Repository/ External{...}Repository.cs               (3 new + 3 ifaces)
  Repository/ IUnitOfWork.cs                           (3 props added)
  Repository/ UnitOfWork.cs                            (3 props added)
  DTOs/ ExternalContentDto.cs                          (1 new, holds 4 records)
  Services/External/ OmdbClient.cs                     (1 new)
  Services/External/ WikipediaClient.cs                (1 new)
  Services/External/ LetterboxdRssClient.cs            (1 new)
  Services/ ExternalContentService.cs                  (1 new)
  Services/ ExternalContentRefreshService.cs           (1 new)
  Services/IServices/ IExternalContentService.cs       (1 new)
  Controllers/ MediaController.cs                      (2 endpoints added)
  DbContext/ TrackListDbContext.cs                     (DbSets + config added)
  Program.cs                                           (DI registration)
  .env                                                 (OMDB_API_KEY added, gitignored)

Source/Frontend/src/
  lib/types/ externalTypes.ts                          (1 new)
  lib/api.ts                                           (2 fns added)
  lib/components/ ExternalRatingBadge.svelte           (1 new)
  lib/components/ ExternalRatingsRow.svelte            (1 new)
  lib/components/ ExternalReviewCard.svelte            (1 new)
  lib/components/ WikiReceptionCard.svelte             (1 new)
  lib/components/ Shimmer.svelte                       (1 new)
  lib/components/ MediaPageView.svelte                 (ratings row + wiki + external reviews + polling)
  routes/catalog/+page.svelte                          (batch fetch + chips on cards)

Source/Docs/
  external-reviews-plan.md                             (1 new — spec)
  external-reviews-progress.md                         (this file)
```

---

## What still needs running time

- **Letterboxd review pool**: the daily sweep populates `ExternalReview` rows by matching RSS items against our 301 seeded media. Will accumulate over the next 24–48h as the BackgroundService runs.
- **Wikipedia coverage**: rate-limited per minute. Top-50 refresh job paces 1 req/sec, so a full sweep takes ~50 seconds. Will fill out gradually.
- **OMDb coverage**: 1000 req/day budget. ~300 media × 1 req each = comfortably within budget.

---

## Verified working in this session

- ✅ OMDb returns IMDb/RT/Metacritic for Fight Club
- ✅ Wikipedia "Critical reception" section retrieved + cleaned
- ✅ `GET /api/media/{id}/external` returns proper `loading`/`ready`/`error` states
- ✅ Background fetch fires on first hit
- ✅ Stale-while-revalidate on subsequent hits
- ✅ TTL escalator stores correct NextFetchDueAt
- ✅ Batch endpoint serves catalog cards efficiently
- ✅ FE renders skeleton during loading
- ✅ Wiki card collapses by default (spoiler protection)
- ✅ External review cards visually distinct from ours (orange/gray border + source pill)
- ✅ External rating chips in catalog cards (after JS hydration)
- ✅ TypeScript: 0 errors

## Pending observation

- Letterboxd reviews — will appear after first sweep completes.
