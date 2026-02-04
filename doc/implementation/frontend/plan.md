# Frontend Implementation Plan (Vue + TypeScript)

This plan addresses `doc/implementation/frontend/task.md`:
- Add a client for the current NovaTune project
- TypeScript + Vue + Vite SPA for web
- Main music player: Desktop (Electron) and Android app
- Admin and other services: simple SPA

## 0. Current repo state (constraints)

- Backend already exists as ASP.NET Core minimal APIs in `src/NovaTuneApp/NovaTuneApp.ApiService` with OpenAPI at `/openapi/v1.json` and Scalar UI at `/scalar/v1`.
- Local orchestration is via Aspire in `src/NovaTuneApp/NovaTuneApp.AppHost` (RavenDB, MinIO, Redpanda, Garnet).
- `src/NovaTuneApp/NovaTuneApp.Web` currently contains a Blazor/Razor Components starter UI; `doc/requirements/stack.md` calls for Vue + TypeScript.

## 1. Decisions to lock early

1. **One codebase vs multiple apps**
   - Recommended: a small monorepo containing *two SPAs* plus shared packages:
     - `player` (Listener experience: library + playback + playlists + upload)
     - `admin` (Admin dashboards and moderation)
     - shared packages: API client + auth + telemetry + UI primitives
2. **How the SPA is hosted**
   - Dev: run Vite dev server separately and talk to API via CORS or Vite proxy.
   - Prod: pick one:
     - Option A (recommended): serve the built SPA from a dedicated static host (nginx / container), and keep `NovaTuneApp.Web` as legacy or remove later.
     - Option B: repurpose `NovaTuneApp.Web` to serve `dist/` as static files (no Blazor).
3. **Auth storage strategy**
   - API uses JWT access token + refresh token, and expects refresh token in request body (`POST /auth/refresh`).
   - Recommended per-platform storage:
     - Web: refresh token in `localStorage` initially (MVP), with a follow-up item to migrate to HttpOnly cookie/BFF if desired.
     - Electron: store refresh token in OS keychain (e.g. `keytar`) if/when added.
     - Android: store refresh token via secure storage (Capacitor Secure Storage plugin) if/when added.
4. **Device ID**
   - Clients should generate and persist a per-installation device ID and send `X-Device-Id` on auth requests.
   - Telemetry requires device IDs to be **hashed client-side** before transmission (see `doc/implementation/stage-7-telemetry.md`).

## 2. Proposed repo layout

Add a new frontend workspace without touching the .NET solution:

```
src/NovaTuneClient/
  package.json
  pnpm-workspace.yaml
  apps/
    player/
    admin/
  packages/
    api-client/         (OpenAPI-generated types/client)
    core/               (auth, http, telemetry, device id, errors)
    ui/                 (shared Vue components)
```

Notes:
- Using `pnpm` (workspaces) keeps installs fast and deduplicated; `npm` workspaces is also fine if preferred.
- Keep all runtime configuration via env vars (no secrets committed).

## 3. API integration approach (recommended)

1. **Generate a typed TypeScript client from OpenAPI**
   - Source: API’s `/openapi/v1.json`.
   - Output: `packages/api-client/src/generated/*` (checked in or generated in CI; choose one).
2. **HTTP wrapper in `packages/core`**
   - Centralize:
     - Base URL (`VITE_API_BASE_URL`)
     - `Authorization: Bearer <token>` header injection
     - Refresh-on-401 flow (single-flight refresh; retry original request once)
     - RFC7807 Problem Details parsing into a stable UI error shape
3. **CORS / dev proxy**
   - Short-term easiest: add CORS in `NovaTuneApp.ApiService` for:
     - `http://localhost:<vitePort>` (player/admin dev servers)
   - Also plan MinIO bucket CORS if playback is cross-origin (see `doc/implementation/stage-4-streaming.md`).

## 4. Player SPA scope (MVP)

Minimum flows for a useful player:
- Authentication: register/login/refresh/logout
- Track list: `GET /tracks` (filters/search/sort, paging)
- Track details: `GET /tracks/{trackId}`
- Streaming:
  - call `POST /tracks/{trackId}/stream` to get presigned URL
  - play via `<audio>` with Range request support (browser does this; verify with MinIO CORS/exposed headers)
- Playlists:
  - list/create/rename/delete playlists
  - add/remove/reorder tracks
- Upload (optional for player MVP, but usually needed for end-to-end usability):
  - initiate upload session via upload endpoints
  - upload file to presigned PUT
  - show post-upload processing status transitions (eventual consistency)
- Telemetry:
  - emit play_start/play_progress/play_stop (batching) to telemetry endpoint
  - include hashed device ID + session ID

## 5. Admin SPA scope (MVP)

Implement the Stage 8 admin experience from API endpoints:
- Admin authentication and routing guard
- Users:
  - list/search/filter/sort/paginate
  - update status with reason code
- Tracks:
  - list/search/filter/sort/paginate across users
  - moderate/disable/delete
- Analytics:
  - overview dashboard
  - per-track analytics
- Audit logs:
  - list/filter
  - integrity verification view (if exposed by API)

## 6. Desktop (Electron) packaging plan

Target: ship the `player` app as a desktop player.

Recommended approach:
- Use Electron as a thin shell that loads the same Vue app bundle.
- Add platform integration only when needed (media keys, file associations, auto-updates).

Steps:
1. Create `apps/player` first and stabilize auth + playback.
2. Add an Electron wrapper project (either:
   - `apps/player-electron` that depends on the built `apps/player`, or
   - integrate Electron into the player build via an Electron+Vite setup).
3. Harden security defaults:
   - `contextIsolation: true`, `nodeIntegration: false`
   - strict IPC surface; no arbitrary URL loading
4. Add token storage hardening (keychain) as a follow-up task.

## 7. Android packaging plan

Target: ship the `player` app on Android with minimal rewrite.

Recommended approach:
- Use Capacitor to wrap the `player` web app.

Steps:
1. Create `apps/player` first (web).
2. Add Capacitor project and `capacitor.config.*` pointing at the built `player` `dist/`.
3. Handle mobile-specific concerns:
   - background audio behavior (may need a plugin later)
   - secure token storage (Capacitor secure storage plugin)
   - network/base URL (dev vs prod)

## 8. Local dev workflow (recommended)

1. Start backend (Aspire):
   - `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost/NovaTuneApp.AppHost.csproj`
2. Start SPA(s):
   - `pnpm -C src/NovaTuneClient install`
   - `pnpm -C src/NovaTuneClient --filter player dev`
   - `pnpm -C src/NovaTuneClient --filter admin dev`

## 9. CI/CD (minimum)

- Lint + typecheck + unit tests for frontend workspace.
- Build `player` and `admin` bundles.
- (Optional) Electron build artifacts per OS; Android builds can remain manual initially.

## 10. Work breakdown (recommended order)

1. Add `src/NovaTuneClient` workspace scaffold (Vite+Vue+TS) with `player` + `admin`.
2. Implement shared `packages/core` (auth + http + errors + device ID hashing).
3. Generate `packages/api-client` from `/openapi/v1.json` and wire it into both apps.
4. Build `player` MVP: auth → library → streaming playback → playlists → telemetry.
5. Build `admin` MVP: user list/status → track moderation → analytics → audit logs.
6. Add MinIO CORS (if needed for browser playback + range headers).
7. Add Electron wrapper for `player`.
8. Add Android (Capacitor) wrapper for `player`.
