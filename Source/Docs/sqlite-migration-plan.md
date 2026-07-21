# SQLite Migration Plan

**Date:** 2026-06-02
**Goal:** Replace PostgreSQL with SQLite. Lighter footprint, fewer moving parts in Docker, simpler dev setup.

## Decisions (locked in)
- **Data**: re-seed via `seeder/main.py` (fresh dataset, no PG dump conversion)
- **Search**: replace `EF.Functions.ILike` with `ToLower().Contains(...)` (works for Cyrillic via .NET String semantics)
- **WAL mode**: enabled on startup (`PRAGMA journal_mode=WAL`) — better concurrent reads
- **DB file**: `/app/data/tracklist.db` inside container, mounted from `./data` on host
- **`.gitignore`**: ignore `*.db`, `*.db-shm`, `*.db-wal`; never commit data file

## Changes

### `track-list-api.csproj`
- Remove `Npgsql.EntityFrameworkCore.PostgreSQL` v10.0.1
- Add `Microsoft.EntityFrameworkCore.Sqlite` v10.0.6

### `Configure.cs:ConfigControllers<T>`
- Replace `builder.Services.AddNpgsql<TContext>(cs)` with
  ```csharp
  builder.Services.AddDbContext<TContext>(opts => opts.UseSqlite(cs));
  ```

### `Configure.cs:CreateDbIfNotExists`
- After `EnsureCreated()`: open a raw connection, execute `PRAGMA journal_mode=WAL;` and `PRAGMA foreign_keys=ON;`.

### `MediaGetService.SearchAsync`
- Replace `EF.Functions.ILike(t.Title, likePattern)` with case-insensitive Contains on lowercased values.

### `.env` files (root + api)
- `CONNECTION_STRING="Data Source=/app/data/tracklist.db;Cache=Shared;Mode=ReadWriteCreate"`

### `docker-compose.dev.yaml` + `docker-compose.yaml`
- Drop `dev-db` / db service entirely.
- Drop `depends_on: dev-db` from api service.
- Mount `./data:/app/data` on the api service.
- Caddy stays unchanged (still proxies API).

### Manual migration SQL
- `Migrations/001_ExternalContent.sql`, `Migrations/002_Translations.sql`: rewrite type names (`uuid` → `TEXT`, `timestamp with time zone` → `TEXT`, `character varying(N)` → `TEXT`). Will be largely redundant since `EnsureCreated()` builds the schema from EF model; keep as reference for fresh installs that might need additive sql.

### `Configure.cs:SeedDefaultUsers`
- No change — works with any provider.

## Re-seed flow

After SQLite container boots:
1. `EnsureCreated()` builds schema from EF model
2. `SeedDefaultUsers` + `GenreSeeder.Seed` populate admin + moderator + 18 movie/8 tv genres
3. Run `python seeder/main.py` against API
   - phase 1: media (300+ via TMDB discover → /api/media/Tmdb:{type}:{id})
   - phase 2: TMDB reviews
   - phase 3: Reddit (404 from this IP — skipped)
   - phase 4: register accounts
   - phase 5: post reviews
4. Optionally run `seeder/post_leftovers.py` to fill the 200–499 char gap
5. `seeder/set_avatars.py` to wire DiceBear PNG avatars

## Risks
- `EF.Functions.ILike` references outside `MediaGetService.SearchAsync` — grep first.
- BCrypt 100-user register batch on SQLite (single-writer lock) ~10–20 sec; acceptable.
- DateTime offset round-trip: SQLite stores ISO 8601 strings; EF maps `DateTime?` correctly but `DateTimeOffset` needs verification. We use `DateTime.UtcNow` everywhere — safe.
- `Guid` PK stored as TEXT by default in EF Core SQLite provider.
- Soft-delete query filters work identically.

## Rollback
- If something breaks: `git revert` the migration commits, bring back Postgres compose service. DB file in `./data` can be kept (small) or deleted.

## Out of scope
- Production deployment story (no prod runs yet)
- Migrating .NET tests config (tests probably use SQLite in-memory already; verify)
