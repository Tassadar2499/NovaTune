Phase 1: Solution Restructuring & Domain Foundation

- Reorganize solution into layered architecture:
    - NovaTune.Api - ASP.NET Core endpoints
    - NovaTune.Application - Use cases, abstractions
    - NovaTune.Domain - Entities (User, Track, Playlist), value objects
    - NovaTune.Infrastructure - External service adapters
- Define core domain entities: User, Track, AudioMetadata
- Set up dependency injection boundaries

 ---
Phase 2: User Management (FR 1.x)

- Implement ASP.NET Identity with RavenDB store
- JWT authentication with refresh token flow
- User profile CRUD operations
- Account deletion with cascade triggers

 ---
Phase 3: Audio Upload Pipeline (FR 2.x, 3.x)

- MinIO integration adapter with presigned URL generation
- File validation (format whitelist, size limits)
- Upload streaming endpoint with progress feedback
- Metadata extraction (duration, file info)
- RavenDB track document storage
- Kafka "audio-uploaded" event publishing

 ---
Phase 4: Storage & Access Control (FR 4.x)

- NCache integration for presigned URL caching
- Access control enforcement (user ownership checks)
- Background job for orphan cleanup via Kafka tombstones

 ---
Phase 5: Audio Streaming (FR 5.x)

- Streaming gateway endpoint with signed URLs
- Automatic URL regeneration on expiry/403
- Player control support (seek, pause via range requests)

 ---
Phase 6: Track Management (FR 6.x)

- Track browsing with RavenDB indexes
- Metadata editing with change events
- Track deletion cascade (MinIO, RavenDB, cache, Kafka)
- Search and sorting APIs

 ---
Phase 7: Optional Features

- Tier 1: Playlists (FR 7.x) - creation, editing, reordering, continuous playback
- Tier 2: Sharing (FR 8.x) - share links, visibility controls

 ---
Phase 8: Observability & Admin (FR 9.x, 11.x)

- Analytics event consumers
- Admin dashboard endpoints
- Structured logging with correlation IDs (NF-4.2)
- Alerting integration

 ---
Cross-Cutting (Throughout)

- OpenTelemetry metrics/traces (already scaffolded)
- Polly resilience patterns (already scaffolded)
- xUnit tests with ≥80% coverage target
- Docker Compose for local infrastructure
- CI/CD pipeline (GitHub Actions)
