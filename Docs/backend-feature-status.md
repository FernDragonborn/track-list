# Backend Feature Implementation Status

Analysis of 9 feature epics against actual backend code.
**Last updated:** 2026-04-18

**Controllers:** AuthController, UserController, MediaController, TrackingStatusController, ReportController, ReviewController
**Missing controllers:** FeedController, CollectionController, AdminController (stats only), ModerationController

---

## Epic 1: Authentication (@epic1) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-101 | Registration | DONE | `POST /api/profiles/register` |
| US-101 | Reserved usernames | DONE | `RegisterRequestValidator` blocks admin, moderator, etc. |
| US-101 | Password policy (BRL-2) | DONE | 8+ chars, requires letter + digit |
| US-102 | Login | DONE | `POST /api/auth/login` |
| US-103 | Logout | N/A | Stateless JWT — client-side |
| US-104 | Token refresh | DONE | `POST /api/auth/renewToken` |
| US-104 | RBAC policies | DONE | adminPolicy, moderatorPolicy, userPolicy (fixed — were all "adminPolicy") |
| US-105 | API validation | DONE | FluentValidation |

## Epic 2: Profile (@epic2) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-201 | View own profile | DONE | `GET /api/profiles/{username}` |
| US-201 | Edit own profile | DONE | `PUT /api/profiles/me` → 204 |
| US-202 | View public profile | DONE | Works for guests (JWT optional) |
| US-203 | Follow/Unfollow | DONE | `POST/DELETE /api/profiles/{username}/follow` → 204 |
| US-203 | Self-follow prevention | DONE | UserService checks IDs |
| US-204 | Admin: paginated user list | DONE | `GET /api/profiles` (Admin only, PageSize ≤ 100) |
| US-204 | Admin: edit other user | DONE | `PUT /api/profiles/me` with admin override |
| US-205 | Admin: change role | DONE | `PUT /api/profiles/updateRole` → 204 |
| US-205 | Self-demotion prevention | DONE | UserService checks changerEmail |
| US-205 | Admin: delete user | DONE | `DELETE /api/profiles/{username}` and `/id/{userId}` → 204 |
| US-206 | Change password | DONE | `PUT /api/profiles/me/password` → 204 |
| US-206 | Admin: reset password | DONE | `POST /api/profiles/{userId}/reset-password` |
| US-207 | Upload avatar | DONE | `POST /api/profiles/me/redactpfp` [FromForm], sets ProfilePicUrl |
| US-207 | Download avatar | DONE | `GET /api/profiles/avatar/{fileName}` |
| US-207 | Delete avatar | DONE | `DELETE /api/profiles/me/avatar` → 204 |

## Epic 3: Feed (@epic3) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-301 | Personal feed | DONE | `GET /api/feed/personal` [Authorize] — reviews from followed users |
| US-302 | Global feed | DONE | `GET /api/feed/global` — all reviews, newest first, guests OK |
| US-303 | Like review from feed | DONE | Uses existing `POST .../reviews/{id}/like` (Epic 4) |
| US-304 | Top comment in feed | DONE | Each feed item includes top (most-liked) level-0 comment |

## Epic 4: Media Page (@epic4) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-401 | View media page | DONE | `GET /api/media/{id}` with translation support |
| US-402 | Create review | DONE | `POST /api/media/{mediaId}/reviews` [Authorize] |
| US-402 | BRL-4: one per user per media | DONE | Unique index (UserId, MediaId) + service check |
| US-403 | Like/unlike review | DONE | `POST .../reviews/{reviewId}/like` (toggle) |
| US-404 | Comments (level 0) | DONE | `POST .../reviews/{reviewId}/comments` |
| US-405 | Reply (level 1) | DONE | Set `ParentCommentId` in request |
| US-406 | Block level 2+ nesting | DONE | Service rejects if parent already has parent |
| US-407 | Guest restrictions | DONE | [Authorize] on all write endpoints, GET is public |
| US-408 | Translation suggestions | DONE | `POST /api/media/{mediaId}/translations` [Authorize] |

## Epic 5: Tracking (@epic5) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-501 | Upsert tracking status | DONE | `POST /api/TrackingStatus` |
| US-501 | BRL-3: one per user+media | DONE | Composite key |
| US-502 | Progress tracking | DONE | Progress field in entity |
| US-502 | Get tracking for media | DONE | `GET /api/TrackingStatus/{mediaId}` |
| US-502 | Get all tracking statuses | DONE | `GET /api/TrackingStatus` |
| US-502 | Delete tracking status | DONE | `DELETE /api/TrackingStatus/{mediaId}` (was broken — fixed) |

## Epic 6: Moderation (@epic6) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-601 | Create report | DONE | `POST /api/report` |
| US-602 | View reports | DONE | `GET /api/report` (Admin/Moderator, filter by status) |
| US-602 | Resolve report (delete content) | DONE | `POST /api/report/{id}/resolve` — soft-deletes target on ResolvedDeleted |
| US-602 | Report status ENUM | DONE | Pending / ResolvedDeleted / ResolvedDismissed |
| US-603 | Translation moderation | DONE | `GET /api/moderation/translations` (queue) + `POST .../translations/{id}/status` (approve/reject) |

## Epic 7: Admin (@epic7) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-701 | Change user role | DONE | `PUT /api/profiles/updateRole` |
| US-701 | Soft-delete user | DONE | `DELETE /api/profiles/{username}` |
| US-702 | Edit translations | DONE | `PUT /api/media/translations/{id}` [Moderator+] |
| US-702 | Soft-delete media | DONE | `DELETE /api/media/{id}` [Admin] |
| US-703 | Statistics | DONE | `GET /api/admin/stats` (users, media, reviews, reports, etc.) |
| US-703 | CSV export | DONE | `GET /api/admin/export/users.csv` (per-user stats w/ BOM) |

## Epic 8: Collections (@epic8) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-801 | Create collection | DONE | `POST /api/collections` |
| US-802 | Add/remove media | DONE | `POST/DELETE /api/collections/{id}/items` |
| US-803 | Privacy levels | DONE | Public/Private enum, owner sees all, others see public only |
| US-804 | Granular access | DONE | `POST/DELETE /api/collections/{id}/access` (Google Docs style sharing) |
| US-805 | Soft-delete collection | DONE | `DELETE /api/collections/{id}` (owner or admin) |

## Epic 9: Search (@epic9) - DONE

| US | Feature | Status | Notes |
|----|---------|--------|-------|
| US-901 | Search local DB + TMDB | DONE | `GET /api/media/search?query=` |
| US-902 | On-demand caching | DONE | `GET /api/media/{id}` supports "Tmdb:movie:123" |
| US-902 | Auto-create translations | DONE | TmdbService fetches + stores |

---

## Summary

| Epic | Name | Status |
|------|------|--------|
| 1 | Authentication | **DONE** |
| 2 | Profile | **DONE** |
| 3 | Feed | **DONE** |
| 4 | Media Page | **DONE** |
| 5 | Tracking | **DONE** |
| 6 | Moderation | **DONE** |
| 7 | Admin | **DONE** |
| 8 | Collections | **DONE** |
| 9 | Search | **DONE** |

## Bugs Fixed This Session

| Bug | Description | Fix |
|-----|-------------|-----|
| CRITICAL | IdentityData: all 3 policies = "adminPolicy" (any user = admin) | Unique policy names |
| CRITICAL | TrackingStatus DELETE used [HttpGet], no [Authorize], Reqnroll import | Full rewrite |
| CRITICAL | Password change logic inverted | Removed `!` from condition |
| HIGH | MediaController: 6 write endpoints had no [Authorize] | Added Admin/Moderator/User auth |
| HIGH | Public profile endpoint required JWT | Made JWT optional |
| HIGH | Avatar upload used [FromBody] instead of [FromForm] | Changed binding |
| HIGH | Inconsistent response format across all controllers | Standardized: 200+{data}, 204 for mutations, 400+{error} |
| MEDIUM | SaveAvatarAsync never set User.ProfilePicUrl | Added update after save |
| MEDIUM | Registration: no reserved usernames, no password complexity | Added validator rules |

## Remaining Work

None — all epics and infra complete.

## Infrastructure (added)

| Feature | Status | Notes |
|---------|--------|-------|
| Structured logging | DONE | Serilog → console + daily rolling file (`logs/api-.log`) |
| Global exception handler | DONE | Middleware catches unhandled exceptions → 500 `{error}`, logs via Serilog |
