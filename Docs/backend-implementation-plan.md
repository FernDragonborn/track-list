# Backend Implementation Plan

Remaining work aligned with waterfall. Epics 1, 2, 5, 9 complete. BE-402 (Sprint 4) done.

---

## Sprint 4 — DONE

| Task | Status |
|------|--------|
| BE-401 Entities (Review, Comment, ReviewLike) | Done (models existed) |
| BE-402 Review CRUD + Likes + Comments API | Done |
| BE-501 Tracking (Upsert) | Done (was already implemented) |

---

## Sprint 5 — Collections (BE-801 → BE-804)

Models exist: `Playlist`, `PlaylistItem`, `PlaylistAccess`. Repos wired in UnitOfWork. No controller/service.

### BE-801: Collection Entities & Access Service
- Add global query filters for Playlist, PlaylistItem (soft delete)
- Create `ICollectionAccessService` with `CanView(userId, playlistId)`, `CanEdit(userId, playlistId)`
- Logic: owner always can, public playlists viewable by all, `PlaylistAccess` table for granular sharing

### BE-802: Collection CRUD
- `CollectionController` at `api/collections`
- `POST /api/collections` — create (owner = current user)
- `PUT /api/collections/{id}` — update name/description/privacy
- `DELETE /api/collections/{id}` — soft delete (owner only)
- All writes check `CanEdit`

### BE-803: Sharing Endpoints
- `POST /api/collections/{id}/access` — grant access to user
- `DELETE /api/collections/{id}/access/{userId}` — revoke
- `GET /api/collections/{id}/access` — list who has access
- Owner only for all three

### BE-804: Collection Items
- `POST /api/collections/{id}/items` — add media
- `DELETE /api/collections/{id}/items/{itemId}` — remove media
- `GET /api/collections/{id}` — return collection with items (check `CanView`)

**Files:** `Services/IServices/ICollectionService.cs`, `Services/CollectionService.cs`, `Controllers/CollectionController.cs`, DTOs, Program.cs DI

---

## Sprint 6 — Moderation (BE-601 → BE-603)

ReportController exists with basic CRUD. Gaps: content soft-delete on resolve, dedicated moderation queue, translation moderation endpoints.

### BE-601: Reporting System — PARTIAL (exists)
- `POST /api/report` exists
- Need: expand `ReportStatus` enum to `Pending`, `Resolved_Deleted`, `Resolved_Dismissed`

### BE-602: Moderation Dashboard API
- `ModerationController` at `api/moderation`
- `GET /api/moderation/reports` — filter by Pending, paginated (Moderator/Admin)
- `POST /api/moderation/reports/{id}/resolve` — soft-delete target content + update report status
- `POST /api/moderation/reports/{id}/dismiss` — reject report, update status
- Set `ProcessedByUserId` on resolution

### BE-603: Translation Moderation API
- `GET /api/moderation/translations` — pending translations queue
- `POST /api/moderation/translations/{id}/approve`
- `POST /api/moderation/translations/{id}/reject`
- Currently in MediaController — move to ModerationController or keep and add queue endpoint

**Files:** `Controllers/ModerationController.cs`, `Services/ModerationService.cs`, update `ReportStatus` enum, update `ReportController` if needed

---

## Sprint 7 — Admin (BE-701 → BE-703)

### BE-701: User Management API — DONE (scattered)
- `GET /api/profiles` (admin, paginated) — exists in UserController
- `PUT /api/profiles/updateRole` — exists
- `DELETE /api/profiles/{username}` — exists
- Decision: keep in UserController or consolidate into AdminController

### BE-702: Statistics Aggregation — NOT IMPLEMENTED
- `GET /api/admin/stats` — new endpoint
- Queries: COUNT users by date range, COUNT reviews by date, COUNT media by type
- GROUP BY date (daily/weekly/monthly)
- CSV export: generate in-memory, return as `FileResult`
- Optional: XLSX via ClosedXML or similar package

### BE-703: Global Media Management — DONE (scattered)
- `PUT /api/media/translations/{id}` — exists in MediaController
- `DELETE /api/media/{id}` — exists in MediaController

**New files:** `Controllers/AdminController.cs` (stats only), `Services/StatisticsService.cs`

---

## Sprint 8 — Polish

### BE-004: Serilog Structured Logging
- Replace built-in logging with Serilog
- Configure sinks: Console (structured JSON) + File (rolling)
- Add request correlation IDs

### BE-005: Global Exception Handler
- Middleware that catches unhandled exceptions
- Returns consistent JSON error response `{ error: "..." }`
- Logs exception details server-side
- No stack traces in production responses

---

## Epic 3 — Feed (depends on BE-402 ✅)

### FeedController at `api/feed`
- `GET /api/feed/personal` — reviews from followed users, paginated, chronological
  - JOIN Reviews + Follows WHERE follower = current user
  - Include: isLikedByMe, topComment (highest-liked), like/comment counts
- `GET /api/feed/global` — all reviews, paginated
  - Same shape, no follow filter

**Complexity:** Most complex query in the system. Consider raw SQL or EF LINQ with careful Include/projection.

**Files:** `Controllers/FeedController.cs`, `Services/FeedService.cs`

---

## Priority Order

1. **Sprint 5** (Collections) — FE needs it next
2. **Epic 3** (Feed) — depends on BE-402 (done), high user value
3. **Sprint 6** (Moderation) — consolidate existing + add missing
4. **Sprint 7** (Statistics) — only BE-702 is new work
5. **Sprint 8** (Serilog + Exception Handler) — polish before deploy
