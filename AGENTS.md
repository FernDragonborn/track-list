# AGENTS.md

## Repo layout

This is an academic course repo (README is course requirements in Ukrainian). The actual project lives entirely under `Source/`.

- `Source/frontend/` — SvelteKit 2 + TypeScript + Tailwind CSS 4
- `Source/Backend/track-list-api/` — ASP.NET Core 10 + EF Core + PostgreSQL
- `Source/Features/` — shared BDD feature files (Ukrainian Gherkin)
- `Source/` — Docker Compose files, root `.env`, shared dev tooling (prettier, eslint)

## Commands — working directories matter

| Subsystem | Work from | Key commands |
|---|---|---|
| Frontend | `Source/frontend/` | `npm run dev`, `npm run build`, `npm run check`, `npm run lint`, `npm run test:unit`, `npm run test:bdd` |
| Backend | `Source/Backend/track-list-api/` | `dotnet run`, `dotnet test ../TrackListTests`, `dotnet ef migrations add <Name>` |
| Docker | `Source/` | `docker compose -f docker-compose.dev.yaml up --build` (dev), `docker compose up --build -d` (prod) |

Use the `Source/package.json` only for lint/format — it has no app scripts.

## Critical details

- **Casing**: Frontend dir is `frontend/` (lowercase). Prod Dockerfile references `./Frontend` (capital F) — works on Windows, beware on Linux.
- **Framework versions**: SvelteKit 2 + Svelte 5, .NET 10, Tailwind CSS 4, Vitest 4.x. These are cutting-edge version lines.
- **Vite proxy** (`vite.config.ts`): dev `/api` calls go to `http://localhost:80` (Caddy), not directly to the API port.
- **Frontend tests**: `test:integration` lists specific files in `package.json` (not auto-discovered). Vitest config is in `vite.config.ts` (jsdom env).
- **BDD**: Frontend Cucumber config reads from both `features/` and `../Features/`. Backend uses Reqnroll. Feature files are Ukrainian (`language: uk`).
- **Soft-delete**: Entities with `DeletedAt` use an EF Core global query filter — all queries exclude them by default.
- **API DTOs**: Backend never returns raw entities; always use DTOs (AutoMapper).
- **`.env` files**: At `Source/.env` (shared), `Source/Backend/track-list-api/.env` (backend-specific), and `Source/frontend/.env` (frontend). All are gitignored. `dotenv.net` loads `.env` in `Program.cs` automatically.
- **Migrations** (`Source/Backend/track-list-api/Migrations/`) are gitignored — must be regenerated locally.
- **No CI**: `.github/workflows/` is empty.
- **`npm run check`** runs `svelte-kit sync && svelte-check` — must pass before tests.

## Reference

`Source/CLAUDE.md` has comprehensive architecture and convention docs for the Track List project proper.
