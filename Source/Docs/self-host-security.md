# Self-Host Security Checklist

Track List defaults are intended for a closed self-host instance:

- no default `admin/admin` or `moderator/moderator` accounts;
- one-time `/setup` creates the first administrator;
- production setup requires `TRACKLIST_SETUP_TOKEN`;
- public registration is off unless explicitly enabled;
- max users defaults to `1` in production;
- external HTTP integrations are off unless explicitly enabled;
- refresh tokens rotate on renew and are revoked on logout/password change.

## Required Production Env

Set these before first production boot:

```env
JWT_PRIVATE_KEY="replace-with-32-plus-random-characters"
TRACKLIST_SETUP_TOKEN="replace-with-a-one-time-random-token"
TRACKLIST_PUBLIC_REGISTRATION=false
TRACKLIST_MAX_USERS=1
TRACKLIST_ALLOWED_ORIGINS=https://your-domain.example
```

`JWT_PRIVATE_KEY` placeholders make production startup fail on purpose.

## First Admin

Open `/setup` and create the first administrator. In production, provide the same
value as `TRACKLIST_SETUP_TOKEN`. After any user exists in the database,
`/api/setup/admin` returns conflict and cannot create another admin.

## Optional External Integrations

All outbound providers are opt-in:

```env
TRACKLIST_ENABLE_TMDB=false
TRACKLIST_ENABLE_OMDB=false
TRACKLIST_ENABLE_DEEPL=false
TRACKLIST_ENABLE_LETTERBOXD=false
TRACKLIST_ENABLE_WIKIPEDIA=false
```

Data sent when enabled:

- TMDB: media search terms and TMDB external ids;
- OMDb: IMDb ids;
- DeepL: review text, comment text, external review text, and media descriptions requested for translation;
- Letterboxd: configured reviewer handles and media title/year matching queries through RSS/profile fetches;
- Wikipedia: media titles, release years, and media type during article lookup.

## Operational Notes

- Keep API reachable only through Caddy; do not publish container port `8080`.
- `PUBLIC_API_URL` is optional for the default Caddy layout; frontend falls back
  to same-origin `/api`. Set it only when API and frontend are on different
  origins or a custom proxy path.
- Keep `.env`, `data/`, `uploads/`, `logs/`, and generated coverage out of Git.
- Rotate `JWT_PRIVATE_KEY` if it was ever shared with the frontend or committed.
- Review Caddy `SITE_ADDRESS` and set it to your real HTTPS site address before public release.
- `DOTNET_EnableDiagnostics=0` is the production default to reduce runtime inspection surface.
