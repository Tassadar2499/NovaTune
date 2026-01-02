# NovaTune Backend Service — Functional Requirements

This document captures functional requirements for the NovaTune music player backend service, derived from:
- `doc/diagrams/context.puml`
- `doc/diagrams/component.puml`
- `doc/requirements/stack.md`
- Current implementation stubs and domain models under `src/NovaTuneApp/NovaTuneApp.ApiService/`
- Clarifications captured in `doc/requirements/functional/questions.md`

It is intentionally implementation-agnostic where the repo is still scaffolded (e.g., controllers listed in diagrams do not yet exist in code).

## 0. Scope

### 0.1 In scope
- **API service**: HTTP backend responsible for authentication, uploads, streaming URL issuance, track management, and publishing events.
- **Background workers**: Asynchronous processing for audio metadata/waveforms, analytics aggregation, and lifecycle cleanup triggered by events.

### 0.2 External dependencies
- RavenDB: track metadata, analytics, playlists (system of record).
- MinIO (S3-compatible): audio object storage.
- Redpanda (Kafka-compatible): event streaming backbone.
- Garnet (Redis-compatible): distributed cache for tokens and presigned URLs.

### 0.3 Clarified decisions
- **Identifiers**: external identifiers (`UserId`, `TrackId`, etc.) use ULID and must be consistent across API payloads and events.
- **Topic naming**: `{env}` is the authoritative environment prefix (e.g., `dev`, `staging`, `prod`) and is required/configured per deployment.
- **Consistency model**: clients should expect eventual consistency (e.g., track moves from `Processing` to `Ready` asynchronously).
- **Error contract**: API errors use RFC 7807 Problem Details.
- **Rate limiting**: explicit rate limits are required for login, upload initiation, playback URL issuance, and telemetry ingestion (values TBD/configurable).

## 1. Actors

- **Listener**: uploads and streams personal tracks.
- **Admin**: operations/compliance; moderates content; views analytics.

## 2. Domain Objects (current models)

- **User**
  - Fields: `Email`, `DisplayName`, `PasswordHash`, timestamps, `Status`.
  - Validation: email format, display name length constraints.
  - Status: `Active`, `Disabled`, `PendingDeletion`.
  - Authorization: Admins are represented as a separate user type and carry an `admin` role claim.
- **Track**
  - Fields: `UserId`, `Title`, optional `Artist`, `Duration`, `ObjectKey`, optional `Checksum`, optional `AudioMetadata`, timestamps, `Status`.
  - Validation: `Title` length constraints; `UserId` and `ObjectKey` required.
  - Status: `Processing`, `Ready`, `Failed`, `Deleted`.
- **AudioMetadata**
  - `Format`, `Bitrate`, `SampleRate`, `Channels`, `FileSizeBytes`, `MimeType`.

### 2.1 Identifiers

- API and event identifiers (`UserId`, `TrackId`, `PlaylistId`, etc.) shall be ULID values serialized as strings.
- RavenDB document IDs may remain an internal persistence detail, but must not leak as the external/API identifier.

## 3. Functional Requirements

### Req 1.x — Authentication & Authorization

- **Req 1.1** The system shall allow a Listener to register with `Email`, `DisplayName`, and password (stored as a `PasswordHash`).
- **Req 1.2** The system shall allow a Listener to log in and receive a JWT access token and refresh token (refresh flow).
- **Req 1.3** The system shall enforce user status:
  - `Active`: normal access.
  - `Disabled`: cannot authenticate and/or cannot access protected operations.
  - `PendingDeletion`: only login and streaming are allowed; eligible for cleanup workflows within 30 days.
- **Req 1.4** The system shall authorize API operations by role claim (Listener vs Admin), using the `admin` role claim for Admin authorization.
- **Req 1.5** The system shall support token/session revocation semantics:
  - Logout revokes only the current session.
  - Password change and Admin disable revocation semantics are TBD.

#### Req 1.x — Clarifications

- **Password policy**: no required minimum length/complexity beyond being non-empty (additional constraints TBD).
- **Password hashing**: use Argon2id (preferred) or bcrypt (acceptable); parameterization is TBD.
- **Email verification**: email confirmation is not required before `Status=Active`.
- **Refresh tokens**:
  - One-time use rotation strategy.
  - TTL: 1 hour.
  - Max concurrent sessions/devices per user: 5.
  - Stored hashed in RavenDB.

### Req 2.x — Track Upload (create + store audio + metadata record)

- **Req 2.1** The system shall allow a Listener to initiate an upload for an audio file by returning a presigned MinIO URL for direct upload (PUT/POST).
- **Req 2.2** The system shall validate supported audio formats using MIME type and reject unsupported formats (exact supported types TBD/configured).
- **Req 2.3** The system shall store uploaded audio content in MinIO and associate it with a stable `ObjectKey` that is guess-resistant and user-scoped.
- **Req 2.4** The system shall create a track metadata record in RavenDB only after the upload succeeds.
- **Req 2.5** The system shall set new tracks to `Status=Processing` by default.
- **Req 2.6** The system shall publish an `AudioUploadedEvent` exactly-once after successful upload including:
  - `TrackId`, `UserId`, `ObjectKey`, `MimeType`, `FileSizeBytes`, `CorrelationId`, `Timestamp`, `SchemaVersion`.

#### Req 2.x — Clarifications

- Upload completion source of truth is MinIO event notification (not a client callback).
- Upload initiation must create enough server-side state to correlate the MinIO completion notification back to the initiating user and object key.
- Deduplication: compute checksum after upload completion (checksum algorithm and comparison rules TBD).
- Limits/quotas: enforce max upload size, max duration, and per-user quotas (storage, track count); values TBD/configurable.
- Exactly-once event publication (`AudioUploadedEvent`) requires durable semantics (e.g., outbox); concrete mechanism is TBD.

### Req 3.x — Post-upload Processing (workers)

- **Req 3.1** The system shall asynchronously process uploaded audio (metadata extraction; waveform generation).
- **Req 3.2** The system shall consume `AudioUploadedEvent` messages and invoke track processing logic for the referenced `TrackId`.
- **Req 3.3** The processor shall fetch audio from MinIO by `ObjectKey`, extract `AudioMetadata`, compute duration, and persist results to RavenDB.
- **Req 3.4** The processor shall transition track `Status`:
  - `Ready` on successful processing.
  - `Failed` on unrecoverable processing error.
- **Req 3.5** Processing shall be idempotent per `TrackId` (replayed events must not corrupt state).

#### Req 3.x — Clarifications

- Retries: 3 processing retries (backoff and DLQ semantics TBD).
- Idempotency: safe to repeat overwriting `AudioMetadata` and regenerating waveform artifacts; processing must not transition `Ready` back to `Processing`.
- Waveform artifacts: stored as a MinIO sidecar object in WAV format.
- Concurrency: processing should be limited globally.

### Req 4.x — Storage Access & Lifecycle

- **Req 4.1** The system shall support reading/writing audio objects in MinIO and managing object lifecycle (delete/versioning/lifecycle rules).
- **Req 4.2** The system shall generate short-lived presigned MinIO URLs for upload and streaming, and cache them in Garnet/Redis with a TTL.
- **Req 4.3** Presigned URLs shall be scoped to the requesting user and track (no cross-user access).
- **Req 4.4** On track deletion, the system shall:
  - Invalidate cached presigned URLs for that user+track.
  - Schedule deletion of storage objects after a grace period.

#### Req 4.x — Clarifications

- Presigned URL TTL values (upload and streaming) are TBD/configurable.
- Cached URL keying: user+track is sufficient (variants/format/version keying is TBD).
- Deletion grace period: 30 days; users can undo/restore a deletion within the grace window.

### Req 5.x — Streaming

- **Req 5.1** The system shall allow a Listener to request playback for a track they own (or are otherwise permitted to access).
- **Req 5.2** The system shall return a short-lived streaming URL (presigned GET), reusing cached URLs where valid.
- **Req 5.3** The streaming solution shall support range requests (byte-range playback).
- **Req 5.4** The system shall emit playback telemetry suitable for analytics (at minimum: play start/stop, duration/position summaries).

#### Req 5.x — Clarifications

- Streaming is always direct-from-MinIO via presigned GET (the API does not proxy bytes).
- Telemetry mechanism: client-reported events.
- Analytics retention period: 30 days.

### Req 6.x — Track Management (library)

- **Req 6.1** The system shall allow a Listener to list/browse their tracks with pagination and filters (e.g., status, search by title/artist).
- **Req 6.2** The system shall allow a Listener to update permitted metadata fields (`Title`, `Artist`) while enforcing validation constraints (editing while `Status=Processing` is TBD).
- **Req 6.3** The system shall allow a Listener to soft-delete a track (status change) and publish a `TrackDeletedEvent` including:
  - `TrackId`, `UserId`, `Timestamp`, `SchemaVersion`.
- **Req 6.4** The system shall expose track details including processing status and extracted metadata.

#### Req 6.x — Clarifications

- Sorting/filtering: support sort orders (recent, title, artist) and filters with case-insensitive, partial-match search and status filters (exact API surface TBD).
- Sharing: no sharing across users; ownership is per-user only.

### Req 7.x — Playlists (declared in stack; not yet modeled)

- **Req 7.1** The system shall allow a Listener to create, rename, and delete playlists.
- **Req 7.2** The system shall allow a Listener to add/remove/reorder tracks within a playlist.
- **Req 7.3** The system shall persist playlists in RavenDB and enforce ownership and authorization rules.

#### Req 7.x — Clarifications

- Constraints: max playlists per user and max tracks per playlist are required but values are TBD/configurable.
- Duplicates: duplicate tracks in a playlist are allowed.
- Ordering: playlists require stable ordering with explicit positions.
- Future: the data model should anticipate playlist sharing/collaboration (while remaining private by default for now).

### Req 8.x — Error Handling & Rate Limiting

- **Req 8.1** The API shall return errors using RFC 7807 Problem Details for validation, authentication, authorization, and not-found failures.
- **Req 8.2** The system shall implement explicit rate limits for:
  - Login
  - Upload initiation
  - Playback URL issuance
  - Telemetry ingestion
  (exact limits TBD/configurable)

### Req 9.x — Analytics & Events

- **Req 9.1** The system shall use Redpanda topics:
  - `{env}-audio-events`
  - `{env}-track-deletions`
  and include schema-versioning metadata for forwards/backwards compatibility.
- **Req 9.2** The system shall store analytics aggregates in RavenDB for Admin review.
- **Req 9.3** The system shall propagate and store `CorrelationId` across upload/processing/telemetry for tracing and debugging; `CorrelationId` originates in the API gateway.
- **Req 9.4** The system shall encode events as JSON.
- **Req 9.5** The system should use `TrackId` as the topic partition key for ordering/partitioning (other ordering guarantees TBD).

### Req 10.x — Session, Token, and URL Caching

- **Req 10.1** The system shall cache session/token-related data (refresh flow, revocation flags) in Garnet/Redis with TTL (what is cached vs stored in RavenDB is TBD).
- **Req 10.2** The system shall cache presigned URLs keyed by user+track and invalidate them at minimum on track deletion and logout (additional triggers TBD).
- **Req 10.3** Cache entries that include full presigned URLs shall be encrypted at rest.
- **Req 10.4** Cache behavior (key prefix and TTLs) shall be configurable via app configuration (presigned URL TTLs are TBD).

### Req 11.x — Admin / Moderation

- **Req 11.1** The system shall allow Admins to list/search users and update user status (enable/disable/pending deletion).
- **Req 11.2** The system shall allow Admins to list/search tracks across users and delete/moderate tracks, and shall require audit logs and reason codes for Admin actions.
- **Req 11.3** The system shall allow Admins to view analytics dashboards (per-track play counts and recent activity at minimum).

#### Req 11.x — Clarifications

- Moderation semantics:
  - Delete: removes the track from all playlists and prevents further streaming.
  - Moderate: marks the track for review.
  - Disable: prevents streaming but keeps the track accessible for review.

## 4. Implementation Notes (current repo state)

- The API project currently exposes only sample endpoints (`/` and `/weatherforecast`) and sets up KafkaFlow + Garnet cache; the controllers/services shown in `doc/diagrams/component.puml` are not yet implemented.
- The event types and KafkaFlow consumers/producers are present, along with handler stubs and cache invalidation behavior for deletions.
