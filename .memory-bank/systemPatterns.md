# System Patterns

## Runtime Topology
```text
player SPA (Vite dev or static files)
admin SPA (Vite dev or static files under /admin)
                |
                v
NovaTuneApp.ApiService
  |- RavenDB
  |- Garnet/Redis
  |- MinIO
  \- Redpanda/Kafka
       |- UploadIngestor worker
       |- AudioProcessor worker
       |- Lifecycle worker
       \- Telemetry worker
```

## Environment-Specific Composition
- `Testing`: AppHost starts API + cache + RavenDB + MinIO and disables messaging
- `Development`: AppHost starts API, workers, infrastructure, and Vite dev servers on ports `25173` and `25174`
- non-Development: AppHost starts `NovaTuneApp.Web`, which serves built frontend assets from `wwwroot` and `wwwroot/admin`

## Backend Patterns
- Minimal APIs grouped by domain in `Endpoints/*`
- Central extension methods for auth, RavenDB, Kafka, rate limiting, and audit wiring
- Background hosted services for initialization, outbox publishing, cleanup, and admin seeding
- KafkaFlow handlers per event type in workers and API-side messaging helpers
- RFC 7807-style problem responses via custom exception/problem-details infrastructure
- Correlation ID middleware and outbound propagation for tracing consistency

## Domain and Data Patterns
- RavenDB documents for users, tracks, playlists, upload sessions, refresh tokens, outbox messages, telemetry aggregates, and audit entries
- Static RavenDB indexes support search, retention, pagination, and admin workflows
- Track lifecycle is stateful: upload session -> processing -> ready/failed -> soft delete -> physical deletion
- Outbox pattern is used to bridge RavenDB writes and Kafka publication

## Security and Reliability Patterns
- JWT bearer auth plus refresh token rotation
- Argon2 password hashing
- Role- and policy-based authorization, including admin-only flows
- Login-specific rate limiting and a broader rate limiter pipeline
- Encrypted cache wrapper around Redis/Garnet for sensitive streaming data
- Health checks distinguish required dependencies from optional ones based on feature flags and environment

## Frontend Patterns
- pnpm workspace with `apps/player`, `apps/admin`, and shared `packages/*`
- Vue Router route guards based on Pinia auth stores
- `@novatune/api-client` is generated via Orval and wrapped with a custom Axios instance
- `@novatune/core` centralizes auth storage, HTTP behavior, telemetry helpers, and device utilities
- `@novatune/ui` exposes basic shared components and toast/composable helpers

## Test Patterns
- Unit tests use xUnit + Shouldly with fakes for infrastructure-heavy services
- Integration tests use `Aspire.Hosting.Testing` and cover auth, upload, tracks, playlists, streaming, telemetry, admin, and basic web behavior
- Frontend UI coverage now includes Playwright + TypeScript specs under `src/ui_tests/host`, targeting the player and admin SPAs through the Development AppHost stack
