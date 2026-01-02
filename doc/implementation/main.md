# NovaTune — Implementation Plan

This document turns the requirements in:
- `doc/requirements/stack.md`
- `doc/requirements/functional/`
- `doc/requirements/non-functional/`
into an actionable implementation plan aligned with the current repo structure:
- `src/NovaTuneApp/NovaTuneApp.AppHost` (Aspire host)
- `src/NovaTuneApp/NovaTuneApp.ApiService` (API + domain models)
- `src/NovaTuneApp/NovaTuneApp.Web` (frontend)
- `src/NovaTuneApp/NovaTuneApp.ServiceDefaults` (shared hosting/logging defaults)

## 1. Scope & goals (MVP)

### In scope
- Listener experience: register/login, upload tracks, see processing status, stream tracks, manage library and playlists.
- Admin experience: user moderation, track moderation/deletion, basic analytics views.
- Event-driven processing: upload completion → processing → ready.

### Out of scope (initially)
- Multi-region, complex DR automation (see `NF-1.3`, `NF-6.5`).
- Sharing/collaboration between users (explicitly deferred in functional clarifications).
- Full-featured feature-flag service (configuration-only knobs are sufficient; `NF-5.4`).

## 2. Architecture target (high-level)

### Components
- **API service** (`NovaTuneApp.ApiService`)
  - HTTP endpoints (auth, upload initiation, streaming URL issuance, track management, playlists, telemetry ingestion, admin).
  - Publishes domain events to Redpanda.
  - Performs authorization and enforces rate limits and quotas.
- **Worker workloads** (new projects; run as separate deployments, `NF-1.1`)
  - **Upload completion ingestor**: consumes MinIO bucket notifications and turns them into domain actions (create track record + publish `AudioUploadedEvent`) (`Req 2.4`, `Req 2.6`).
  - **Audio processor**: consumes `AudioUploadedEvent` and runs ffprobe/ffmpeg to extract metadata and create waveform sidecar objects (`Req 3.x`).
  - **Deletion/lifecycle worker**: consumes `TrackDeletedEvent` and performs cache invalidation + scheduled physical deletion after grace period (`Req 4.4`, `NF-6.1`).
  - **Analytics worker**: consumes telemetry events (or aggregates from RavenDB) into admin-facing aggregates (`Req 9.2`).
- **API gateway** (YARP; `stack.md`)
  - Routes traffic to API/web.
  - Provides correlation header propagation (`Req 9.3`).

### External dependencies (Aspire-managed for local dev)
- RavenDB (system of record for metadata, analytics, playlists).
- MinIO (audio object storage + waveform sidecars).
- Redpanda (event streaming: `{env}-audio-events`, `{env}-track-deletions`).
- Garnet (Redis protocol): cache for presigned URLs and session/token artifacts.

## 3. Cross-cutting implementation decisions (do early)

### 3.1 External identifiers (ULID)
Requirements mandate ULID as external IDs serialized as strings (functional scope `0.3` and domain objects `2.1 Identifiers`).
- Adopt a single ULID implementation for .NET and standardize:
  - API payloads: `string` ULID.
  - Event payloads: `string` ULID.
  - RavenDB docs: store ULID as an explicit field; RavenDB document IDs remain internal and must not leak.
- Migration note: current stubs use `Guid` for events; update event contracts and any cache keys accordingly.

### 3.2 Error contract and validation
- Standardize on RFC 7807 Problem Details (`Req 8.1`) for all controllers and model binding errors.
- Centralize validation attributes in `NovaTuneApp.ApiService` (per repo guidelines) and use them consistently for:
  - DTOs (API requests/responses).
  - Domain models (invariants).

### 3.3 Resilience, timeouts, and dependency isolation
Implement baseline resilience aligned to `NF-1.4` and `NF-2.2`:
- Per-dependency timeouts and retry budgets (bounded, idempotent-only writes).
- Bulkheads per dependency client (bounded concurrency).
- Circuit breakers for repeated failures.

### 3.4 Observability and correlation
Implement the minimum observability spine upfront (`NF-4.x`, `stack.md`):
- Structured JSON logging (Serilog) with a stable `CorrelationId`.
- Distributed traces via OpenTelemetry and `traceparent` propagation.
- Metrics for API + workers (request latency/error rate; consumer lag; processing outcomes).
- Redaction rules (`NF-4.5`): never log passwords/tokens/presigned URLs/object keys.

### 3.5 Security foundations
Implement baseline security to avoid rework later (`NF-3.x`):
- TLS termination at ingress (local dev via Aspire is fine).
- Refresh token storage hashed in RavenDB (`Req 1.x clarifications`).
- Cache encryption at rest for presigned URLs (`Req 10.3`, `NF-3.2`), with key rotation support.
- Least-privilege credentials and no secrets in repo-tracked configs (`NF-3.4`).

## 4. Milestones (recommended delivery order)

### Milestone 0 — Infrastructure & local dev composition
Goal: make local runs mirror production topology early.
- Extend `NovaTuneApp.AppHost` to include RavenDB + MinIO + Redpanda + Garnet.
- Add per-service health endpoints and readiness checks (`NF-1.2`):
  - API ready: RavenDB + Redpanda required; MinIO required for upload initiation/stream URL issuance; cache optional.
  - Worker ready: Redpanda + worker-specific dependencies.
- Add configuration validation at startup (`NF-5.1`): `{env}` topic prefix, TTLs, quotas, crypto keys, rate limit settings.
- Add OpenAPI surface and UI (Scalar per `stack.md`) and ensure it’s wired in non-dev as appropriate.

Requirements covered: `NF-1.1`, `NF-1.2`, `NF-5.1`, `NF-4.x`.

### Milestone 1 — Authentication & authorization
Goal: enable secure identity and role separation for Listener vs Admin.
- API endpoints:
  - `POST /auth/register` (Req 1.1)
  - `POST /auth/login` (Req 1.2)
  - `POST /auth/refresh` (refresh rotation; one-time use; TTL 1h)
  - `POST /auth/logout` (revokes current session; Req 1.5)
- Identity store:
  - Implement ASP.NET Identity with RavenDB stores (`stack.md`).
  - Persist hashed refresh tokens (one-time rotation) + session limits (max 5 devices).
  - Enforce `UserStatus` rules (`Req 1.3`).
- Rate limits for auth endpoints (`Req 8.2`, `NF-2.5`).

Requirements covered: `Req 1.x`, `NF-3.x`, `NF-2.2`, `NF-2.5`.

### Milestone 2 — Upload initiation + MinIO notification ingestion
Goal: allow direct-to-MinIO uploads with MinIO as the source of truth for completion.
- API endpoint:
  - `POST /tracks/upload/initiate` returns presigned upload URL + upload metadata (Req 2.1).
  - Validate supported MIME types and file size limits (Req 2.2, `NF-2.4`).
- Server-side correlation state (required; Req 2 clarifications):
  - Create an `UploadSession` (or equivalent) document in RavenDB containing: `UploadId`, `UserId`, reserved `TrackId` (ULID), `ObjectKey`, expected `MimeType`, max allowed size, creation time, expiry.
  - Do not create the Track record until MinIO confirms upload completion (`Req 2.4`).
- MinIO integration:
  - Configure bucket notifications to Redpanda (or a worker-consumable queue).
  - Implement a dedicated worker that consumes object-created events, validates them against the `UploadSession`, and then:
    - Creates the `Track` record (`Status=Processing`, `ObjectKey`, etc.).
    - Publishes `AudioUploadedEvent` durably (`Req 2.6`).
- Event publication durability:
  - Implement an outbox in RavenDB for “must publish” events (`NF-5.2`, `Req 2.6` exactly-once intent).

Requirements covered: `Req 2.x`, `NF-5.2`, `NF-6.2`, `NF-1.4`.

### Milestone 3 — Post-upload audio processing worker (ffprobe/ffmpeg)
Goal: process uploaded tracks asynchronously and transition to `Ready`.
- Consume `AudioUploadedEvent` (partition key: `TrackId`; `Req 9.5`).
- Implement bounded concurrency and retries with DLQ semantics (`Req 3.5`, `NF-2.1`, open items).
- Processing pipeline:
  - Fetch audio from MinIO by `ObjectKey`.
  - Use `ffprobe` to extract technical metadata and duration (`Req 3.3`).
  - Generate waveform sidecar object (store in MinIO; `Req 3 clarifications`).
  - Persist results to RavenDB with optimistic concurrency and monotonic state transitions (`NF-6.2`).
- Mark track `Ready` or `Failed` (`Req 3.4`).

Requirements covered: `Req 3.x`, `NF-2.1`, `NF-6.2`, `NF-4.2`.

### Milestone 4 — Streaming URL issuance + caching
Goal: allow streaming via short-lived presigned GET without proxying bytes.
- API endpoint:
  - `POST /tracks/{trackId}/stream` issues presigned GET URL (`Req 5.1`, `Req 5.2`).
  - Enforce ownership and status checks (no issuance for `Deleted` or not permitted; `NF-6.1`).
- Cache behavior:
  - Cache presigned URLs in Garnet by user+track (`Req 10.2`).
  - TTL slightly shorter than presign expiry (`NF-3.3`).
  - Encrypt cached values (`Req 10.3`).
- Range requests:
  - Ensure MinIO presign and bucket/object headers allow byte-range playback (`Req 5.3`).

Requirements covered: `Req 5.x`, `Req 10.x`, `NF-2.3`, `NF-3.3`, `NF-6.1`.

### Milestone 5 — Track management + lifecycle cleanup
Goal: manage the user library and enforce deletion integrity.
- API endpoints:
  - `GET /tracks` list/search/filter/sort with pagination (`Req 6.1`).
  - `GET /tracks/{trackId}` details (`Req 6.4`).
  - `PATCH /tracks/{trackId}` update title/artist with validation (`Req 6.2`, `NF-6.2` merge policy).
  - `DELETE /tracks/{trackId}` soft-delete (`Req 6.3`).
  - Optional: `POST /tracks/{trackId}/restore` within grace window (aligned to `NF-6.1`).
- Deletion semantics:
  - Publish `TrackDeletedEvent` after state change.
  - Invalidate cached presigned URLs immediately.
  - Schedule physical deletion after 30 days (configurable; `Req 4.4`, `NF-6.3`).
  - Ensure repeatable deletion jobs (safe to re-run; `NF-6.1`).

Requirements covered: `Req 6.x`, `Req 4.x`, `NF-6.x`.

### Milestone 6 — Playlists
Goal: enable playlist CRUD with stable ordering.
- Data model:
  - Playlist doc with `PlaylistId` (ULID), `UserId`, name, and ordered list of track references + positions.
  - Enforce ownership, limits, and duplicates policy (`Req 7 clarifications`, `NF-2.4` quotas).
- API endpoints:
  - Create/rename/delete playlists.
  - Add/remove/reorder tracks.

Requirements covered: `Req 7.x`.

### Milestone 7 — Telemetry ingestion + analytics aggregation
Goal: store short-retention analytics and make it visible to Admin.
- API endpoint:
  - `POST /telemetry/playback` (or similar) for client-reported play events (`Req 5.4`).
  - Rate limit telemetry ingestion (`Req 8.2`, `NF-2.5`).
- Pipeline:
  - Publish telemetry events to Redpanda (recommended) and aggregate in a worker.
  - Store aggregates in RavenDB for admin dashboards (`Req 9.2`).
  - Enforce analytics retention (30 days configurable; `NF-6.3`).

Requirements covered: `Req 5.4`, `Req 9.x`, `NF-6.3`, `NF-4.2`.

### Milestone 8 — Admin / moderation + audit logs
Goal: allow administrative operations with auditability.
- Admin APIs:
  - Search/list users; update status (`Req 11.1`).
  - Search/list tracks across users; delete/moderate with reason codes (`Req 11.2`).
  - View analytics dashboards (`Req 11.3`).
- Audit logging:
  - Record: actor identity, timestamp, action, target, reason codes (`NF-3.5`).
  - Retention 1 year and access restricted to Admin role.
  - Tamper-evidence mechanism remains TBD (track as an explicit open item).

Requirements covered: `Req 11.x`, `NF-3.5`.

## 5. Work breakdown (by repo module)

### `src/NovaTuneApp/NovaTuneApp.AppHost`
- Add RavenDB, MinIO, and any required initialization (buckets, topic creation, notification wiring).
- Ensure services run as independent workloads to match `NF-1.1`.

### `src/NovaTuneApp/NovaTuneApp.ApiService`
- Add controllers/endpoints for functional requirements (`Req 1.x`–`Req 11.x`).
- Add RavenDB persistence layer (repositories or document sessions).
- Add MinIO client integration for presign and object operations.
- Add cache abstraction that supports encryption and key versioning.
- Add event contracts (JSON, versioned, ULID identifiers) and outbox publisher.

### New projects (recommended)
- `NovaTuneApp.Workers.UploadIngestor` (MinIO notification consumer → track creation + publish `AudioUploadedEvent`).
- `NovaTuneApp.Workers.AudioProcessor` (`AudioUploadedEvent` consumer → metadata/waveform → RavenDB updates).
- `NovaTuneApp.Workers.Lifecycle` (`TrackDeletedEvent` consumer → cache invalidation + scheduled physical delete).
- `NovaTuneApp.Workers.Analytics` (telemetry consumer → aggregate → RavenDB).

### `src/unit_tests/` and `src/integration_tests/`
- Unit tests for:
  - Validation and domain transitions (monotonic status; merge policy; ULID parsing).
  - Cache encryption/decryption and key rotation behavior.
  - Rate limit policies (policy selection and configuration validation).
- Integration tests (Aspire host) for:
  - Upload initiation + MinIO notification path.
  - Event publication + worker consumption + track transitions.

## 6. Open items to resolve (track explicitly)

From NFRs and functional clarifications:
- Worker retry/backoff/DLQ semantics (and how KafkaFlow is configured for poison messages).
- Audit log tamper-evidence strategy.
- Concrete bulkhead/circuit breaker settings per dependency and endpoint class.
- Concrete concurrency limits for processing and telemetry ingestion.
- Supported MIME types and exact validation rules for audio formats.
- Upload quota and playlist quota values (keep configurable).

From current repo vs `stack.md`:
- Frontend technology mismatch: repo contains ASP.NET Core Razor Components (`NovaTuneApp.Web`), while `stack.md` calls for Vue.js + TypeScript. Decide whether to:
  - Replace the frontend project, or
  - Treat the current web as an interim UI while a Vue app is developed separately.

## 7. Definition of done (per milestone)

- Functional endpoints implemented and documented (OpenAPI) for the milestone’s requirements.
- Unit tests added for new domain rules; integration tests where cross-service behavior is involved.
- Health/readiness checks reflect actual dependencies and degrade correctly (cache optional).
- Observability in place (logs, traces, metrics) with `CorrelationId` propagation and redaction.
- Security requirements met (token storage, cache encryption, least-privilege config, rate limits).
