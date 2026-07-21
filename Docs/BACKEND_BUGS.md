# Backend Bugs — Resolved

All bugs found during code audit have been fixed. Documented for history.

| ID | Severity | Description | Fix |
|----|----------|-------------|-----|
| SEC-001 | **CRITICAL** | IdentityData: PolicyModerator + PolicyUser both = "adminPolicy" — any user got admin access | Unique policy names: adminPolicy, moderatorPolicy, userPolicy |
| SEC-002 | **CRITICAL** | TrackingStatus DELETE used `[HttpGet]`, no `[Authorize]`, imported Reqnroll in prod | Full controller rewrite: correct verb, auth, route constraints |
| SEC-003 | **HIGH** | MediaController: 6 write endpoints (CRUD + translations) had no `[Authorize]` | Added Admin/Moderator/User policies per endpoint |
| BUG-001 | **CRITICAL** | Password change rejected correct password (`!NotCorrectPassword`) | Removed `!` negation |
| BUG-002 | **HIGH** | Public profile `GET /api/profiles/{username}` required JWT — guests couldn't view | Made JWT optional, skip follow-status for guests |
| BUG-003 | **HIGH** | Avatar upload used `[FromBody]` — IFormFile needs `[FromForm]` | Changed attribute |
| BUG-004 | **MEDIUM** | `SaveAvatarAsync` never updated `User.ProfilePicUrl` | Added update after save |
| BUG-006 | **LOW** | Registration: no reserved username check, no letters+digits in password | Added rules to RegisterRequestValidator |
| RESP-001 | **HIGH** | Inconsistent response format across controllers (Ok/NoContent/message/unwrapped) | Standardized: `{data}` for 200, 204 for mutations, `{error}` for 400 |
