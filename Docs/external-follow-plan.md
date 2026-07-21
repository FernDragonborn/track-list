# Follow external reviewers + mix their reviews into personal feed

## Context

`ExternalReviewer` already lives as its own table, with virtual-profile
endpoints and a working external-only feed at `/external-feed`. The user
wants the next step: a "follow" button on the external-reviewer profile,
and external reviews to show up in the **personal feed** alongside
reviews from followed TrackList users.

This must NOT touch the existing `User.Follow` graph or the `UserRole`
enum (see prior discussion — auth-surface expansion is the failure
mode). The cleanest cut is a parallel table that links a real
`User.Id` to an `ExternalReviewer.Id`, independent of the user↔user
follow path.

The current `FeedService` is offset-paged
(`Backend/Services/FeedService.cs:9-54`, `MapToFeedItems` lines 82-178).
The existing `ExternalReviewerService.GetGlobalFeedAsync`
(`Services/ExternalReviewerService.cs:131-155`) is cursor-paged and
returns its own `ExternalReview` shape. The integration point is the
feed: union the two streams, sort by their respective "happened at"
timestamps, then page.

## Backend Changes

### 1. New entity `ExternalReviewerFollow`

`Backend/track-list-api/Models/ExternalReviewerFollow.cs`:

```csharp
public class ExternalReviewerFollow
{
    public Guid Id { get; set; }
    public Guid FollowerUserId { get; set; }
    public virtual User? Follower { get; set; }
    public Guid ExternalReviewerId { get; set; }
    public virtual ExternalReviewer? ExternalReviewer { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

Not derived from `BaseEntity` — no soft-delete, no `UpdatedAt`. Matches
the simpler shape of a join table; mirrors the **physical-delete**
behavior of `Follow` (audited at `FeedService` / `UserService.UnfollowUserAsync:464-485`).

### 2. DbContext config

`Backend/track-list-api/DbContext/TrackListDbContext.cs`:

```csharp
public DbSet<ExternalReviewerFollow> ExternalReviewerFollows { get; set; } = null!;

modelBuilder.Entity<ExternalReviewerFollow>()
    .HasIndex(e => new { e.FollowerUserId, e.ExternalReviewerId })
    .IsUnique();
modelBuilder.Entity<ExternalReviewerFollow>()
    .HasOne(e => e.Follower).WithMany()
    .HasForeignKey(e => e.FollowerUserId)
    .OnDelete(DeleteBehavior.Cascade);
modelBuilder.Entity<ExternalReviewerFollow>()
    .HasOne(e => e.ExternalReviewer).WithMany()
    .HasForeignKey(e => e.ExternalReviewerId)
    .OnDelete(DeleteBehavior.Cascade);
```

### 3. Schema-bridge migration

`Backend/track-list-api/Configure.cs` — extend
`EnsureExternalReviewerSchema()` (the existing idempotent raw-SQL bridge
that handles SQLite-without-EF-migrations) to also:

```sql
CREATE TABLE IF NOT EXISTS ExternalReviewerFollows (
    Id TEXT NOT NULL CONSTRAINT PK_ExternalReviewerFollows PRIMARY KEY,
    FollowerUserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    ExternalReviewerId TEXT NOT NULL REFERENCES ExternalReviewers(Id) ON DELETE CASCADE,
    CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_ExternalReviewerFollows_Follower_Reviewer
    ON ExternalReviewerFollows (FollowerUserId, ExternalReviewerId);
```

### 4. Repository + UoW

- `Repository/IReposotory/IExternalReviewerFollowRepository.cs` — extends `IRepository<ExternalReviewerFollow>`.
- `Repository/ExternalReviewerFollowRepository.cs` — concrete.
- Register in `IUnitOfWork` + `UnitOfWork.cs`.

### 5. Service methods on `IExternalReviewerService`

Add to `Services/IServices/IExternalReviewerService.cs`:

```csharp
Task<bool> FollowAsync(Guid userId, string source, string handle, CancellationToken ct);
Task<bool> UnfollowAsync(Guid userId, string source, string handle, CancellationToken ct);
Task<HashSet<Guid>> GetFollowedReviewerIdsAsync(Guid userId, CancellationToken ct);
```

`FollowAsync`: ensure reviewer exists (`GetOrCreateAsync`), insert row
unless `(FollowerUserId, ExternalReviewerId)` already exists. Returns
`true` if newly created, `false` if already followed.
`UnfollowAsync`: hard-delete by composite key, return whether a row was
removed.
`GetFollowedReviewerIdsAsync`: returns the set used by FeedService.

Wire `isFollowing` into the existing `ExternalReviewerProfileDto` —
populate when the caller passes the current user id (mirrors how
`UserService.GetUserByUsernameAsync:96-107` sets `IsFollowing` on
`ProfileDto`). `ExternalReviewerService.GetProfileAsync` gains a
`Guid? viewerUserId` parameter.

### 6. Controller endpoints

In `Controllers/ExternalReviewerController.cs` (existing):

```csharp
[HttpPost("{handle}/follow")][Authorize]
public Task<ObjectResult> Follow(string handle, [FromQuery] string source = "letterboxd", CancellationToken ct = default);

[HttpDelete("{handle}/follow")][Authorize]
public Task<ObjectResult> Unfollow(string handle, [FromQuery] string source = "letterboxd", CancellationToken ct = default);
```

`GET /api/external-reviewers/{handle}` — change to read the bearer's
user id (`User.FindFirst(ClaimTypes.NameIdentifier)` via existing
`JwtHandler` helper) and pass through to the service so the response
contains `isFollowing`.

### 7. FeedService — mix external into personal feed

`Backend/Services/FeedService.cs:9-36` — `GetPersonalFeedAsync` today
pulls reviews where `r.UserId IN followedUserIds`. Add a parallel
query:

```csharp
var followedExternalIds = await uow.ExternalReviewerFollowRepository
    .GetAsync(f => f.FollowerUserId == userId);
var externalReviews = await dbContext.ExternalReviews
    .Include(r => r.ExternalReviewer)
    .Include(r => r.Media!).ThenInclude(m => m.Translations)
    .Where(r => followedExternalIds.Select(x => x.ExternalReviewerId).Contains(r.ExternalReviewerId!.Value))
    .ToListAsync(ct);
```

Map both into a single result list with a discriminator and merge-sort
by "when did this happen on the wire" — `CreatedAt` for internal,
`PublishedAt ?? FetchedAt` for external (the same fallback
`ExternalReviewerService` already uses, lines 84, 116-118).

### 8. DTO: `FeedMixedItemDto`

`Backend/track-list-api/DTOs/FeedDto.cs` — new wrapper:

```csharp
public class FeedMixedItemDto
{
    public string Kind { get; set; } = "";  // "internal" | "external"
    public FeedItemDto? Internal { get; set; }
    public ExternalReviewFeedItemDto? External { get; set; }
    public DateTime SortAt { get; set; }     // resolved timestamp for FE display
}
```

`ExternalReviewFeedItemDto` already exists
(`DTOs/ExternalReviewerDto.cs`) and already carries reviewer +
mediaTitle/poster — reuse.

`GetPersonalFeedAsync` now returns `List<FeedMixedItemDto>` (paged).
Keep `GetGlobalFeedAsync` reviews-only — global feed mixing is
optional; the user's request was "personal feed" specifically.

## Frontend Changes

### 1. Reuse FollowButton

`Frontend/src/lib/components/FollowButton.svelte` — make it
source-aware via one new optional prop:

```ts
interface Props {
  username: string;             // present in user-follow mode
  externalHandle?: string;      // present in external-follow mode
  externalSource?: string;      // default 'letterboxd'
  initialIsFollowing: boolean;
  token: string | null;
}
```

Branch on `externalHandle` for the API path; the rest of the
state-machine + UI (optimistic toggle, hover-to-show-Unfollow, spinner)
is identical to the current `FollowButton`. Avoids forking a second
component. Audit confirmed the visible behavior matches what
external-reviewer profiles need.

`Frontend/src/lib/api.ts` — add:

```ts
followExternalReviewer(handle, source='letterboxd', token): POST /api/external-reviewers/{handle}/follow?source=…
unfollowExternalReviewer(handle, source='letterboxd', token): DELETE …
```

### 2. Drop the button onto the external-reviewer profile

`Frontend/src/routes/external-reviewers/[handle]/+page.svelte` —
currently the page banner explicitly says "підписатись не можна". Flip
that: render `<FollowButton externalHandle={…} initialIsFollowing={data.profile.isFollowing} token={…}>` in the same place the username profile shows it. Drop the "підписатись не можна" line; keep the disclaimer that this is not a TrackList account (no DMs, no reports).

`+page.server.ts` — pass the JWT cookie through to the
`getExternalReviewerProfile` call so the BE can compute `isFollowing`.

### 3. Render mixed feed items

`Frontend/src/routes/+page.svelte` — feed items become
`FeedMixedItemDto`. One render switch:

```svelte
{#each items as item (item.kind === 'internal' ? item.internal.reviewId : item.external.id)}
  {#if item.kind === 'internal'}
    <FeedCard item={item.internal} ... />
  {:else}
    <ExternalReviewCard
      review={externalReviewFromFeedItem(item.external)}
      mediaLink={{ href: `/media/${item.external.mediaId}`, title: item.external.mediaTitle, year: item.external.mediaReleaseYear, posterUrl: item.external.mediaPosterUrl }}
    />
  {/if}
{/each}
```

`ExternalReviewCard` already accepts `mediaLink` (added in the recent
external-pages refactor), so no component changes here. The
`externalReviewFromFeedItem` shim is the same projection
`/external-feed/+page.svelte` already does — lift it from there into
`src/lib/utils/feed.ts` so both pages share it.

`Frontend/src/routes/+page.server.ts` — change `api.getPersonalFeed`
return shape from `FeedItemDto[]` to `FeedMixedItemDto[]`. Same
endpoint path; only the DTO shape changes.

### 4. Type definitions

`Frontend/src/lib/types/reviewTypes.ts` — add `FeedMixedItem`
discriminated union mirroring the new BE DTO.

## Out of Scope

- **Mixing into the global feed.** The user asked for the personal feed
  specifically. Global mixing adds noise without the "I chose to see
  this critic" signal that personal follow provides.
- **Notifications when a followed external critic posts.** No
  notification subsystem exists for internal follows either.
- **Showing follower count on the external-reviewer profile.**
  Possible later; not requested.
- **Restoring soft-deleted follows like `User.Follow` does.** Hard
  delete is enough — re-following is a fresh insert.
- **Sharing one polymorphic `Follow` table for both kinds.** Already
  rejected in the prior architecture discussion; FK shape mismatch +
  RBAC-leakage risk.

## Files Modified

**New (5):**
- `Backend/track-list-api/Models/ExternalReviewerFollow.cs`
- `Backend/track-list-api/Repository/IReposotory/IExternalReviewerFollowRepository.cs`
- `Backend/track-list-api/Repository/ExternalReviewerFollowRepository.cs`
- `Frontend/src/lib/utils/feed.ts` (the `externalReviewFromFeedItem` shim)

**Modified (10):**
- `Backend/track-list-api/DbContext/TrackListDbContext.cs` — DbSet + index
- `Backend/track-list-api/Configure.cs` — schema bridge for the new table
- `Backend/track-list-api/Repository/IReposotory/IUnitOfWork.cs` — repo accessor
- `Backend/track-list-api/Repository/UnitOfWork.cs` — wire it
- `Backend/track-list-api/Services/IServices/IExternalReviewerService.cs` — three new methods + `viewerUserId` on `GetProfileAsync`
- `Backend/track-list-api/Services/ExternalReviewerService.cs` — impls
- `Backend/track-list-api/Services/FeedService.cs` — merge external into `GetPersonalFeedAsync`
- `Backend/track-list-api/DTOs/FeedDto.cs` — `FeedMixedItemDto`
- `Backend/track-list-api/DTOs/ExternalReviewerDto.cs` — `isFollowing` on `ExternalReviewerProfileDto`
- `Backend/track-list-api/Controllers/ExternalReviewerController.cs` — follow/unfollow endpoints + thread current user id
- `Frontend/src/lib/api.ts` — two helpers
- `Frontend/src/lib/types/reviewTypes.ts` — discriminated union
- `Frontend/src/lib/components/FollowButton.svelte` — accept `externalHandle`
- `Frontend/src/routes/external-reviewers/[handle]/+page.svelte` — render the button, drop the "не можна" line
- `Frontend/src/routes/external-reviewers/[handle]/+page.server.ts` — pass token through
- `Frontend/src/routes/+page.svelte` — kind-switch render
- `Frontend/src/routes/+page.server.ts` — adopt the new DTO shape
- `Frontend/src/routes/external-feed/+page.svelte` — adopt the shared `externalReviewFromFeedItem` shim

## Verification

```bash
cd Source
docker compose -f docker-compose.dev.yaml up -d --build dev-api
until curl -s -o /dev/null -w "%{http_code}" http://localhost/api/stats/public | grep -q 200; do sleep 2; done

# Login as a seed user, capture token.
TOKEN=$(curl -s -X POST http://localhost/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"perfuser1@test.com","username":"","password":"PerfTest123!"}' \
  | python -c "import sys,json; print(json.load(sys.stdin)['data']['accessToken'])")

# Follow davidehrlich
curl -s -X POST -H "Authorization: Bearer $TOKEN" \
  "http://localhost/api/external-reviewers/davidehrlich/follow"

# Profile now reports isFollowing=true
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost/api/external-reviewers/davidehrlich" | jq '.data.isFollowing'

# Personal feed contains at least one item with kind='external' and authorHandle='davidehrlich'
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost/api/feed/personal?pageNumber=1&pageSize=20" \
  | jq '.data.items[] | select(.kind=="external") | .external.authorHandle' | sort -u

# Unfollow
curl -s -X DELETE -H "Authorization: Bearer $TOKEN" \
  "http://localhost/api/external-reviewers/davidehrlich/follow"

# isFollowing back to false; personal feed no longer mixes davidehrlich
```

Browser smoke:
1. Log in as `perfuser1`.
2. Open `/external-reviewers/davidehrlich`. Click "Підписатися". Banner
   transitions to "Підписаний"; profile banner stays correct.
3. Open `/`. Find davidehrlich's MOTU 2026 review interleaved with
   internal reviews from followed users, sorted by date desc.
4. Unfollow on profile. Reload `/`. External item is gone.
5. `dotnet test` for the BE tests — no regressions.

## Suggested Commits

1. `feat(be): ExternalReviewerFollow table + follow/unfollow endpoints`
2. `feat(be): mix followed external reviews into personal feed (DTO discriminator)`
3. `feat(fe): FollowButton supports external handles; profile renders it`
4. `feat(fe): personal feed renders mixed internal/external items`
