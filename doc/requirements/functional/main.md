# NovaTune Backend Service — Functional Requirements

This document captures functional requirements for the NovaTune music player backend service, derived from:
- `doc/diagrams/context.puml`
- `doc/diagrams/component.puml`
- `doc/requirements/stack.md`
- Current implementation stubs and domain models under `src/NovaTuneApp/NovaTuneApp.ApiService/`

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

## 1. Actors

- **Listener**: uploads and streams personal tracks.
- **Admin**: operations/compliance; moderates content; views analytics.

## 2. Domain Objects (current models)

- **User**
  - Fields: `Email`, `DisplayName`, `PasswordHash`, timestamps, `Status`.
  - Validation: email format, display name length constraints.
  - Status: `Active`, `Disabled`, `PendingDeletion`.
- **Track**
  - Fields: `UserId`, `Title`, optional `Artist`, `Duration`, `ObjectKey`, optional `Checksum`, optional `AudioMetadata`, timestamps, `Status`.
  - Validation: `Title` length constraints; `UserId` and `ObjectKey` required.
  - Status: `Processing`, `Ready`, `Failed`, `Deleted`.
- **AudioMetadata**
  - `Format`, `Bitrate`, `SampleRate`, `Channels`, `FileSizeBytes`, `MimeType`.

Note: Models currently use Raven-like string IDs (e.g., `tracks/1`), while events use `Guid` IDs. This mismatch must be resolved as part of API design.

## 3. Functional Requirements

### Req 1.x — Authentication & Authorization

- **Req 1.1** The system shall allow a Listener to register with `Email`, `DisplayName`, and password (stored as a `PasswordHash`).
- **Req 1.2** The system shall allow a Listener to log in and receive a JWT access token and refresh token (refresh flow).
- **Req 1.3** The system shall enforce user status:
  - `Active`: normal access.
  - `Disabled`: cannot authenticate and/or cannot access protected operations.
  - `PendingDeletion`: access limited per policy; eligible for cleanup workflows.
- **Req 1.4** The system shall authorize API operations by role/scope (Listener vs Admin).
- **Req 1.5** The system shall support token/session revocation semantics (e.g., logout, password change, admin disable).

### Req 2.x — Track Upload (create + store audio + metadata record)

- **Req 2.1** The system shall allow a Listener to initiate an upload for an audio file.
- **Req 2.2** The system shall validate the incoming audio format and reject unsupported formats before persisting as a playable track.
- **Req 2.3** The system shall store uploaded audio content in MinIO and associate it with a stable `ObjectKey`.
- **Req 2.4** The system shall create a track metadata record in RavenDB with required fields and validation enforced.
- **Req 2.5** The system shall set new tracks to `Status=Processing` by default.
- **Req 2.6** The system shall publish an `AudioUploadedEvent` after successful upload including:
  - `TrackId`, `UserId`, `ObjectKey`, `MimeType`, `FileSizeBytes`, `CorrelationId`, `Timestamp`, `SchemaVersion`.

### Req 3.x — Post-upload Processing (workers)

- **Req 3.1** The system shall asynchronously process uploaded audio (metadata extraction; waveform generation).
- **Req 3.2** The system shall consume `AudioUploadedEvent` messages and invoke track processing logic for the referenced `TrackId`.
- **Req 3.3** The processor shall fetch audio from MinIO by `ObjectKey`, extract `AudioMetadata`, compute duration, and persist results to RavenDB.
- **Req 3.4** The processor shall transition track `Status`:
  - `Ready` on successful processing.
  - `Failed` on unrecoverable processing error.
- **Req 3.5** Processing shall be idempotent per `TrackId` (replayed events must not corrupt state).

### Req 4.x — Storage Access & Lifecycle

- **Req 4.1** The system shall support reading/writing audio objects in MinIO and managing object lifecycle (delete/versioning/lifecycle rules).
- **Req 4.2** The system shall generate short-lived presigned MinIO URLs for upload and streaming, and cache them in Garnet/Redis with a TTL.
- **Req 4.3** Presigned URLs shall be scoped to the requesting user and track (no cross-user access).
- **Req 4.4** On track deletion, the system shall:
  - Invalidate cached presigned URLs for that user+track.
  - Schedule deletion of storage objects after a grace period.

### Req 5.x — Streaming

- **Req 5.1** The system shall allow a Listener to request playback for a track they own (or are otherwise permitted to access).
- **Req 5.2** The system shall return a short-lived streaming URL (presigned GET), reusing cached URLs where valid.
- **Req 5.3** The streaming solution shall support range requests (byte-range playback).
- **Req 5.4** The system shall emit playback telemetry suitable for analytics (at minimum: play start/stop, duration/position summaries).

### Req 6.x — Track Management (library)

- **Req 6.1** The system shall allow a Listener to list/browse their tracks with pagination and filters (e.g., status, search by title/artist).
- **Req 6.2** The system shall allow a Listener to update permitted metadata fields (e.g., `Title`, `Artist`) while enforcing validation constraints.
- **Req 6.3** The system shall allow a Listener to delete a track and publish a `TrackDeletedEvent` including:
  - `TrackId`, `UserId`, `Timestamp`, `SchemaVersion`.
- **Req 6.4** The system shall expose track details including processing status and extracted metadata.

### Req 7.x — Playlists (declared in stack; not yet modeled)

- **Req 7.1** The system shall allow a Listener to create, rename, and delete playlists.
- **Req 7.2** The system shall allow a Listener to add/remove/reorder tracks within a playlist.
- **Req 7.3** The system shall persist playlists in RavenDB and enforce ownership and authorization rules.

### Req 9.x — Analytics & Events

- **Req 9.1** The system shall use Redpanda topics:
  - `{env}-audio-events`
  - `{env}-track-deletions`
  and include schema-versioning metadata for forwards/backwards compatibility.
- **Req 9.2** The system shall store analytics aggregates in RavenDB for Admin review.
- **Req 9.3** The system shall propagate and store `CorrelationId` across upload/processing/telemetry for tracing and debugging.

### Req 10.x — Session, Token, and URL Caching

- **Req 10.1** The system shall cache session/token-related data (refresh flow, revocation flags) in Garnet/Redis with TTL.
- **Req 10.2** The system shall cache presigned URLs keyed by user+track and invalidate them on deletion and relevant security events.
- **Req 10.3** Cache behavior (key prefix and TTLs) shall be configurable via app configuration.

### Req 11.x — Admin / Moderation

- **Req 11.1** The system shall allow Admins to list/search users and update user status (enable/disable/pending deletion).
- **Req 11.2** The system shall allow Admins to list/search tracks across users and delete/moderate tracks.
- **Req 11.3** The system shall allow Admins to view analytics dashboards (per-track play counts and recent activity at minimum).

## 4. Implementation Notes (current repo state)

- The API project currently exposes only sample endpoints (`/` and `/weatherforecast`) and sets up KafkaFlow + Garnet cache; the controllers/services shown in `doc/diagrams/component.puml` are not yet implemented.
- The event types and KafkaFlow consumers/producers are present, along with handler stubs and cache invalidation behavior for deletions.

