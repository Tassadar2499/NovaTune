# Product Context

## Problem Statement
Musicians and audio creators need a self-hosted platform to upload, organize, and stream their audio content with modern infrastructure patterns (event-driven, resilient, observable).

## User Experience Goals
- **Seamless Upload**: Direct browser-to-storage uploads without proxying through the API
- **Instant Playback**: Cached presigned URLs for low-latency streaming
- **Organized Library**: Filterable, searchable track library with cursor pagination
- **Playlist Management**: Create and organize playlists with drag-and-drop reordering
- **Admin Oversight**: Content moderation, user management, and analytics dashboards

## Key Workflows
1. **Upload Flow**: Initiate upload session -> Get presigned URL -> Upload to MinIO -> MinIO event -> UploadIngestor creates track -> AudioProcessor extracts metadata
2. **Streaming Flow**: Request stream URL -> Check cache -> Generate presigned GET URL -> Encrypt and cache -> Return URL
3. **Track Lifecycle**: Create -> Ready -> (optional) Soft Delete -> 30-day grace -> Physical deletion by Lifecycle worker
4. **Moderation Flow**: Admin reviews track -> Sets moderation status (UnderReview/Disabled/Removed) -> Audit log entry created

## Design Decisions
- **Event-driven architecture**: Workers process uploads, deletions, and telemetry asynchronously via Kafka topics
- **Outbox pattern**: Reliable event publishing through RavenDB transactional outbox
- **Resilience-first**: Polly circuit breakers, retries, and timeouts on all external calls
- **Decorator pattern**: ResilientTrackManagementService wraps TrackManagementService for resilience
- **Tamper-evident audit**: SHA-256 hash chain for audit log integrity verification
