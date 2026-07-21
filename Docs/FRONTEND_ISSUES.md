# Frontend Code Issues

Analysis of `Source/Frontend/src/` — SvelteKit 2 + Svelte 5 + TypeScript.

---

## ~~SECURITY~~ (FIXED)

### ~~S1. Token leaked to client via form action response~~
**Files:** `auth/login/+page.server.ts:58`, `auth/register/+page.server.ts:52`
Server actions return `{ success: true, tokens }` — the full `TokensResponse` (including accessToken/refreshToken) is sent back to the browser as form action data. The client then stores it in `localStorage` (login `+page.svelte:30`, register `+page.svelte:51-53`). Since tokens are already set as httpOnly cookies server-side, returning them to the client and storing in localStorage defeats the purpose of httpOnly — XSS can now read tokens from localStorage.

### ~~S2. JWT secret fallback to hardcoded string~~
**File:** `lib/server/auth.ts:12`
`jwt.verify(token, env.JWT_SECRET || 'secret-fallback')` — if `JWT_SECRET` env var is missing, falls back to `'secret-fallback'`, which is trivially guessable. Should throw on missing secret.

### ~~S3. Wrong env var name for JWT secret~~
**File:** `lib/server/auth.ts:12` uses `env.JWT_SECRET`, but `hooks.server.ts:15` uses `env.JWT_PRIVATE_KEY`, and `.env` defines `JWT_PRIVATE_KEY`. The `auth.ts` file will always hit the fallback.

### ~~S4. Refresh token stored in localStorage~~
**File:** `auth/register/+page.svelte:53`
`localStorage.setItem('refresh_token', ...)` — refresh tokens should never be in localStorage (XSS-accessible). They're already in httpOnly cookies.

### ~~S5. `sameSite` missing on cookies in hooks.server.ts~~
**File:** `hooks.server.ts:48-63`
Cookie options during token refresh don't set `sameSite`. Login/register actions set `sameSite: 'lax'`, but hooks.server.ts omits it, creating inconsistency.

---

## ARCHITECTURE / DEAD CODE

### ~~A1. Duplicate token refresh logic~~ (FIXED)
**Files:** `hooks.server.ts:35-71` AND `lib/server/auth.ts:18-35`
Two separate implementations of token renewal: `hooks.server.ts` calls `AuthService.renewToken()` (which uses `api.ts`), while `lib/server/auth.ts` has `refreshAuthToken()` doing raw fetch. The `auth.ts` version is never called.

### ~~A2. `lib/server/auth.ts` is entirely unused~~ (FIXED)
`verifyToken()`, `refreshAuthToken()`, `setAuthCookies()` — none are imported anywhere. `hooks.server.ts` does everything directly. Dead module.

### A3. `lib/api.ts` mixes client and server concerns
**File:** `lib/api.ts`
Uses `goto()`, `browser`, `localStorage` (client-only) but also used by `AuthService` which is imported in `hooks.server.ts` (server-only). The `browser` guard prevents crashes but the module design is confused — server code shouldn't import a module that references `$app/navigation`.

### ~~A4. `lib/services/auth.service.ts` only used server-side~~ (FIXED — deleted, hooks.server.ts uses auth.ts directly)
**File:** `lib/services/auth.service.ts`
Only imported in `hooks.server.ts` for `renewToken()`. Login/register bypass it entirely (server actions use raw `fetch`). Either use AuthService consistently or remove it.

### A5. `storage.ts` partially redundant
**File:** `lib/utils/storage.ts`
Stores access token in localStorage, but auth is httpOnly cookie-based. `storage.getToken()` is called in `api.ts:25` as fallback, and `storage.setToken()` in login/register pages. This creates a dual-auth system (cookies + localStorage) that's confusing and insecure (see S1).

### ~~A6. Duplicate logout endpoints~~ (FIXED)
**Files:** `auth/logout/+page.server.ts` AND `api/auth/logout/+server.ts`
Two logout handlers doing the same thing (delete cookies). The form in Header.svelte posts to `/auth/logout` (page action), making the API endpoint unused.

### A7. Profile page is a test stub
**File:** `routes/profile/+page.svelte`
Contains only a test button labeled "ТЕСТ ІНТЕРЦЕПТОРА" that calls a non-existent endpoint. This is debug code, not a profile page.

### A8. `lib/index.ts` is empty
**File:** `lib/index.ts`
Only contains a comment. No re-exports. Either use it as a barrel or delete it.

---

## TYPE SAFETY

### T1. `TokensResponse` extends `UserDto` incorrectly
**File:** `lib/types/userTypes.ts:43-46`
`TokensResponse extends UserDto` implies a token response contains user fields (email, role, gender, etc.). The actual API returns `{ accessToken, refreshToken }` without user data. This causes type confusion — code treats token responses as if they contain user fields.

### T2. Optional fields that shouldn't be optional
**File:** `lib/types/userTypes.ts`
`LoginRequest.password` is optional (`password?: string`) — a login request always requires a password. `RegisterRequest.confirmPassword` is also optional. `TokensResponse.accessToken/refreshToken` are optional — if they're missing, the auth flow breaks.

### T3. `App.Locals.user` type doesn't match `UserState`
**File:** `app.d.ts` vs `stores/user.svelte.ts`
`Locals.user` has `id?: string` field, but `UserState` has `profilePicUrl?: string` instead. These represent the same user but with different shapes.

---

## CODE QUALITY

### Q1. `console.log` left in production code
**Files:**
- `hooks.server.ts:26` — `console.log('Decoded JWT (Success):', decoded)` — logs full JWT payload including claims
- `hooks.server.ts:29` — logs token errors
- `auth/login/+page.server.ts:16` — `console.log('login attempt')`
- `api.ts:51` — `console.log(res)` on error
- `profile/+page.svelte:8` — test console.log

### Q2. Inconsistent error message strings
Login uses `'Невірний логін або пароль'` (server) but validation uses `'Невірний формат email'` (client). Mix of Ukrainian and English error messages. No centralized error message constants.

### Q3. Typo in api.ts
**File:** `api.ts:10`
`'PUBLIC_API_URL is 0 charecters long'` — "charecters" should be "characters".

### Q4. Redundant null check after length check
**File:** `api.ts:10`
`BASE_URL.length === 0 || BASE_URL === null || BASE_URL === undefined` — if `.length` didn't throw, it's not null/undefined. Also, the ternary on line 9 already guarantees it's a string.

### Q5. Commented-out code
**Files:**
- `hooks.server.ts:6-13` — commented `handleError`
- `api.ts:9` — commented `.replace()`
- `lib/types/userTypes.ts:21-24` — commented user fields
- `+layout.svelte:31` — commented `{@render children()}`

### Q6. `userStore.set()` and `userStore.init()` are identical
**File:** `stores/user.svelte.ts:15-19`
Both methods do `user = newUser`. No difference. Remove one.

### Q7. Cookie maxAge inconsistency
Access token cookie maxAge:
- `hooks.server.ts:59` → 15 minutes
- `auth/login/+page.server.ts:45` → 30 minutes
- `auth/register/+page.server.ts:42` → 30 minutes
- `lib/server/auth.ts:42` → 15 minutes

Should be one constant.

### Q8. `secure` flag determined differently
- `hooks.server.ts:52` → `event.url.protocol === 'https:'`
- `auth/login/+page.server.ts:44` → `import.meta.env.PROD`
- `lib/server/auth.ts:41` → `process.env.NODE_ENV === 'production'`

Three different methods for the same decision.

### Q9. Home page is SvelteKit boilerplate
**File:** `routes/+page.svelte`
Still shows "Welcome to SvelteKit" default content.

### Q10. Static pages are stubs
**Files:** `about/`, `contact/`, `privacy/`, `terms/` — all just `<h1>WIP</h1>`. Footer links to them, giving users dead pages.

### Q11. Header avatar is hardcoded
**File:** `components/Header.svelte:85`
Avatar always uses `https://ui-avatars.com/api/?name=User` regardless of actual username or profile pic. External service dependency for a placeholder.

### Q12. Unicode quote in string literal
**File:** `lib/utils/validation.ts:6`
`'Це поле обов'язкове'` — the `'` (right single quote / apostrophe) inside the string may cause issues depending on encoding. Should use escaped apostrophe or template literal.

---

## SUMMARY BY PRIORITY

| Priority | Count | Category |
|----------|-------|----------|
| ~~HIGH~~ | ~~5~~ | ~~Security (S1-S5) — FIXED~~ |
| ~~MEDIUM~~   | ~~3~~ | ~~Architecture / Dead code (A1, A2, A4, A6) — FIXED~~ |
| MEDIUM   | 5     | Architecture / Dead code (A3, A5, A7, A8) |
| MEDIUM   | 3     | Type safety (T1-T3) |
| LOW      | 12    | Code quality (Q1-Q12) |
