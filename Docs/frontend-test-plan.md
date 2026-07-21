# Frontend Test Plan

## Context

FE = SvelteKit 2 + Svelte 5 + TypeScript. 10 shared BDD features + 1 FE-specific. Backend already has full test coverage (controller + service + BDD). This plan covers FE-side: unit tests (Vitest), E2E tests (Cucumber + Playwright), matching all feature files.

## Current State (updated 2026-04-26)

| Layer | Status | Count |
|-------|--------|-------|
| Unit (Vitest) | **Done** | 105 tests across 8 files |
| Component (Vitest+STL) | **Done** | 20 tests (Header: 10, Footer: 5, Error: 5) |
| E2E infra | **Done** | Cucumber + Playwright + world/hooks/steps |
| E2E steps | **Improved** | ~50 step defs, ~60% real assertions, ~40% stubs (blocked by WIP routes) |
| Feature files | **11 files** | 1-auth, 2-profile, 3-feed, 4-media, 5-tracking, 6-moderation, 7-admin, 8-collections, 9-search, 10-reviews, 11-auth-e2e |

### All Test Files (125 total)

| File | Tests | Type |
|------|-------|------|
| `validation.test.ts` | 22 | Unit — form validation |
| `claims.test.ts` | 11 | Unit — JWT claim mapping |
| `api-helpers.test.ts` | 14 | Unit — API URL/response helpers |
| `storage.test.ts` | 9 | Unit — localStorage wrapper + SSR |
| `auth.test.ts` | 13 | Unit — server auth (JWT, refresh, cookies) |
| `user.svelte.test.ts` | 9 | Unit — Svelte 5 $state store |
| `hooks.server.test.ts` | 10 | Unit — auth middleware + route protection |
| `api.test.ts` | 11 | Unit — API client (GET/POST/PUT/DELETE) |
| `Header.test.ts` | 10 | Component — guest vs auth rendering |
| `Footer.test.ts` | 5 | Component — links + copyright |
| `error.test.ts` | 5 | Component — error page rendering |
| **11 files** | **125** | |

### Existing Unit Tests
- `src/lib/utils/validation.test.ts` — 22 tests (Required, Email, MinLength, PasswordMatch, Validator chain)
- `src/lib/utils/claims.test.ts` — 11 tests (JWT claim mapping: standard, MS URIs, fallbacks, precedence)
- `src/lib/utils/api-helpers.test.ts` — 14 tests (buildApiUrl, unwrapResponse, parseErrorMessage)

### Existing E2E Infrastructure
- `cucumber.cjs` — config, paths to `../Features/**/*.feature`, 30s timeout
- `features/support/world.ts` — CustomWorld with Playwright browser + API context
- `features/support/hooks.ts` — Before/After browser lifecycle
- `features/support/steps.ts` — ~50 step definitions (mixed real/stub)
- `features/11-auth-e2e.feature` — FE-specific auth + route protection scenarios

---

## Test Architecture

```
Layer 0: Unit tests (Vitest)           — pure fn, utils, stores
Layer 1: Component tests (Vitest+STL)  — Svelte component rendering
Layer 2: E2E (Cucumber + Playwright)   — full-stack feature scenarios
```

---

## Phase 1: Unit Tests — Missing Coverage (~25 new tests)

### 1A: `src/lib/server/auth.test.ts` (~8 tests)

Server-side auth utilities. Needs vitest module mocking for `$env`.

- `verifyToken_ValidJwt_ReturnsPayload`
- `verifyToken_InvalidJwt_ReturnsNull`
- `verifyToken_ExpiredJwt_ReturnsNull`
- `verifyToken_WrongKey_ReturnsNull`
- `refreshAuthToken_Success_ReturnsTokenPair`
- `refreshAuthToken_BackendError_ReturnsNull`
- `setAuthCookies_SetsAccessTokenCookie`
- `setAuthCookies_SetsRefreshTokenCookie`
- `setAuthCookies_SecureFlagMatchesEnv`

**Critical file:** `src/lib/server/auth.ts`
**Mocking needed:** `$env/dynamic/private`, `$env/dynamic/public`, fetch

### 1B: `src/lib/stores/user.test.ts` (~5 tests)

Svelte 5 `$state` rune store.

- `init_SetsUserData`
- `set_UpdatesValue`
- `value_ReturnsCurrentUser`
- `init_Null_ClearsUser`
- `set_PartialData_Works`

**Critical file:** `src/lib/stores/user.svelte.ts`
**Note:** May need Svelte compiler context for `$state` rune

### 1C: `src/lib/utils/storage.test.ts` (~6 tests)

Client-side localStorage wrapper.

- `setToken_StoresInLocalStorage`
- `getToken_RetrievesFromLocalStorage`
- `removeToken_DeletesFromLocalStorage`
- `getToken_NoToken_ReturnsNull`
- `setToken_SSR_NoOp` (no `window`)
- `getToken_SSR_ReturnsNull`

**Critical file:** `src/lib/utils/storage.ts`
**Mocking needed:** `localStorage`, `browser` check

### 1D: `src/lib/api.test.ts` (~6 tests)

API client `send()` fn. Needs heavy mocking (`$env`, `fetch`, `$app`).

- `send_Get_CallsFetchWithCorrectUrl`
- `send_Post_IncludesBody`
- `send_401_ThrowsRedirectError`
- `send_204_ReturnsEmptyObject`
- `send_JsonError_ParsesMessage`
- `send_IncludesCredentials`

**Critical file:** `src/lib/api.ts`
**Mocking needed:** `$env/dynamic/public`, `$app/navigation`, `$app/environment`, global `fetch`

---

## Phase 2: Component Tests (~15 tests)

Requires: `@testing-library/svelte`, `jsdom`

### 2A: `src/lib/components/Header.test.ts` (~8 tests)

- `Guest_ShowsLoginRegisterLinks`
- `Guest_NoAvatarDropdown`
- `AuthUser_ShowsUsername`
- `AuthUser_ShowsAvatarDropdown`
- `AuthUser_DropdownHasProfileLink`
- `AuthUser_DropdownHasLogoutForm`
- `SearchInput_Exists`
- `SearchInput_SubmitsOnEnter`

**Critical file:** `src/lib/components/Header.svelte`

### 2B: `src/lib/components/Footer.test.ts` (~3 tests)

- `ShowsCopyright`
- `HasAboutLink`
- `HasPrivacyLink`

### 2C: `src/routes/+error.test.ts` (~4 tests)

- `ShowsErrorStatus`
- `ShowsErrorMessage`
- `ShowsUkrainianText`
- `HasHomeLink`

---

## Phase 3: E2E Step Fixes (existing steps.ts)

### 3A: Fix Stub Steps → Real Assertions

| Step pattern | Current | Fix to |
|---|---|---|
| DB verification (`/.* в базі даних .*/`) | `no error toast` | API verification OR remove from FE features |
| DB precondition (`/.* існує .* в базі .*/`) | empty body | `api.post('debug/ensure-...')` with error handling |
| `Система створює/оновлює/видаляє` | `networkidle` check | Intercept network req + verify response |
| `Лічильник вподобайок збільшується` | `toBeVisible()` only | Parse counter text, verify numeric change |
| `Система м'яко видаляє зв'язок` | `no error toast` | Verify element disappeared from DOM |

### 3B: Fix World Timeout

`world.ts:16` → `setDefaultTimeout(30 * 1000)` (already fixed per exploration — was 2s, now 30s)

---

## Phase 4: E2E Scenarios by Feature File

Each feature file → FE E2E scenarios. Features reference shared `Source/Features/*.feature` files. Step defs in `features/support/steps.ts`.

### Feature 1: Authentication (20 scenarios)

**Already covered by step defs:** ~70%
**Missing/stub steps to implement:**

| Scenario | Status | What's needed |
|---|---|---|
| US-101: Registration happy path | Partial | Form fill works, need post-register redirect + cookie verification |
| US-101: Duplicate username | Partial | Need error message assertion for specific Ukrainian text |
| US-101: Duplicate email | Partial | Same as above |
| US-101: Password mismatch | Partial | Client-side validation fires before submit — verify |
| US-101: Weak password (BRL-2) | Partial | MinLength validation message |
| US-101: Empty required fields | Partial | Multi-field validation |
| US-101: Reserved username | Stub | Need backend to reject + FE to display error |
| US-102: Login happy path | Real | ✓ Works |
| US-102: Login via email | Real | ✓ Works |
| US-102: Wrong password | Real | ✓ Works |
| US-102: User not found | Real | ✓ Works |
| US-103: Logout | Real | ✓ Cookie cleared, redirect |
| US-104: Token renewal | Stub | Hard to E2E — needs expired token simulation |
| US-104: Invalid refresh token | Stub | Same |
| US-104: RBAC admin access | Stub | Need admin user + protected endpoint |
| US-104: RBAC user denied | Stub | Need role check |
| US-104: Guest denied | Stub | 401 check |
| US-105: Empty login body | Stub | API validation |
| US-105: Missing password | Stub | API validation |

**New steps needed:** ~8 step defs

### Feature 2: Profile (23 scenarios)

**Status:** Most steps are stubs. FE routes largely WIP.

| Scenario Group | Count | E2E Possible? |
|---|---|---|
| US-201: View/edit own profile | 3 | When /profile/edit route built |
| US-202: View other's profile | 2 | When /profile/[username] built |
| US-203: Follow/unfollow | 4 | When follow UI built |
| US-204: Admin user list | 3 | When /admin route built |
| US-205: Admin role/delete | 3 | When admin panel built |
| US-206: Password change | 3 | When /settings route built |
| US-207: Avatar upload/delete | 4 | When avatar UI built |
| US-207: Avatar security | 1 | API-level test |

**New steps needed:** ~20 step defs (when routes implemented)
**Blocked by:** WIP routes (/profile/edit, /settings, /admin, /profile/[username])

### Feature 3: Feed (5 scenarios)

| Scenario | E2E Possible? |
|---|---|
| US-301: Personal feed | When / route shows feed |
| US-301: Empty feed | When / route shows feed |
| US-302: Global feed | When / route has tabs |
| US-303: Like from feed | When feed + like UI built |
| US-304: Top comment in feed | When comment display built |

**Blocked by:** Home page is SvelteKit boilerplate (no feed UI yet)

### Feature 4: Media Page (13 scenarios)

| Scenario | E2E Possible? |
|---|---|
| US-401: Media page with translation | When /media/[id] built |
| US-401: Default language fallback | When /media/[id] built |
| US-402: Write review | When review form built |
| US-402: Duplicate review (BRL-4) | When review form built |
| US-403: Like review | When like button built |
| US-403: Unlike review | When like button built |
| US-404: Write comment L0 | When comment form built |
| US-405: Reply comment L1 | When reply UI built |
| US-406: No reply to reply | When reply UI built |
| US-407: Guest → login redirect | Partial (redirect works) |
| US-407: Guest comment blocked | When comment form built |
| US-408: Propose translation | When translation form built |
| US-408: Existing translation hidden | When translation check built |

**Blocked by:** /media/[id] route not yet built

### Feature 5: Tracking (5 scenarios)

| Scenario | E2E Possible? |
|---|---|
| US-501: Add tracking status | When tracking dropdown built |
| US-501: Change status | When tracking dropdown built |
| US-501: Close dropdown (click outside) | When tracking dropdown built |
| US-501: Same status = no request | When tracking dropdown built |
| US-502: Update progress | When tracking list built |

**Blocked by:** Tracking UI not yet built

### Feature 6: Moderation (5 scenarios)

| Scenario | E2E Possible? |
|---|---|
| US-601: Report review | When report UI built |
| US-602: Moderator resolves (delete) | When moderation panel built |
| US-602: Moderator dismisses | When moderation panel built |
| US-603: Approve translation | When moderation queue built |
| US-603: Reject translation | When moderation queue built |

**Blocked by:** Moderation panel not yet built

### Feature 7: Admin (6 scenarios)

| Scenario | E2E Possible? |
|---|---|
| US-701: Change user role | When admin panel built |
| US-701: Soft-delete user | When admin panel built |
| US-702: Edit translation | When admin media page built |
| US-702: Soft-delete media | When admin media page built |
| US-703: View statistics | When stats dashboard built |
| US-703: Export CSV | When export button built |

**Blocked by:** Admin panel not yet built

### Feature 8: Collections (9 scenarios)

| Scenario | E2E Possible? |
|---|---|
| US-801: Create collection | When /lists route built |
| US-802: Add media to collection | When add-to-collection UI built |
| US-802: Remove media from collection | When collection page built |
| US-803: Make collection private | When privacy settings built |
| US-803: Stranger can't see private | When privacy enforced |
| US-804: Grant access | When sharing UI built |
| US-804: Invited user sees collection | When access check built |
| US-804: Revoke access | When sharing UI built |
| US-805: Delete collection | When collection settings built |

**Blocked by:** /lists, /collections routes not yet built

### Feature 9: Search (5 scenarios)

| Scenario | E2E Possible? |
|---|---|
| US-901: Search with mixed results | When search results page built |
| US-901: Search Ukrainian translation | When search results page built |
| US-901: No pending/deleted results | When filtering implemented |
| US-902: Open external media (cache miss) | When /media/[id] built |
| US-902: Open cached media | When /media/[id] built |

**Blocked by:** Search results page not yet built (Header has search input but no results route)

### Feature 10: Reviews (24 scenarios)

Overlaps with Feature 4. Backend-focused scenarios (API status codes). FE tests:

| Scenario Group | Count | E2E Possible? |
|---|---|---|
| Create review | 4 | When review form built |
| Get reviews (paginated) | 2 | When review list built |
| Update review | 2 | When edit review built |
| Delete review | 3 | When delete button built |
| Like/unlike review | 3 | When like button built |
| Comments (L0, L1, no L2) | 5 | When comment UI built |
| Comment likes | 2 | When comment like built |
| Delete comment | 3 | When delete comment built |

**Blocked by:** Review/comment UI not yet built

### Feature 11: Auth E2E (10 scenarios) — FE-SPECIFIC

| Scenario | Status |
|---|---|
| Failed login — wrong password | **Real** ✓ |
| Failed login — empty fields | **Real** ✓ |
| Logout → see login button | **Real** ✓ |
| Login → redirectTo | **Real** ✓ |
| Route protection: /profile | **Real** ✓ |
| Route protection: /settings | **Real** ✓ |
| Route protection: /lists | **Real** ✓ |
| Route protection: /collections | **Real** ✓ |
| Route protection: /reviews | **Real** ✓ |
| Route protection: /following | **Real** ✓ |

**Feature 11 is 100% implemented** — all steps have real Playwright assertions.

---

## Phase 5: hooks.server.ts Tests (~8 tests)

Server hook = auth middleware. High-value target.

**File:** `src/hooks.server.test.ts`

- `handle_ValidAccessToken_SetsLocalsUser`
- `handle_ExpiredAccess_ValidRefresh_RefreshesTokens`
- `handle_NoTokens_LocalsUserNull`
- `handle_ProtectedRoute_NoAuth_RedirectsToLogin`
- `handle_ProtectedRoute_WithAuth_Passes`
- `handle_PublicRoute_NoAuth_Passes`
- `handle_RedirectTo_PreservedInLoginUrl`
- `handle_InvalidToken_ClearsSession`

**Critical file:** `src/hooks.server.ts`
**Mocking needed:** `RequestEvent`, cookies, `$env`, `resolve()`

---

## Summary

| Phase | Tests | Priority | Status |
|-------|-------|----------|--------|
| 1. Unit tests (auth, store, storage, api) | 42 | HIGH | **DONE** |
| 2. Component tests (Header, Footer, Error) | 20 | MEDIUM | **DONE** |
| 3. E2E step fixes (stubs → real) | 8 improved | HIGH | **DONE** |
| 4. E2E Feature 11 (auth-e2e) | 10 scenarios | HIGH | **DONE** |
| 4. E2E Features 2-10 | ~60 steps | LOW | **Blocked — routes WIP** |
| 5. hooks.server.ts tests | 10 | HIGH | **DONE** |
| **Total implemented** | **125** | | |
| **Remaining (when routes built)** | **~60** | | |

## Completed Implementation (2026-04-26)

| Step | What | Effort |
|------|------|--------|
| 1 | Phase 1C: `storage.test.ts` — localStorage + SSR (9 tests) | Done |
| 2 | Phase 1A: `auth.test.ts` — JWT verify, refresh, cookies (13 tests) | Done |
| 3 | Phase 1B: `user.svelte.test.ts` — Svelte 5 $state store (9 tests) | Done |
| 4 | Phase 5: `hooks.server.test.ts` — auth middleware (10 tests) | Done |
| 5 | Phase 1D: `api.test.ts` — API client all methods (11 tests) | Done |
| 6 | Phase 2A: `Header.test.ts` — guest vs auth rendering (10 tests) | Done |
| 7 | Phase 2B: `Footer.test.ts` — links + copyright (5 tests) | Done |
| 8 | Phase 2C: `error.test.ts` — error page rendering (5 tests) | Done |
| 9 | Phase 3: E2E stub improvements (8 step defs upgraded) | Done |

## Previous Implementation
| 4 | Phase 3A: Fix stub step defs → real assertions | 1-2 hr |
| 5 | Phase 2A: `Header.test.ts` — component tests | 1 hr |
| 6 | Phase 1D: `api.test.ts` — API client | 1 hr |
| 7 | Phase 4 Feature 1: remaining auth E2E | 1 hr |
| 8 | Phase 2B-C: Footer + Error component tests | 30 min |
| 9 | Phase 1B: `user.test.ts` — store | 30 min |

## New Dependencies

```bash
npm install -D @testing-library/svelte @testing-library/jest-dom jsdom
```

## File Organization

```
Frontend/
├── src/
│   ├── hooks.server.test.ts          (NEW — Phase 5)
│   └── lib/
│       ├── api.test.ts               (NEW — Phase 1D)
│       ├── server/
│       │   └── auth.test.ts          (NEW — Phase 1A)
│       ├── stores/
│       │   └── user.test.ts          (NEW — Phase 1B)
│       ├── components/
│       │   ├── Header.test.ts        (NEW — Phase 2A)
│       │   └── Footer.test.ts        (NEW — Phase 2B)
│       └── utils/
│           ├── api-helpers.test.ts    (EXISTS — 14 tests)
│           ├── claims.test.ts         (EXISTS — 11 tests)
│           ├── validation.test.ts     (EXISTS — 22 tests)
│           └── storage.test.ts        (NEW — Phase 1C)
├── features/
│   ├── support/
│   │   ├── world.ts                  (EXISTS)
│   │   ├── hooks.ts                  (EXISTS)
│   │   └── steps.ts                  (MODIFY — Phase 3)
│   └── 11-auth-e2e.feature           (EXISTS — DONE)
└── (shared features in Source/Features/*.feature)
```

## What NOT to Test (FE)

- Static WIP pages (about, contact, privacy, terms) — all `<h1>WIP</h1>`
- `lib/index.ts` — empty file
- Backend API logic — already tested in BE test suite
- Features 2-10 E2E — blocked by unbuilt routes (plan exists, implement when routes ready)

## Verification

```bash
# Unit tests
npm run test:unit

# E2E tests (needs running dev server + backend)
npm run dev &
npm run test:bdd

# Type check
npm run check
```
