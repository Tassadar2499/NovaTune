# NovaTune – Functional Requirements

> **Version:** 2.0
> **Last Updated:** 2025-11-23
> **Status:** Active

This document defines the authoritative functional requirements for NovaTune, a user-uploaded audio management platform. Implementations use ASP.NET Core services, RavenDB for metadata, MinIO for audio storage, NCache for caching, and Kafka/RabbitMQ for background workflows.

Reference requirement IDs (e.g., FR 4.2) in tickets, commits, tests, and deployment notes. See [non_functional.md](non_functional.md) for quality attributes and [stack.md](stack.md) for technology decisions.

---

## Legend

| Symbol | Meaning |
|--------|---------|
| **P1** | Must-have – blocks release if unmet |
| **P2** | Should-have – degrades experience if unmet |
| **P3** | Nice-to-have – enhances experience |
| `[Test]` | Verified via automated tests in CI |
| `[E2E]` | Verified via end-to-end test scenarios |
| `[Manual]` | Verified via manual QA checklist |
| **Actor** | User, Admin, or System |

---

## FR 1. User Management

*Implementation Phase: 2*

- **FR 1.1 Account Creation** `[Test]` **P1**
  - **Actor:** User
  - **Description:** Provide a registration flow using ASP.NET Identity with custom RavenDB `IUserStore` implementation.
  - **Acceptance Criteria:**
    - Registration accepts email, password, and display name.
    - Password requirements: minimum 8 characters, at least 1 uppercase, 1 lowercase, 1 digit, 1 special character.
    - Email uniqueness enforced at database level.
    - Account verification email sent within 60 seconds of registration.
    - Duplicate registration attempts return appropriate error (409 Conflict).
  - **API:** `POST /api/v1/auth/register`
  - *Quality: NF-3.4, NF-3.5*

- **FR 1.2 Authentication** `[Test]` **P1**
  - **Actor:** User
  - **Description:** Allow logins with valid credentials and maintain JWT-based sessions with refresh token flow.
  - **Acceptance Criteria:**
    - Login accepts email and password.
    - Successful login returns JWT access token (15 min TTL) and refresh token (7 day TTL).
    - Failed login increments counter; lock account for 15 minutes after 5 consecutive failures.
    - Refresh endpoint issues new access token without re-authentication.
    - Logout invalidates refresh token and propagates to NCache within 5 seconds.
  - **API:** `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`
  - *Quality: NF-3.4, NF-1.2*

- **FR 1.3 Profile Updates** `[Test]` **P2**
  - **Actor:** User
  - **Description:** Users can edit profile attributes persisted in RavenDB.
  - **Acceptance Criteria:**
    - Editable fields: display name (2-50 chars), avatar URL, bio (max 500 chars).
    - Avatar upload accepts JPEG/PNG, max 2 MB, resized to 256x256.
    - Profile changes emit `user-profile-updated` event to Kafka.
    - Concurrent updates handled via RavenDB optimistic concurrency.
  - **API:** `GET /api/v1/users/me`, `PATCH /api/v1/users/me`, `POST /api/v1/users/me/avatar`
  - *Quality: NF-6.2*

- **FR 1.4 Account Removal** `[Test]` **P1**
  - **Actor:** User
  - **Description:** Users may delete their account, triggering cascading data removal.
  - **Acceptance Criteria:**
    - Deletion requires password confirmation.
    - Cascade deletes: RavenDB user document, all owned tracks (FR 6.3), playlists (FR 7.x), share links (FR 8.x).
    - MinIO objects scheduled for batch deletion within 24 hours.
    - NCache entries invalidated immediately.
    - Kafka tombstone events published for compaction.
    - Deletion confirmation sent via email.
    - Soft-delete with 30-day recovery window before permanent deletion.
  - **API:** `DELETE /api/v1/users/me`
  - *Quality: NF-3.3, NF-6.1*

---

## FR 2. Audio Upload

*Implementation Phase: 3*

- **FR 2.1 Supported Formats** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Accept whitelisted audio formats enforced at API and processing layers.
  - **Acceptance Criteria:**
    - Supported formats: MP3, WAV, FLAC, AAC, OGG, M4A.
    - Format whitelist configurable via Kubernetes ConfigMap (FR 11.4).
    - MIME type validation at upload start; reject mismatched Content-Type.
    - FFprobe validation confirms actual file format matches declared type.
  - *Quality: NF-3.5*

- **FR 2.2 Validation** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Reject unsupported types and enforce configurable limits before MinIO upload.
  - **Acceptance Criteria:**
    - Maximum file size: 200 MB (configurable via FR 11.4).
    - Maximum duration: 60 minutes per track.
    - Reject files with malformed headers or corrupted audio streams.
    - File name sanitization: strip path traversal, limit to 255 chars, alphanumeric + hyphens/underscores.
    - Return 413 (Payload Too Large) for size violations, 415 (Unsupported Media Type) for format violations.
  - *Quality: NF-3.5, NF-1.6*

- **FR 2.3 Storage Pipeline** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Stream uploads into MinIO with versioning for rollback capability.
  - **Acceptance Criteria:**
    - Uploads stream directly to MinIO (no intermediate disk storage on API servers).
    - Object key format: `{environment}/{userId}/{trackId}/{version}/{filename}`.
    - MinIO object versioning enabled; retain last 3 versions.
    - Multipart upload for files >5 MB with configurable part size (5-100 MB).
    - Incomplete multipart uploads cleaned after 24 hours (NF-6.1).
  - *Quality: NF-1.1, NF-5.3*

- **FR 2.4 Metadata Capture** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Store comprehensive metadata for every upload in RavenDB.
  - **Acceptance Criteria:**
    - Required fields captured:
      | Field | Source | Constraints |
      |-------|--------|-------------|
      | `Id` | Generated | GUID format |
      | `Title` | User input or filename | 1-200 chars |
      | `Artist` | User input or "Unknown" | 0-200 chars |
      | `Duration` | FFprobe extraction | Seconds (decimal) |
      | `FileSizeBytes` | Upload | Integer |
      | `MimeType` | Validation | From whitelist |
      | `UserId` | JWT claim | GUID |
      | `ObjectKey` | MinIO | Full path |
      | `BucketName` | Config | Environment-specific |
      | `UploadedAt` | System | UTC timestamp |
      | `Bitrate` | FFprobe | kbps (optional) |
      | `SampleRate` | FFprobe | Hz (optional) |
    - Custom tags: array of strings, max 20 tags, max 50 chars each.
  - *Quality: NF-6.2*

- **FR 2.5 Feedback** `[Test]` **P2**
  - **Actor:** User
  - **Description:** Provide real-time upload progress and completion notifications.
  - **Acceptance Criteria:**
    - Progress events via Server-Sent Events (SSE) or WebSocket at 1-second intervals.
    - Progress payload: `{ percentage, bytesUploaded, bytesTotal, correlationId }`.
    - Success response includes: track ID, metadata summary, streaming URL.
    - Failure response includes: error code, message, correlation ID for support.
    - Correlation ID format: `upload-{timestamp}-{random}`.
  - **API:** `POST /api/v1/tracks/upload`, SSE endpoint `/api/v1/tracks/upload/progress/{correlationId}`
  - *Quality: NF-4.2, NF-7.2*

- **FR 2.6 Background Tasks** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Publish events to enable downstream processing pipelines.
  - **Acceptance Criteria:**
    - Publish `audio-uploaded` event to Kafka `audio-events` topic within 5 seconds of upload completion.
    - Event schema:
      ```json
      {
        "schemaVersion": 1,
        "eventType": "audio-uploaded",
        "trackId": "guid",
        "userId": "guid",
        "objectKey": "string",
        "mimeType": "string",
        "fileSizeBytes": 0,
        "correlationId": "string",
        "timestamp": "ISO8601"
      }
      ```
    - For optional waveform generation (FR 3.3), publish to RabbitMQ `waveform-jobs` queue.
    - Dead-letter handling if consumer fails after 3 retries.
  - *Quality: NF-6.3, NF-9.2*

---

## FR 3. Audio Processing

*Implementation Phase: 3*

- **FR 3.1 Duration Extraction** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Determine track length during ingestion using FFprobe.
  - **Acceptance Criteria:**
    - Duration extracted within 30-second timeout (NF-1.6).
    - Precision: milliseconds, stored as decimal seconds.
    - Fallback: estimate from file size and bitrate if FFprobe fails.
    - Duration stored in RavenDB track document (FR 2.4).
  - *Quality: NF-1.6*

- **FR 3.2 Track IDs** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Generate unique identifiers for each track.
  - **Acceptance Criteria:**
    - Format: GUID v4 (e.g., `550e8400-e29b-41d4-a716-446655440000`).
    - ID generated before upload begins; used in MinIO object key.
    - ID immutable after creation; no reassignment.
    - Collision probability: <1 in 10^18.
  - *Quality: NF-6.2*

- **FR 3.3 Optional Waveform Generation** `[E2E]` **P3**
  - **Actor:** System
  - **Description:** Background workers generate waveform previews for visualization.
  - **Acceptance Criteria:**
    - Consumer subscribes to RabbitMQ `waveform-jobs` queue.
    - Generate 1000-sample waveform array per track using FFmpeg.
    - Store waveform JSON in RavenDB track document or separate MinIO object.
    - Processing timeout: 60 seconds per track.
    - Retry: 3 attempts with exponential backoff before dead-letter.
    - Waveform available at `GET /api/v1/tracks/{id}/waveform`.
  - *Quality: NF-1.6, NF-2.2*

---

## FR 4. Storage & File Management

*Implementation Phase: 4*

- **FR 4.1 Secure Storage** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Store audio objects in MinIO with security controls.
  - **Acceptance Criteria:**
    - All buckets configured as private (no public access).
    - Bucket naming: `novatune-{environment}-audio` (e.g., `novatune-prod-audio`).
    - Credentials stored in Kubernetes secrets; no hardcoded values.
    - MinIO server-side encryption (SSE-S3) with AES-256.
    - Bucket policies deny all cross-user object access.
  - *Quality: NF-3.2, NF-3.1*

- **FR 4.2 Signed URLs** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Issue presigned URLs for secure object access without exposing MinIO endpoints.
  - **Acceptance Criteria:**
    - Presigned URL TTL: 10 minutes (configurable).
    - URLs cached in NCache with key `presigned:{userId}:{trackId}`.
    - Cache TTL: 8 minutes (80% of presigned TTL for safety margin).
    - URL includes signature; cannot be modified to access other objects.
    - Direct MinIO endpoint never exposed to clients.
  - **API:** Internal service; URLs returned via FR 5.2.
  - *Quality: NF-6.4, NF-1.3*

- **FR 4.3 Access Control** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Enforce ownership and sharing rules for track access.
  - **Acceptance Criteria:**
    - Default: tracks accessible only to owning user.
    - Ownership verified at API level before presigned URL generation.
    - Share rules (FR 8.x) checked if requester != owner.
    - Failed access attempts logged with user ID, track ID, timestamp.
    - Return 403 Forbidden for unauthorized access; 404 Not Found if track doesn't exist.
  - *Quality: NF-3.2*

- **FR 4.4 Lifecycle Management** `[Test]` **P2**
  - **Actor:** System
  - **Description:** Automatically clean up orphaned and deleted objects.
  - **Acceptance Criteria:**
    - Background job runs every 6 hours.
    - Consumes Kafka tombstone events from `track-deletions` topic.
    - Deletes MinIO objects 24 hours after tombstone (grace period for undo).
    - Orphan detection: MinIO objects with no RavenDB reference after 7 days.
    - Cleanup metrics published to Aspire dashboard.
  - *Quality: NF-6.1*

---

## FR 5. Audio Streaming

*Implementation Phase: 5*

- **FR 5.1 Playback Gateway** `[E2E]` **P1**
  - **Actor:** User
  - **Description:** Stream uploaded tracks through secure API gateway.
  - **Acceptance Criteria:**
    - Streaming endpoint returns audio with appropriate Content-Type.
    - Support HTTP Range requests for seek functionality.
    - Response headers: `Accept-Ranges: bytes`, `Content-Length`, `Content-Type`.
    - CORS configured for frontend domain(s).
    - Gateway (YARP) routes to presigned MinIO URL transparently.
  - **API:** `GET /api/v1/tracks/{id}/stream`
  - *Quality: NF-1.1, NF-1.5*

- **FR 5.2 Signed Streaming URLs** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Streaming uses time-bound presigned URLs with caching.
  - **Acceptance Criteria:**
    - Check NCache for existing presigned URL before generation.
    - Cache hit returns URL immediately (<50ms).
    - Cache miss generates new presigned URL and caches it.
    - URL keyed by user ID and track ID for isolation.
    - Concurrent requests for same track deduplicated.
  - *Quality: NF-6.4, NF-1.2*

- **FR 5.3 Player Controls** `[E2E]` **P1**
  - **Actor:** User
  - **Description:** Support standard audio player interactions.
  - **Acceptance Criteria:**
    - Play: Start/resume playback from current position.
    - Pause: Stop playback, retain position.
    - Seek: Jump to any position via Range request (HTTP 206).
    - Volume: Client-side control (0-100%).
    - Keyboard shortcuts: Space (play/pause), Arrow keys (seek ±10s).
    - Mobile: touch gestures for seek, play/pause on tap.
  - *Quality: NF-7.1, NF-7.4*

- **FR 5.4 Expiry Handling** `[Test]` **P2**
  - **Actor:** System
  - **Description:** Gracefully handle expired presigned URLs.
  - **Acceptance Criteria:**
    - Client detects 403 Forbidden from MinIO.
    - Client requests new streaming URL from API.
    - API invalidates cached URL and generates fresh presigned URL.
    - Playback resumes from last known position (client responsibility).
    - Maximum 3 automatic retry attempts before user notification.
  - *Quality: NF-2.4, NF-7.2*

---

## FR 6. Track Management

*Implementation Phase: 6*

- **FR 6.1 Track Browsing** `[Test]` **P1**
  - **Actor:** User
  - **Description:** List uploaded tracks with pagination and filtering.
  - **Acceptance Criteria:**
    - Default sort: upload date descending.
    - Pagination: cursor-based, 20 items per page (configurable 10-100).
    - Filter by: title (partial match), artist, tags, date range.
    - Response includes: track metadata, duration, upload date, thumbnail URL (if available).
    - RavenDB static indexes for performant queries.
  - **API:** `GET /api/v1/tracks?sort=uploadedAt&order=desc&cursor={cursor}&limit=20`
  - *Quality: NF-1.4, NF-6.2*

- **FR 6.2 Metadata Editing** `[Test]` **P2**
  - **Actor:** User
  - **Description:** Update track metadata after upload.
  - **Acceptance Criteria:**
    - Editable fields: title, artist, tags, description.
    - Validation rules same as FR 2.4.
    - Optimistic concurrency via RavenDB `@etag`.
    - Publish `track-metadata-updated` event to Kafka.
    - Edit history: retain last 10 versions in track document.
  - **API:** `PATCH /api/v1/tracks/{id}`
  - *Quality: NF-6.3, NF-9.2*

- **FR 6.3 Track Deletion** `[Test]` **P1**
  - **Actor:** User
  - **Description:** Remove tracks with cascading cleanup.
  - **Acceptance Criteria:**
    - Soft-delete: mark as deleted, retain for 30 days.
    - Hard-delete after 30 days or immediate on user request.
    - Cascade: remove from all playlists, invalidate share links.
    - Publish tombstone to Kafka `track-deletions` topic.
    - Invalidate NCache presigned URL entries.
    - MinIO object deletion scheduled (FR 4.4).
    - Undo available within 30 days via `POST /api/v1/tracks/{id}/restore`.
  - **API:** `DELETE /api/v1/tracks/{id}`, `POST /api/v1/tracks/{id}/restore`
  - *Quality: NF-6.1, NF-5.3*

- **FR 6.4 Search** `[Test]` **P2**
  - **Actor:** User
  - **Description:** Full-text search across track metadata.
  - **Acceptance Criteria:**
    - Search fields: title, artist, tags, description.
    - RavenDB full-text index with stemming and stop words.
    - Results ranked by relevance score.
    - Support quoted phrases for exact match.
    - Search scoped to user's own tracks only.
    - Autocomplete suggestions from existing tags.
  - **API:** `GET /api/v1/tracks/search?q={query}`
  - *Quality: NF-1.4*

- **FR 6.5 Sorting** `[Test]` **P2**
  - **Actor:** User
  - **Description:** Sort track listings by various criteria.
  - **Acceptance Criteria:**
    - Sort fields: `uploadedAt`, `title`, `artist`, `duration`, `playCount`.
    - Sort order: `asc` or `desc`.
    - Default: `uploadedAt desc`.
    - Multiple sort fields supported (e.g., `artist asc, title asc`).
    - Sort applied server-side before pagination.
  - **API:** Query parameter `sort=field:order` (e.g., `sort=title:asc`)
  - *Quality: NF-1.4*

---

## FR 7. Playlists

*Implementation Phase: 7 (Optional Tier 1)*

- **FR 7.1 Playlist Creation** `[Test]` **P2**
  - **Actor:** User
  - **Description:** Create named playlists stored in RavenDB.
  - **Acceptance Criteria:**
    - Name: 1-100 characters, unique per user.
    - Description: optional, max 500 characters.
    - Maximum playlists per user: 100 (configurable).
    - Created with empty track list; tracks added via FR 7.2.
    - Playlist ID: GUID format.
  - **API:** `POST /api/v1/playlists`
  - *Quality: NF-6.2*

- **FR 7.2 Playlist Editing** `[Test]` **P2**
  - **Actor:** User
  - **Description:** Add, remove, and manage tracks in playlists.
  - **Acceptance Criteria:**
    - Add track: append to end or insert at position.
    - Remove track: by track ID or position.
    - Maximum tracks per playlist: 500.
    - Duplicate tracks allowed (same track multiple times).
    - Track ordering stored as array of track IDs.
    - Batch operations: add/remove multiple tracks in single request.
  - **API:** `POST /api/v1/playlists/{id}/tracks`, `DELETE /api/v1/playlists/{id}/tracks/{trackId}`
  - *Quality: NF-6.2*

- **FR 7.3 Playlist Reordering** `[E2E]` **P2**
  - **Actor:** User
  - **Description:** Reorder tracks within a playlist.
  - **Acceptance Criteria:**
    - Move track from position A to position B.
    - Drag-and-drop UI updates position in real-time.
    - API accepts array of track IDs in new order.
    - Optimistic concurrency prevents lost updates.
  - **API:** `PUT /api/v1/playlists/{id}/order`
  - *Quality: NF-7.1*

- **FR 7.4 Continuous Playback** `[E2E]` **P2**
  - **Actor:** User
  - **Description:** Play through entire playlist without interruption.
  - **Acceptance Criteria:**
    - Auto-advance to next track on completion.
    - Prefetch next track's presigned URL during current playback.
    - Loop mode: restart from beginning after last track.
    - Shuffle mode: randomize order (client-side).
    - Skip: advance to next/previous track.
    - Queue display: show upcoming tracks.
  - *Quality: NF-7.2, NF-5.2*

- **FR 7.5 Playlist Deletion** `[Test]` **P2**
  - **Actor:** User
  - **Description:** Remove playlists.
  - **Acceptance Criteria:**
    - Deleting playlist does not delete contained tracks.
    - Immediate hard-delete (no soft-delete for playlists).
    - Share links to playlist invalidated (FR 8.x).
  - **API:** `DELETE /api/v1/playlists/{id}`
  - *Quality: NF-6.1*

---

## FR 8. Sharing

*Implementation Phase: 7 (Optional Tier 2)*

- **FR 8.1 Share Links** `[Test]` **P3**
  - **Actor:** User
  - **Description:** Generate shareable links for tracks and playlists.
  - **Acceptance Criteria:**
    - Share link format: `https://{domain}/share/{shareId}`.
    - Share ID: 12-character alphanumeric token (URL-safe).
    - Link metadata stored in RavenDB: target ID, type (track/playlist), creator, expiry.
    - Default expiry: 30 days (configurable per link).
    - Maximum active shares per user: 100.
  - **API:** `POST /api/v1/shares`
  - *Quality: NF-3.2*

- **FR 8.2 Visibility Controls** `[Test]` **P3**
  - **Actor:** User
  - **Description:** Control who can access shared content.
  - **Acceptance Criteria:**
    - Visibility modes:
      | Mode | Description |
      |------|-------------|
      | `private` | Owner only (default) |
      | `link` | Anyone with share link |
      | `public` | Discoverable (future feature) |
    - Per-user sharing: grant access to specific user IDs.
    - Revoke: invalidate share link immediately.
    - Access log: record who accessed shared content.
  - **API:** `PATCH /api/v1/shares/{id}`, `DELETE /api/v1/shares/{id}`
  - *Quality: NF-3.2, NF-4.2*

- **FR 8.3 Secure Streaming** `[Test]` **P2**
  - **Actor:** System
  - **Description:** Shared content streams via presigned URLs.
  - **Acceptance Criteria:**
    - Share link resolves to track/playlist metadata.
    - Streaming still requires presigned URL generation.
    - Anonymous access allowed for `link` visibility mode.
    - Rate limiting: 50 streams per share link per hour.
    - Shared streaming logged for analytics (FR 9.2).
  - *Quality: NF-1.5, NF-3.2*

---

## FR 9. Analytics

*Implementation Phase: 8 (Internal)*

- **FR 9.1 Upload Metrics** `[Test]` **P2**
  - **Actor:** System
  - **Description:** Capture and aggregate upload statistics.
  - **Acceptance Criteria:**
    - Events consumed from Kafka `audio-events` topic.
    - Metrics aggregated per user, per day:
      - Total uploads, successful/failed count.
      - Total bytes uploaded.
      - Average file size, duration.
    - Store aggregations in RavenDB `analytics` collection.
    - Retention: raw events 30 days, aggregations 1 year.
  - *Quality: NF-4.1, NF-6.3*

- **FR 9.2 Playback Metrics** `[Test]` **P2**
  - **Actor:** System
  - **Description:** Track audio playback statistics.
  - **Acceptance Criteria:**
    - Events: play start, play complete, seek, pause.
    - Metrics per track:
      - Play count (completed playbacks only).
      - Total play time.
      - Unique listeners (for shared tracks).
    - User play history: last 100 tracks (optional, privacy-respecting).
    - Real-time play count visible in track metadata.
  - *Quality: NF-4.1*

- **FR 9.3 Admin Dashboards** `[Manual]` **P2**
  - **Actor:** Admin
  - **Description:** Surface system-wide statistics for administrators.
  - **Acceptance Criteria:**
    - Dashboards via Dotnet Aspire and/or Grafana.
    - Metrics exposed:
      - Total users, active users (last 7/30 days).
      - Total tracks, storage consumption.
      - Upload/stream throughput.
      - Error rates per endpoint.
      - Top users by storage/uploads.
    - Real-time updates (1-minute refresh).
    - Historical views: 24h, 7d, 30d, 90d.
  - *Quality: NF-4.1, NF-4.3*

---

## FR 10. Security Requirements

*Implementation Phase: Cross-cutting*

- **FR 10.1 Transport Security** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Encrypt all network communication.
  - **Acceptance Criteria:**
    - HTTPS required for all API endpoints (HTTP redirects to HTTPS).
    - TLS 1.2 minimum; TLS 1.3 preferred.
    - HSTS header with 1-year max-age.
    - Certificate auto-renewal via cert-manager (Kubernetes).
    - Internal service-to-service: mTLS via service mesh or Kubernetes secrets.
  - *Quality: NF-3.2*

- **FR 10.2 Authentication Coverage** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Protect all sensitive operations with authentication.
  - **Acceptance Criteria:**
    - All `/api/v1/*` endpoints require valid JWT (except auth endpoints).
    - Share link access uses signed token with short TTL (15 min).
    - Webhook callbacks validated via HMAC signature.
    - API key option for server-to-server integrations (future).
  - *Quality: NF-3.4, NF-1.5*

- **FR 10.3 Data Isolation** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Prevent cross-user data access.
  - **Acceptance Criteria:**
    - All RavenDB queries filtered by user ID (tenant isolation).
    - MinIO object keys include user ID; bucket policies enforce isolation.
    - API layer validates ownership before any operation.
    - Logging: access attempts to non-owned resources flagged.
  - *Quality: NF-3.2*

- **FR 10.4 Storage Governance** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Secure data at rest with access controls.
  - **Acceptance Criteria:**
    - MinIO buckets: private, SSE-S3 encryption enabled.
    - RavenDB: encryption at rest, authentication required.
    - Credentials: Kubernetes secrets, rotated quarterly (NF-3.1).
    - No secrets in code, logs, or environment variables.
  - *Quality: NF-3.1, NF-3.2*

- **FR 10.5 Token Lifecycle** `[Test]` **P1**
  - **Actor:** System
  - **Description:** Manage token expiry and revocation.
  - **Acceptance Criteria:**
    - Access token TTL: 15 minutes (non-refreshable).
    - Refresh token TTL: 7 days (sliding expiration).
    - Revocation: immediate via NCache blocklist.
    - Logout invalidates all user sessions (optional: single session logout).
    - Token claims: user ID, roles, issued-at, expiry.
  - *Quality: NF-3.4, NF-6.4*

---

## FR 11. Administrative Features

*Implementation Phase: 8*

- **FR 11.1 System Visibility** `[Manual]` **P2**
  - **Actor:** Admin
  - **Description:** View global system statistics and health.
  - **Acceptance Criteria:**
    - Aspire dashboard: service health, dependencies, traces.
    - RavenDB Studio: document inspection, index status.
    - MinIO Console: bucket stats, object count.
    - Read-only access for monitoring; write access restricted.
  - *Quality: NF-4.1*

- **FR 11.2 Observability Access** `[Manual]` **P2**
  - **Actor:** Admin
  - **Description:** Inspect logs, traces, and error reports.
  - **Acceptance Criteria:**
    - Log search: filter by correlation ID, user ID, timestamp, level.
    - Trace viewer: distributed traces across services.
    - Error aggregation: group by type, count, last occurrence.
    - Export: logs and traces exportable for offline analysis.
  - *Quality: NF-4.2, NF-4.4*

- **FR 11.3 User Moderation** `[Test]` **P2**
  - **Actor:** Admin
  - **Description:** Manage user accounts for policy enforcement.
  - **Acceptance Criteria:**
    - View user list with search and filters.
    - Actions: disable account, enable account, delete account.
    - Disable: blocks login, invalidates tokens, preserves data.
    - Delete: triggers FR 1.4 cascade (admin-initiated).
    - Publish `user-moderated` event to Kafka for cache invalidation.
    - Audit log: all moderation actions recorded.
  - **API:** `GET /api/v1/admin/users`, `POST /api/v1/admin/users/{id}/disable`, `POST /api/v1/admin/users/{id}/enable`, `DELETE /api/v1/admin/users/{id}`
  - *Quality: NF-4.2*

- **FR 11.4 Configuration Management** `[Test]` **P2**
  - **Actor:** Admin
  - **Description:** Runtime configuration without redeployment.
  - **Acceptance Criteria:**
    - Configurable settings:
      | Setting | Default | Range |
      |---------|---------|-------|
      | `maxFileSizeMb` | 200 | 10-500 |
      | `allowedFormats` | MP3,WAV,FLAC,AAC,OGG,M4A | Whitelist |
      | `maxTracksPerUser` | 1000 | 100-10000 |
      | `presignedUrlTtlMin` | 10 | 5-60 |
      | `enableWaveformGeneration` | false | Boolean |
    - Settings stored in Kubernetes ConfigMaps.
    - Changes apply without pod restart (configuration reload).
    - Settings exposed at `GET /api/v1/admin/config` (read-only for non-admins).
  - *Quality: NF-5.2*

---

## Traceability Matrix

| FR ID | Related NFRs | Priority | Verification | Implementation Phase |
|-------|--------------|----------|--------------|---------------------|
| FR 1.1 | NF-3.4, NF-3.5 | P1 | Test | Phase 2 |
| FR 1.2 | NF-3.4, NF-1.2 | P1 | Test | Phase 2 |
| FR 1.3 | NF-6.2 | P2 | Test | Phase 2 |
| FR 1.4 | NF-3.3, NF-6.1 | P1 | Test | Phase 2 |
| FR 2.1 | NF-3.5 | P1 | Test | Phase 3 |
| FR 2.2 | NF-3.5, NF-1.6 | P1 | Test | Phase 3 |
| FR 2.3 | NF-1.1, NF-5.3 | P1 | Test | Phase 3 |
| FR 2.4 | NF-6.2 | P1 | Test | Phase 3 |
| FR 2.5 | NF-4.2, NF-7.2 | P2 | Test | Phase 3 |
| FR 2.6 | NF-6.3, NF-9.2 | P1 | Test | Phase 3 |
| FR 3.1 | NF-1.6 | P1 | Test | Phase 3 |
| FR 3.2 | NF-6.2 | P1 | Test | Phase 3 |
| FR 3.3 | NF-1.6, NF-2.2 | P3 | E2E | Phase 3 |
| FR 4.1 | NF-3.2, NF-3.1 | P1 | Test | Phase 4 |
| FR 4.2 | NF-6.4, NF-1.3 | P1 | Test | Phase 4 |
| FR 4.3 | NF-3.2 | P1 | Test | Phase 4 |
| FR 4.4 | NF-6.1 | P2 | Test | Phase 4 |
| FR 5.1 | NF-1.1, NF-1.5 | P1 | E2E | Phase 5 |
| FR 5.2 | NF-6.4, NF-1.2 | P1 | Test | Phase 5 |
| FR 5.3 | NF-7.1, NF-7.4 | P1 | E2E | Phase 5 |
| FR 5.4 | NF-2.4, NF-7.2 | P2 | Test | Phase 5 |
| FR 6.1 | NF-1.4, NF-6.2 | P1 | Test | Phase 6 |
| FR 6.2 | NF-6.3, NF-9.2 | P2 | Test | Phase 6 |
| FR 6.3 | NF-6.1, NF-5.3 | P1 | Test | Phase 6 |
| FR 6.4 | NF-1.4 | P2 | Test | Phase 6 |
| FR 6.5 | NF-1.4 | P2 | Test | Phase 6 |
| FR 7.1 | NF-6.2 | P2 | Test | Phase 7 |
| FR 7.2 | NF-6.2 | P2 | Test | Phase 7 |
| FR 7.3 | NF-7.1 | P2 | E2E | Phase 7 |
| FR 7.4 | NF-7.2, NF-5.2 | P2 | E2E | Phase 7 |
| FR 7.5 | NF-6.1 | P2 | Test | Phase 7 |
| FR 8.1 | NF-3.2 | P3 | Test | Phase 7 |
| FR 8.2 | NF-3.2, NF-4.2 | P3 | Test | Phase 7 |
| FR 8.3 | NF-1.5, NF-3.2 | P2 | Test | Phase 7 |
| FR 9.1 | NF-4.1, NF-6.3 | P2 | Test | Phase 8 |
| FR 9.2 | NF-4.1 | P2 | Test | Phase 8 |
| FR 9.3 | NF-4.1, NF-4.3 | P2 | Manual | Phase 8 |
| FR 10.1 | NF-3.2 | P1 | Test | Cross-cutting |
| FR 10.2 | NF-3.4, NF-1.5 | P1 | Test | Cross-cutting |
| FR 10.3 | NF-3.2 | P1 | Test | Cross-cutting |
| FR 10.4 | NF-3.1, NF-3.2 | P1 | Test | Cross-cutting |
| FR 10.5 | NF-3.4, NF-6.4 | P1 | Test | Cross-cutting |
| FR 11.1 | NF-4.1 | P2 | Manual | Phase 8 |
| FR 11.2 | NF-4.2, NF-4.4 | P2 | Manual | Phase 8 |
| FR 11.3 | NF-4.2 | P2 | Test | Phase 8 |
| FR 11.4 | NF-5.2 | P2 | Test | Phase 8 |

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 2.0 | 2025-11-23 | Comprehensive upgrade: added priorities, verification methods, acceptance criteria, API endpoints, NFR cross-references, traceability matrix, FR 7.5 |
| 1.0 | 2025-11-22 | Initial release |
