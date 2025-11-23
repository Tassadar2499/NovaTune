# NovaTune – Functional Requirements (User-Uploaded Audio Only)

This document defines the authoritative requirements for NovaTune. Implementations use ASP.NET Core services, RavenDB for metadata, MinIO for audio storage, NCache for caching, and Kafka/RabbitMQ for background workflows. Reference requirement IDs (e.g., 4.2) in tickets, commits, tests, and deployment notes.

## 1. User Management
- **1.1 Account creation:** Provide a registration flow (ASP.NET Identity) for new listeners.
- **1.2 Authentication:** Allow logins with valid credentials and maintain JWT-based sessions.
- **1.3 Profile updates:** Users can edit profile attributes such as name and avatar, persisted in RavenDB.
- **1.4 Account removal:** Users may delete their account, triggering data removal/anonymization from RavenDB, caches, and object storage references.

## 2. Audio Upload
- **2.1 Supported formats:** Accept MP3 and any other whitelisted formats enforced by the API.
- **2.2 Validation:** Reject unsupported types and enforce a configurable max file size before uploading to MinIO.
- **2.3 Storage pipeline:** Stream uploads into MinIO buckets per environment and version each object key so rollbacks can restore prior files.
- **2.4 Metadata capture:** Store for every upload in RavenDB:
  - Title
  - Artist
  - Duration
  - File size
  - File type
  - User ID
  - MinIO object key/bucket
  - Upload timestamp
- **2.5 Feedback:** Provide success/failure notifications with correlation IDs for tracing.
- **2.6 Background tasks:** Publish an "audio-uploaded" message to Kafka/RabbitMQ for downstream waveform or analytics jobs.

## 3. Audio Processing
- **3.1 Duration extraction:** Determine track length during ingestion and persist it alongside metadata.
- **3.2 Track IDs:** Generate a unique identifier (GUID) per track, reused by playlist/search features.
- **3.3 Optional waveform:** (Optional) Background workers consume the upload event queue to build waveform previews or similar analysis artifacts and store the results in MinIO/RavenDB.

## 4. Storage & File Management
- **4.1 Secure storage:** Keep audio objects in MinIO with private buckets and per-environment credentials managed via Kubernetes secrets.
- **4.2 Signed URLs:** Never expose raw MinIO endpoints; issue presigned URLs cached briefly in NCache.
- **4.3 Access control:** Ensure one user's track cannot be fetched by others unless a share rule exists; enforce checks both in API and presigned URL generation.
- **4.4 Lifecycle:** Implement lifecycle policies to purge orphaned or deleted objects via background jobs reading Kafka tombstone events.

## 5. Audio Streaming
- **5.1 Playback:** Allow users to stream their uploaded tracks through an ASP.NET Core gateway.
- **5.2 Signed streaming URLs:** Streaming must use time-bound MinIO presigned URLs; cache them in NCache keyed by user and track.
- **5.3 Player controls:** Support play, pause, seek, and volume adjustment.
- **5.4 Expiry handling:** Regenerate URLs automatically when caches expire or the client reports a 403.

## 6. Track Management
- **6.1 Browsing:** Users can list their uploads via RavenDB queries optimized with indexes.
- **6.2 Editing:** Allow metadata edits (title, artist, tags) and publish change events for analytics.
- **6.3 Deletion:** Removing a track deletes the MinIO object, RavenDB metadata, cached entries, and enqueues a Kafka tombstone event.
- **6.4 Search:** Enable search by title, artist, or tags using RavenDB indexing capabilities.
- **6.5 Sorting:** Provide sorting by upload date, title, or duration both server-side and via query parameters.

## 7. Playlists (Optional Tier 1)
- **7.1 Creation:** Users can create playlists stored in RavenDB documents.
- **7.2 Editing:** Add or remove tracks and persist ordering arrays.
- **7.3 Reordering:** Enable drag-and-drop style ordering that updates the stored track sequence.
- **7.4 Continuous playback:** Playlists should support uninterrupted playback driven by cached presigned URLs.

## 8. Sharing (Optional Tier 2)
- **8.1 Share links:** Users may generate shareable links referencing playlist/track IDs.
- **8.2 Visibility controls:** Offer Public, Private, and per-user visibility modes enforced by the API and RavenDB access flags.
- **8.3 Secure streaming:** Even public shares stream via presigned MinIO URLs.

## 9. Analytics (Internal)
- **9.1 Upload metrics:** Emit Kafka events that capture uploads per user, file sizes, and failure counts; aggregate in RavenDB or a warehouse sink.
- **9.2 Playback metrics:** Track play counts per track, optional user play history, and playback duration by consuming player telemetry events.
- **9.3 Dashboards:** Surface totals for storage usage, track counts, active users, and per-endpoint errors through Dotnet Aspire/Prometheus dashboards for admins.

## 10. Security Requirements
- **10.1 Transport security:** Use HTTPS for every API and gRPC surface, including Kubernetes ingress.
- **10.2 Auth coverage:** Require authentication for every track-related operation; share links get signed tokens with short TTLs.
- **10.3 Isolation:** Prevent users from accessing or modifying others' data through RavenDB security filters and MinIO bucket policies.
- **10.4 Storage governance:** Enforce private MinIO buckets, rotate credentials via Kubernetes secrets, and ensure RavenDB encryption at rest.
- **10.5 Token lifecycle:** Tokens must expire and support refresh flows; cache revocation state in NCache for near-real-time logout.

## 11. Administrative Features
- **11.1 Visibility:** Admins can view global statistics in the Aspire dashboard and RavenDB admin UI.
- **11.2 Observability:** Admins can inspect logs, traces, and error reports flowing through Dotnet Aspire.
- **11.3 Moderation:** Admins can disable or delete abusive accounts, propagating the action through Kafka to revoke cached URLs.
- **11.4 Configuration:** Admins can define maximum file size, allowed formats, storage limits, and toggle feature flags deployed via Kubernetes ConfigMaps.
