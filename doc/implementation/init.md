# NovaTune – Implementation Plan

> **Version:** 2.2
> **Last Updated:** 2025-12-06
> **Status:** Active

This document defines the phased implementation roadmap for NovaTune. Each phase builds upon prior deliverables and maps directly to functional requirements ([functional.md](../requirements/functional.md)) and non-functional requirements ([non_functional.md](../requirements/non_functional.md)).

Reference phase numbers and requirement IDs in tickets, commits, and PRs (e.g., "Phase 2: FR 1.1 - Account Creation").

---

## Legend

| Symbol | Meaning |
|--------|---------|
| **P1** | Must-have – blocks phase completion |
| **P2** | Should-have – degrades phase quality |
| **P3** | Nice-to-have – enhances phase deliverables |
| ✅ | Phase complete |
| 🔄 | Phase in progress |
| ⏳ | Phase pending |
| 🔒 | Blocked by dependency |

---

## Phase Overview

| Phase | Name | FR Coverage | NFR Coverage | Key Deliverables | Status |
|-------|------|-------------|--------------|------------------|--------|
| 1 | Infrastructure & Domain Foundation | — | NF-3.1, NF-3.6, NF-8.1, NF-9.1 | Aspire setup, Docker infra, base entities, security headers | ⏳ |
| 2 | User Management | FR 1.x | NF-3.2–3.4, NF-6.2 | Auth system, JWT flow, profile APIs | ⏳ |
| 3 | Audio Upload Pipeline | FR 2.x, FR 3.x | NF-1.1–1.2, NF-1.6, NF-3.5 | Upload API, MinIO integration, Kafka events, checksum validation | ⏳ |
| 4 | Storage & Access Control | FR 4.x | NF-1.3, NF-3.2, NF-6.1, NF-6.4 | Presigned URLs, NCache, lifecycle jobs, stampede prevention | ⏳ |
| 5 | Audio Streaming | FR 5.x | NF-1.1, NF-1.5, NF-7.x | Streaming gateway, range requests, YARP | ⏳ |
| 6 | Track Management | FR 6.x | NF-1.4, NF-6.2–6.3 | CRUD APIs, search, RavenDB indexes | ⏳ |
| 7 | Optional Features | FR 7.x, FR 8.x | NF-6.2, NF-7.1–7.2 | Playlists, sharing | ⏳ |
| 8 | Observability & Admin | FR 9.x, FR 11.x | NF-4.x, NF-5.x | Analytics, admin dashboard, alerting | ⏳ |

---

## Dependency Matrix

```
Phase 1 ─────────────────────────────────────────────────────────────┐
    │                                                                │
    ▼                                                                │
Phase 2 (User Management)                                            │
    │                                                                │
    ├──────────────────┬─────────────────────┐                       │
    ▼                  ▼                     ▼                       │
Phase 3            Phase 4              Phase 5                      │
(Upload)          (Storage)            (Streaming)                   │
    │                  │                     │                       │
    └──────────────────┴─────────────────────┘                       │
                       │                                             │
                       ▼                                             │
                   Phase 6 (Track Management)                        │
                       │                                             │
                       ├─────────────────────────────────────────────┘
                       ▼
                   Phase 7 (Optional Features)
                       │
                       ▼
                   Phase 8 (Observability & Admin)
```

---

## Phase 1: Infrastructure & Domain Foundation

### Objective

Configure the existing Aspire project structure with infrastructure dependencies and define core domain entities. Keep it simple—avoid premature abstraction.

### FR Coverage

*None directly – foundational infrastructure phase.*

### NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-3.1 | Secrets Management | Configure `dotnet user-secrets` for local dev |
| NF-3.6 | HTTP Security Headers | Configure HSTS, CSP, X-Frame-Options, X-Content-Type-Options |
| NF-8.1 | Solution Hygiene | Organize folders within existing Aspire projects |
| NF-8.4 | API Documentation | Set up Scalar OpenAPI infrastructure |
| NF-9.1 | API Versioning | Establish `/api/v1/` convention |
| NF-9.3 | Service Discovery | Configure Dotnet Aspire orchestration |

### Deliverables

1. **Use existing Aspire project structure** (`src/NovaTuneApp/`):
   - `NovaTuneApp.ApiService` – API endpoints, entities, services (all-in-one)
   - `NovaTuneApp.Web` – Blazor frontend (existing)
   - `NovaTuneApp.AppHost` – Aspire orchestration
   - `NovaTuneApp.ServiceDefaults` – Shared configuration (OpenTelemetry, health checks)
   - `NovaTuneApp.Tests` – All tests (unit + integration)

2. **Folder structure within ApiService**:
   ```
   NovaTuneApp.ApiService/
   ├── Models/           # Entities (User, Track, AudioMetadata)
   ├── Services/         # Business logic (AuthService, TrackService, etc.)
   ├── Endpoints/        # Minimal API route definitions
   └── Infrastructure/   # External adapters (MinIO, RavenDB, Kafka, NCache)
   ```

3. **Core domain entities** (in `Models/`):
   ```csharp
   User { Id, Email, DisplayName, PasswordHash, CreatedAt, UpdatedAt }
   Track { Id, UserId, Title, Artist, Duration, ObjectKey, Metadata, Status }
   AudioMetadata { Format, Bitrate, SampleRate, Channels, FileSize }
   ```

4. **Infrastructure configuration**:
   - Docker Compose for local dependencies
   - `.env.example` with all required variables
   - `appsettings.Development.json` and `appsettings.Test.json`
   - Aspire AppHost resource definitions for all services

5. **API foundation**:
   - Health check endpoints (`/health`, `/ready`)
   - Scalar OpenAPI documentation at `/docs`
   - API versioning via `/api/v1/` prefix
   - CORS configuration
   - HTTP security headers middleware (HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy)

### Infrastructure Setup

- [ ] Docker Compose: RavenDB, MinIO, NCache, Kafka, RabbitMQ
- [ ] Aspire AppHost resource definitions
- [ ] Health check endpoints integrated with Aspire
- [ ] OpenTelemetry exporters (metrics, traces) via ServiceDefaults
- [ ] Serilog structured logging with correlation IDs
- [ ] `.env.example` documenting all required variables
- [ ] Minimum resource requirements documented (≥8GB RAM for full infra stack)
- [ ] FFmpeg/FFprobe base Docker image configuration

### Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Entity validation | Core validation rules |
| Integration | Aspire orchestration | All services start and communicate |
| Integration | Health endpoints | Return 200 when healthy |

### Exit Criteria

- [ ] `dotnet build` succeeds with warnings-as-errors
- [ ] `dotnet test` passes all tests
- [ ] `dotnet format --verify-no-changes` passes
- [ ] Aspire dashboard shows all services healthy
- [ ] API returns 200 on `/health` endpoint
- [ ] Scalar UI accessible at `/docs`
- [ ] Docker Compose starts all infrastructure services

### Dependencies

*None – this is the foundational phase.*

### Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Aspire version incompatibility | High | Pin Aspire 13.0 in `global.json` |
| Docker resource constraints | Medium | Document minimum resource requirements (8GB RAM) |
| Infrastructure service startup order | Medium | Use Aspire health checks and wait strategies |

### Future Considerations

If codebase complexity grows significantly (Phase 6+), consider extracting to layered architecture:
- `NovaTune.Domain` – Pure domain entities
- `NovaTune.Application` – Use cases and abstractions
- `NovaTune.Infrastructure` – External adapters

For now, keeping everything in `NovaTuneApp.ApiService` with clear folder boundaries is sufficient and avoids premature abstraction.

---

## Phase 2: User Management (FR 1.x)

### Objective

Implement complete user lifecycle management with secure authentication using ASP.NET Identity backed by RavenDB.

### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 1.1 | Account Creation | P1 | Test |
| FR 1.2 | Authentication | P1 | Test |
| FR 1.3 | Profile Updates | P2 | Test |
| FR 1.4 | Account Removal | P1 | Test |

### NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-3.2 | Data Protection | RavenDB encryption at rest |
| NF-3.3 | Privacy & Data Subject Rights | Cascade deletion implementation |
| NF-3.4 | Authentication Performance | JWT <200ms p95 |
| NF-6.2 | RavenDB Integrity | User document schema, indexes |

### Deliverables

1. **ASP.NET Identity with RavenDB**:
   - Custom `IUserStore<ApplicationUser>` implementation
   - Custom `IRoleStore<ApplicationRole>` implementation
   - Password hashing with Identity defaults (PBKDF2)

2. **JWT Authentication**:
   - Access token: 15-minute TTL
   - Refresh token: 7-day sliding expiration
   - Token revocation via NCache blocklist
   - Claims: `userId`, `email`, `roles`, `iat`, `exp`
   - **Security requirements:**
     - Asymmetric signing (RS256 or ES256); HS256 prohibited
     - Include `kid` (key ID) in JWT header for key rotation
     - Validate `iss` (issuer) and `aud` (audience) on every request
     - Clock skew tolerance: ±2 minutes for `exp`/`nbf` validation
     - JWKS endpoint at `/.well-known/jwks.json` for public key distribution

3. **API Endpoints**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/auth/register` | POST | Account creation |
   | `/api/v1/auth/login` | POST | Authentication |
   | `/api/v1/auth/refresh` | POST | Token refresh |
   | `/api/v1/auth/logout` | POST | Session termination |
   | `/api/v1/users/me` | GET | Profile retrieval |
   | `/api/v1/users/me` | PATCH | Profile update |
   | `/api/v1/users/me/avatar` | POST | Avatar upload |
   | `/api/v1/users/me` | DELETE | Account deletion |

4. **Security features**:
   - Account lockout: 15 minutes after 5 failed attempts
   - Email verification flow
   - Password requirements enforcement
   - Concurrent session limit (5 per user)

5. **Cascade deletion**:
   - Soft-delete with 30-day recovery window
   - Kafka tombstone event publication
   - NCache entry invalidation
   - MinIO deletion scheduling (Phase 4 integration)

### Infrastructure Setup

- [ ] RavenDB database and collections: `Users`, `RefreshTokens`
- [ ] RavenDB indexes: `Users_ByEmail`, `Users_ByStatus`
- [ ] NCache region: `auth-tokens`
- [ ] Email service integration (SMTP or SendGrid)

### Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Identity stores | ≥80% |
| Unit | JWT service | 100% |
| Integration | Auth endpoints | All happy/error paths |
| Integration | Token refresh flow | Expiry, revocation |
| Integration | Account deletion cascade | Verify all cleanup steps |

### Exit Criteria

- [ ] User can register, login, refresh, and logout
- [ ] JWT tokens validate correctly with proper TTLs
- [ ] Account lockout triggers after 5 failed logins
- [ ] Profile CRUD operations work with optimistic concurrency
- [ ] Account deletion initiates cascade (stubs for Phase 4+ items)
- [ ] All auth endpoints return <200ms p95
- [ ] ≥80% test coverage for auth middleware

### Dependencies

| Dependency | Source | Required For |
|------------|--------|--------------|
| Layered architecture | Phase 1 | Project structure |
| RavenDB connection | Phase 1 | User storage |
| NCache connection | Phase 1 | Token caching |

### Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| RavenDB Identity store complexity | High | Reference existing implementations, extensive tests |
| Token revocation latency | Medium | NCache with sub-5s propagation |
| Email delivery delays | Low | Async processing, retry with RabbitMQ |

---

## Phase 3: Audio Upload Pipeline (FR 2.x, FR 3.x)

### Objective

Build a robust audio upload system with streaming to MinIO, format validation via FFprobe, and event publishing to Kafka.

### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 2.1 | Supported Formats | P1 | Test |
| FR 2.2 | Validation | P1 | Test |
| FR 2.3 | Storage Pipeline | P1 | Test |
| FR 2.4 | Metadata Capture | P1 | Test |
| FR 2.5 | Feedback | P2 | Test |
| FR 2.6 | Background Tasks | P1 | Test |
| FR 3.1 | Duration Extraction | P1 | Test |
| FR 3.2 | Track IDs | P1 | Test |
| FR 3.3 | Optional Waveform Generation | P3 | E2E |

### NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-1.1 | Throughput | 50 concurrent uploads per node |
| NF-1.2 | Latency | <3s p50 for files ≤50 MB |
| NF-1.6 | Audio Processing Limits | 30s FFprobe timeout |
| NF-2.2 | Resilience | Polly retry policies |
| NF-3.5 | Input Validation | MIME type, size, format checks |
| NF-6.3 | Event Stream Governance | `audio-uploaded` schema |
| NF-9.2 | Event Schema Evolution | Versioned JSON payloads |

### Deliverables

1. **MinIO Integration Adapter**:
   - `IStorageService` interface in `Services/`
   - `MinioStorageService` implementation in `Infrastructure/`
   - Multipart upload for files >5 MB
   - Object key format: `{env}/{userId}/{trackId}/{version}/{filename}`
   - Bucket configuration: `novatune-{env}-audio`
   - **Checksum validation:**
     - Compute SHA-256 checksum on upload completion
     - Validate `Content-MD5` header if provided by client; return 400 on mismatch
     - Store checksum alongside `ObjectKey` in RavenDB track document
   - **Versioning lifecycle:**
     - MinIO lifecycle rules: retain last 3 versions, expire older after 7 days
     - Lifecycle rules preserve versions during 30-day soft-delete recovery window

2. **Upload API**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/tracks/upload` | POST | Streaming upload |
   | `/api/v1/tracks/upload/progress/{correlationId}` | GET (SSE) | Progress events |

3. **Validation Pipeline**:
   - MIME type whitelist: MP3, WAV, FLAC, AAC, OGG, M4A
   - File size limit: 200 MB (configurable)
   - Duration limit: 60 minutes
   - File name sanitization (path traversal prevention)
   - FFprobe format verification

4. **Metadata Extraction**:
   - Duration (milliseconds precision)
   - Bitrate, sample rate, channels
   - ID3 tags (title, artist) if present
   - File size verification
   - SHA-256 checksum (stored in track document)

5. **Kafka Event Publishing**:
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

6. **RavenDB Track Documents**:
   - Collection: `Tracks`
   - Indexes: `Tracks_ByUserId`, `Tracks_ByUploadDate`

7. **Optional: Waveform Generation** (P3):
   - RabbitMQ `waveform-jobs` queue
   - Background worker with FFmpeg
   - 1000-sample waveform array
   - Storage in track document or MinIO

### Infrastructure Setup

- [ ] MinIO buckets: `novatune-dev-audio`, `novatune-test-audio`
- [ ] MinIO bucket policies (private, SSE-S3)
- [ ] Kafka topic: `audio-events` (30-day retention)
- [ ] RabbitMQ queue: `waveform-jobs` (optional)
- [ ] FFmpeg/FFprobe in Docker image

### Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Validation logic | 100% |
| Unit | Metadata extraction | All supported formats |
| Unit | Checksum computation | SHA-256, MD5 validation |
| Property-based | Filename sanitization | FsCheck edge cases |
| Integration | MinIO upload | Success, failure, multipart |
| Integration | Kafka publishing | Event schema validation |
| Integration | End-to-end upload | Full pipeline |
| Integration | Checksum mismatch | 400 response verification |
| Load | Concurrent uploads | 50 simultaneous |

### Exit Criteria

- [ ] Upload accepts all whitelisted formats
- [ ] Rejects invalid formats with 415 status
- [ ] Rejects oversized files with 413 status
- [ ] Progress events stream via SSE
- [ ] Metadata captured accurately in RavenDB
- [ ] SHA-256 checksum computed and stored for each upload
- [ ] Content-MD5 mismatch returns 400 Bad Request
- [ ] Kafka event published within 5 seconds
- [ ] Upload latency <3s p50 for ≤50 MB files
- [ ] ≥80% test coverage for upload pipeline

### Dependencies

| Dependency | Source | Required For |
|------------|--------|--------------|
| User authentication | Phase 2 | User context, JWT validation |
| Domain entities | Phase 1 | Track model |
| MinIO setup | Phase 1 | Object storage |
| Kafka setup | Phase 1 | Event streaming |

### Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| FFprobe timeout on large files | Medium | 30s limit, background processing |
| MinIO connection failures | High | Polly retry with circuit breaker |
| Kafka unavailability | High | RabbitMQ dead-letter fallback |
| Memory pressure from large uploads | High | Streaming (no buffering) |

---

## Phase 4: Storage & Access Control (FR 4.x)

### Objective

Implement secure access to stored audio with presigned URL generation, caching via NCache, and lifecycle management for orphaned objects.

### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 4.1 | Secure Storage | P1 | Test |
| FR 4.2 | Signed URLs | P1 | Test |
| FR 4.3 | Access Control | P1 | Test |
| FR 4.4 | Lifecycle Management | P2 | Test |

### NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-1.3 | Statelessness | Presigned URLs, no session state |
| NF-2.4 | Graceful Degradation | Cache bypass fallback |
| NF-3.2 | Data Protection | Bucket policies, SSE |
| NF-6.1 | Lifecycle Rules | Orphan cleanup, tombstone processing |
| NF-6.4 | Cache Management | TTL policies, eviction |

### Deliverables

1. **Presigned URL Service**:
   - `IPresignedUrlService` interface
   - TTL: 10 minutes (configurable)
   - URL signature verification
   - User-track scoped URLs

2. **NCache Integration**:
   - Cache key: `presigned:{userId}:{trackId}`
   - Cache TTL: 8 minutes (80% of presigned TTL)
   - LRU eviction when >80% capacity
   - Cache hit target: >90%
   - **Cache stampede prevention:**
     - Single-flight pattern via `SemaphoreSlim` for concurrent cache misses
     - Jittered TTL: base TTL * (0.9 + random * 0.2) to prevent synchronized expiration
     - Request coalescing: concurrent requests for same key share single generation
   - **Time abstraction:**
     - Use `TimeProvider` (.NET 8+) or `IClock` interface for TTL calculations
     - Enables deterministic testing with fixed clocks

3. **Access Control Enforcement**:
   - Ownership verification middleware
   - Share rule checking (prepares for Phase 7)
   - Access denial logging
   - Response codes: 403 (forbidden), 404 (not found)

4. **Lifecycle Background Jobs**:
   - Kafka consumer: `track-deletions` topic
   - 24-hour grace period before MinIO deletion
   - Orphan detection: objects with no RavenDB reference after 7 days
   - Cleanup metrics published to Aspire

5. **Bucket Security**:
   - Private bucket ACLs
   - SSE-S3 with AES-256
   - Cross-user access denied via bucket policies

### Infrastructure Setup

- [ ] NCache cluster configuration
- [ ] NCache regions: `presigned-urls`, `access-tokens`
- [ ] Kafka topic: `track-deletions` (compacted)
- [ ] Background worker service for lifecycle jobs

### Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Presigned URL generation | TTL, signatures |
| Unit | Access control logic | Owner, shared, denied |
| Unit | Jittered TTL calculation | Distribution validation |
| Unit | TimeProvider/IClock usage | Fixed clock tests |
| Integration | NCache operations | Set, get, eviction |
| Integration | Single-flight pattern | Concurrent request coalescing |
| Integration | Lifecycle job | Tombstone processing |
| Integration | Orphan cleanup | Detection and deletion |

### Exit Criteria

- [ ] Presigned URLs generated with correct TTL
- [ ] NCache hit rate >90% under normal load
- [ ] Single-flight pattern prevents cache stampede
- [ ] Jittered TTL applied to all cached entries
- [ ] TimeProvider used for all TTL calculations (testable)
- [ ] Fallback to direct generation when cache unavailable
- [ ] Unauthorized access returns 403
- [ ] Lifecycle job processes tombstones correctly
- [ ] Orphan objects cleaned after 7 days
- [ ] No cross-user object access possible

### Dependencies

| Dependency | Source | Required For |
|------------|--------|--------------|
| MinIO objects | Phase 3 | Objects to access |
| Track documents | Phase 3 | Ownership data |
| Kafka infrastructure | Phase 1 | Tombstone events |
| NCache infrastructure | Phase 1 | URL caching |

### Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Cache stampede | Medium | Request coalescing |
| NCache cluster failure | High | Graceful degradation to direct MinIO |
| Orphan false positives | High | 7-day grace period, audit logging |

---

## Phase 5: Audio Streaming (FR 5.x)

### Objective

Deliver seamless audio playback through a secure streaming gateway with range request support and automatic URL refresh handling.

### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 5.1 | Playback Gateway | P1 | E2E |
| FR 5.2 | Signed Streaming URLs | P1 | Test |
| FR 5.3 | Player Controls | P1 | E2E |
| FR 5.4 | Expiry Handling | P2 | Test |

### NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-1.1 | Throughput | 200 concurrent streams per node |
| NF-1.5 | API Gateway Performance | <10ms YARP overhead |
| NF-7.1 | Responsiveness | Keyboard accessible controls |
| NF-7.2 | Feedback | Loading indicators, error messages |
| NF-7.4 | Frontend Performance | <2.5s LCP |

### Deliverables

1. **Streaming Gateway Endpoint**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/tracks/{id}/stream` | GET | Audio stream |

2. **YARP Reverse Proxy Configuration**:
   - Route to presigned MinIO URLs
   - Transparent proxying (client doesn't see MinIO)
   - Rate limiting: 100 req/min per user
   - CORS configuration for frontend

3. **HTTP Range Request Support**:
   - `Accept-Ranges: bytes` header
   - Partial content (HTTP 206) responses
   - Seek support via byte ranges
   - Proper `Content-Length` headers

4. **Expiry Handling**:
   - Detect 403 from MinIO (expired URL)
   - Automatic cache invalidation
   - Fresh URL generation
   - Client retry mechanism (max 3 attempts)

5. **Player Control Support**:
   - Play/pause state management
   - Seek to position
   - Volume control (client-side)
   - Progress tracking

### Infrastructure Setup

- [ ] YARP configuration in Aspire
- [ ] CORS policy for frontend domains
- [ ] Rate limiting middleware

### Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | URL expiry detection | 403 handling |
| Integration | Range requests | Seek, partial content |
| Integration | YARP routing | Transparent proxy |
| E2E | Full playback | Start to finish |
| E2E | Seek operations | Multiple positions |
| Load | Concurrent streams | 200 simultaneous |

### Exit Criteria

- [ ] Audio streams successfully via gateway
- [ ] Range requests enable seeking
- [ ] Expired URLs automatically refreshed
- [ ] CORS allows frontend playback
- [ ] YARP overhead <10ms p95
- [ ] 200 concurrent streams sustained

### Dependencies

| Dependency | Source | Required For |
|------------|--------|--------------|
| Presigned URL service | Phase 4 | Streaming URLs |
| NCache integration | Phase 4 | URL caching |
| Track documents | Phase 3 | Track metadata |

### Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| High bandwidth costs | Medium | CDN integration (future) |
| YARP configuration complexity | Medium | Extensive integration tests |
| Client-side seek bugs | Low | Comprehensive E2E tests |

---

## Phase 6: Track Management (FR 6.x)

### Objective

Provide comprehensive track browsing, editing, search, and deletion capabilities with optimized RavenDB queries.

### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 6.1 | Track Browsing | P1 | Test |
| FR 6.2 | Metadata Editing | P2 | Test |
| FR 6.3 | Track Deletion | P1 | Test |
| FR 6.4 | Search | P2 | Test |
| FR 6.5 | Sorting | P2 | Test |

### NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-1.4 | Metadata Query Latency | <300ms p95 |
| NF-6.2 | RavenDB Integrity | Static indexes, migrations |
| NF-6.3 | Event Stream Governance | `track-metadata-updated` events |

### Deliverables

1. **Track Browsing API**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/tracks` | GET | List with pagination |
   | `/api/v1/tracks/{id}` | GET | Single track |
   | `/api/v1/tracks/search` | GET | Full-text search |

2. **Pagination & Filtering**:
   - Cursor-based pagination (not offset)
   - Default: 20 items per page (configurable 10-100)
   - Filters: title, artist, tags, date range
   - Sort fields: uploadedAt, title, artist, duration, playCount

3. **RavenDB Static Indexes**:
   - `Tracks_ByUserId_SortByUploadDate`
   - `Tracks_ByUserId_Search` (full-text)
   - `Tracks_ByUserId_Stats` (aggregations)

4. **Metadata Editing**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/tracks/{id}` | PATCH | Update metadata |

   - Editable: title, artist, tags, description
   - Optimistic concurrency via `@etag`
   - Edit history (last 10 versions)
   - Kafka event: `track-metadata-updated`

5. **Track Deletion**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/tracks/{id}` | DELETE | Soft delete |
   | `/api/v1/tracks/{id}/restore` | POST | Restore |

   - Soft-delete with 30-day retention
   - Cascade: playlist removal, share invalidation
   - Kafka tombstone to `track-deletions`
   - NCache invalidation

6. **Search Implementation**:
   - RavenDB full-text index with stemming
   - Relevance ranking
   - Quoted phrase support
   - Tag autocomplete

### Infrastructure Setup

- [ ] RavenDB indexes deployed
- [ ] Kafka topic: `track-events`
- [ ] Index performance baseline established

### Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Pagination logic | Cursor encoding/decoding |
| Unit | Search query building | All filter combinations |
| Integration | Browse endpoints | All sort/filter options |
| Integration | Edit concurrency | Conflict resolution |
| Integration | Deletion cascade | All cleanup steps |
| Performance | Query latency | <300ms p95 with 10K tracks |

### Exit Criteria

- [ ] Browse returns paginated results correctly
- [ ] All sort and filter options work
- [ ] Search returns relevant results
- [ ] Metadata edits persist with versioning
- [ ] Deletion cascades to all related data
- [ ] Query latency <300ms p95 for 10K tracks/user
- [ ] Kafka events published for all changes

### Dependencies

| Dependency | Source | Required For |
|------------|--------|--------------|
| Track documents | Phase 3 | Data to browse |
| NCache integration | Phase 4 | Cache invalidation |
| Kafka infrastructure | Phase 1 | Event publishing |

### Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Index rebuild time | Medium | Test migrations in staging |
| Search relevance tuning | Low | Iterative improvement |
| Concurrent edit conflicts | Medium | Clear error messaging |

---

## Phase 7: Optional Features (FR 7.x, FR 8.x)

### Objective

Extend platform capabilities with playlist management (Tier 1) and content sharing (Tier 2).

### Tier 1: Playlists (FR 7.x)

#### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 7.1 | Playlist Creation | P2 | Test |
| FR 7.2 | Playlist Editing | P2 | Test |
| FR 7.3 | Playlist Reordering | P2 | E2E |
| FR 7.4 | Continuous Playback | P2 | E2E |
| FR 7.5 | Playlist Deletion | P2 | Test |

#### Deliverables

1. **Playlist API**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/playlists` | POST | Create |
   | `/api/v1/playlists` | GET | List |
   | `/api/v1/playlists/{id}` | GET | Get with tracks |
   | `/api/v1/playlists/{id}` | PATCH | Update name/description |
   | `/api/v1/playlists/{id}` | DELETE | Delete |
   | `/api/v1/playlists/{id}/tracks` | POST | Add tracks |
   | `/api/v1/playlists/{id}/tracks/{trackId}` | DELETE | Remove track |
   | `/api/v1/playlists/{id}/order` | PUT | Reorder tracks |

2. **Playlist Features**:
   - Name: 1-100 chars, unique per user
   - Max playlists per user: 100
   - Max tracks per playlist: 500
   - Duplicate tracks allowed
   - Batch add/remove operations

3. **Continuous Playback**:
   - Auto-advance to next track
   - Prefetch next track URL
   - Loop and shuffle modes
   - Queue display

### Tier 2: Sharing (FR 8.x)

#### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 8.1 | Share Links | P3 | Test |
| FR 8.2 | Visibility Controls | P3 | Test |
| FR 8.3 | Secure Streaming | P2 | Test |

#### Deliverables

1. **Share API**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/shares` | POST | Create share link |
   | `/api/v1/shares/{id}` | PATCH | Update visibility |
   | `/api/v1/shares/{id}` | DELETE | Revoke |
   | `/share/{shareId}` | GET | Public access endpoint |

2. **Share Features**:
   - 12-character alphanumeric share ID
   - Visibility: private, link, public (future)
   - Expiry: 30 days default (configurable)
   - Max active shares per user: 100
   - Rate limiting: 50 streams/hour per link

### NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-6.2 | RavenDB Integrity | Playlist/share schemas |
| NF-7.1 | Responsiveness | Drag-drop reordering |
| NF-7.2 | Feedback | Playback transitions |

### Exit Criteria

**Tier 1:**
- [ ] Playlist CRUD operations work
- [ ] Track add/remove/reorder functional
- [ ] Continuous playback with prefetch
- [ ] Loop and shuffle modes work

**Tier 2:**
- [ ] Share links generated correctly
- [ ] Visibility controls enforced
- [ ] Anonymous streaming via share link works
- [ ] Rate limiting prevents abuse

### Dependencies

| Dependency | Source | Required For |
|------------|--------|--------------|
| Track management | Phase 6 | Track references |
| Streaming | Phase 5 | Shared playback |
| Access control | Phase 4 | Share rule checking |

---

## Phase 8: Observability & Admin (FR 9.x, FR 11.x)

### Objective

Complete the platform with analytics dashboards, administrative controls, and production-grade observability.

### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 9.1 | Upload Metrics | P2 | Test |
| FR 9.2 | Playback Metrics | P2 | Test |
| FR 9.3 | Admin Dashboards | P2 | Manual |
| FR 11.1 | System Visibility | P2 | Manual |
| FR 11.2 | Observability Access | P2 | Manual |
| FR 11.3 | User Moderation | P2 | Test |
| FR 11.4 | Configuration Management | P2 | Test |

### NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-4.1 | Metrics | OTEL exporters, dashboards |
| NF-4.2 | Logging | Correlation ID search |
| NF-4.3 | Alerting | PagerDuty/Slack integration |
| NF-4.4 | Distributed Tracing | W3C trace context |
| NF-5.1 | CI/CD Pipeline | Complete pipeline |
| NF-5.2 | Environments | Dev/staging/prod parity |

### Deliverables

1. **Analytics Event Consumers**:
   - Kafka consumer for `audio-events`
   - Aggregations: uploads/day, bytes/day, avg duration
   - Kafka consumer for playback events
   - Metrics: play count, total play time, unique listeners

2. **Admin API**:
   | Endpoint | Method | Description |
   |----------|--------|-------------|
   | `/api/v1/admin/users` | GET | User list with search |
   | `/api/v1/admin/users/{id}/disable` | POST | Disable account |
   | `/api/v1/admin/users/{id}/enable` | POST | Enable account |
   | `/api/v1/admin/users/{id}` | DELETE | Delete account |
   | `/api/v1/admin/config` | GET | Runtime configuration |
   | `/api/v1/admin/config` | PATCH | Update configuration |

3. **Admin Dashboard**:
   - Total users, active users (7/30 days)
   - Total tracks, storage consumption
   - Upload/stream throughput
   - Error rates per endpoint
   - Top users by storage

4. **Configuration Management**:
   | Setting | Default | Range |
   |---------|---------|-------|
   | `maxFileSizeMb` | 200 | 10-500 |
   | `allowedFormats` | MP3,WAV,FLAC,AAC,OGG,M4A | Whitelist |
   | `maxTracksPerUser` | 1000 | 100-10000 |
   | `presignedUrlTtlMin` | 10 | 5-60 |
   | `enableWaveformGeneration` | false | Boolean |

5. **Alerting Integration**:
   | Severity | Condition | Response |
   |----------|-----------|----------|
   | P1-Critical | Error rate >5% 5min | 15 min |
   | P2-High | Latency 2x baseline | 1 hour |
   | P3-Medium | Storage >80% | 4 hours |
   | P4-Low | Cache hit <80% | Next day |

6. **Structured Logging Enhancement**:
   - Correlation ID propagation verified
   - Requirement ID in logs (`"req": "FR-2.3"`)
   - User ID (hashed) in non-debug
   - Log search by correlation ID

### Infrastructure Setup

- [ ] Grafana dashboards (or Aspire UI)
- [ ] Alert rules in monitoring system
- [ ] Kubernetes ConfigMaps for runtime config
- [ ] Admin role in Identity system

### Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Analytics aggregations | All metric types |
| Integration | Admin endpoints | All operations |
| Integration | Config reload | Hot reload verification |
| E2E | Dashboard data | Accurate metrics display |

### Exit Criteria

- [ ] Analytics consume and aggregate events
- [ ] Admin can view/manage users
- [ ] Admin can modify runtime config without restart
- [ ] Dashboards show accurate real-time data
- [ ] Alerts fire correctly on threshold breach
- [ ] Logs searchable by correlation ID

### Dependencies

| Dependency | Source | Required For |
|------------|--------|--------------|
| All Kafka events | Phases 3, 6 | Analytics data |
| User management | Phase 2 | Moderation targets |
| Aspire setup | Phase 1 | Dashboard infrastructure |

---

## Cross-Cutting Concerns

### Implemented Throughout All Phases

#### Security (FR 10.x)

| FR ID | Requirement | Phase |
|-------|-------------|-------|
| FR 10.1 | Transport Security | Phase 1 (TLS setup) |
| FR 10.2 | Authentication Coverage | Phase 2 (JWT middleware) |
| FR 10.3 | Data Isolation | Phase 2+ (query filters) |
| FR 10.4 | Storage Governance | Phase 3-4 (MinIO config) |
| FR 10.5 | Token Lifecycle | Phase 2 (JWT/refresh) |

#### Observability (NF-4.x)

| Phase | Observability Milestone |
|-------|------------------------|
| 1 | OpenTelemetry exporters, Serilog setup, HTTP security headers |
| 2 | Auth metrics (`novatune_auth_login_total`), login/logout traces |
| 3 | Upload metrics (`novatune_upload_duration_seconds`), processing traces |
| 4 | Cache hit rates (`novatune_presigned_cache_hit_total`), lifecycle job metrics (`novatune_cleanup_objects_total`) |
| 5 | Streaming metrics (`novatune_stream_active_total`), bandwidth metrics |
| 6 | Query performance, index stats |
| 7 | Playlist/share metrics |
| 8 | Full dashboards, alerting |

**Canonical Metric Names (OTEL Semantic Conventions):**

| Metric | Labels | FR/NF Reference |
|--------|--------|-----------------|
| `novatune_upload_duration_seconds` | `{status, format}` | FR 2.5, NF-1.2 |
| `novatune_stream_active_total` | `{userId_hash}` | FR 5.1, NF-1.1 |
| `novatune_presigned_cache_hit_total` | `{outcome=hit\|miss}` | FR 5.2, NF-6.4 |
| `novatune_cleanup_objects_total` | `{action=deleted\|skipped}` | FR 4.4, NF-6.1 |
| `novatune_auth_login_total` | `{status=success\|failure}` | FR 1.2, NF-3.4 |

**SLO Alert Thresholds:**

| Metric | Threshold | Severity |
|--------|-----------|----------|
| `novatune_upload_duration_seconds` p95 | >5s | P2-High |
| `novatune_presigned_cache_hit_total` rate | <80% | P4-Low |
| RabbitMQ DLQ depth | >100 | P3-Medium |

#### Resilience (NF-2.x)

| Component | Polly Policy |
|-----------|--------------|
| MinIO | Retry (5x exponential), Circuit breaker (5 failures/30s) |
| RavenDB | Retry (3x), Circuit breaker |
| NCache | Retry (2x), Graceful degradation |
| Kafka | Retry (3x), Dead-letter queue |

#### CI/CD Pipeline Evolution

| Phase | Pipeline Additions |
|-------|-------------------|
| 1 | Build, format check, basic tests, secret scanning |
| 2 | Coverage gate (80% Services), auth tests |
| 3 | Integration tests (Testcontainers), property-based tests |
| 4 | NCache/MinIO integration tests, cache stampede tests |
| 5 | E2E streaming tests, HTTP Range request tests |
| 6 | Performance benchmarks, k6 load tests |
| 7 | Full E2E suite |
| 8 | SAST, DAST, dependency scanning, OpenAPI diff check |

**Additional CI Enhancements (NF-5.1):**
- OpenAPI schema diff check (fail on breaking changes without version bump)
- Secret scanning (gitleaks or similar)
- Test artifact upload: coverage reports, k6 HTML reports
- Aspire integration test with Testcontainers for full stack verification

#### Documentation Requirements

| Phase | Documentation Deliverable |
|-------|--------------------------|
| 1 | README.md (with quickstart), CLAUDE.md, AGENTS.md, `.env.example` |
| 2 | Auth API documentation (Scalar), ADR-0001 (Blazor selection) |
| 3 | Upload API documentation, event schemas, ADR-0003 (Kafka vs RabbitMQ) |
| 4 | Cache configuration guide, ADR-0002 (NCache selection), ADR-0004 (Presigned URL TTL) |
| 5 | Streaming integration guide, ADR-0005 (YARP placement) |
| 6 | Search/filter API documentation |
| 7 | Playlist/share API documentation |
| 8 | Admin guide, runbooks, COMMITTING.md |

**ADR Directory (`doc/adr/`):**

| ADR ID | Title | Decision Summary |
|--------|-------|------------------|
| ADR-0001 | Frontend Framework | Blazor selected for .NET ecosystem integration |
| ADR-0002 | Caching Solution | NCache selected over Redis for .NET native support |
| ADR-0003 | Message Broker Roles | Kafka for events, RabbitMQ for task queues |
| ADR-0004 | Presigned URL TTL | 10 min TTL with 8 min cache for safety margin |
| ADR-0005 | API Gateway Placement | YARP embedded in ApiService, not edge gateway |

---

## Milestone Summary

| Milestone | Phases | Definition of Done |
|-----------|--------|-------------------|
| **M1: Foundation** | 1-2 | Users can register, login, manage profile |
| **M2: Upload** | 3 | Users can upload audio files |
| **M3: Playback** | 4-5 | Users can stream their audio |
| **M4: Management** | 6 | Users can browse, search, edit, delete tracks |
| **M5: Extended** | 7 | Playlists and sharing functional |
| **M6: Production** | 8 | Full observability, admin controls |

---

## Traceability Matrix

| Phase | Functional Requirements | Non-Functional Requirements |
|-------|------------------------|----------------------------|
| 1 | — | NF-3.1, NF-3.6, NF-8.1, NF-8.4, NF-9.1, NF-9.3 |
| 2 | FR 1.1–1.4 | NF-3.2–3.4, NF-6.2 |
| 3 | FR 2.1–2.6, FR 3.1–3.3 | NF-1.1–1.2, NF-1.6, NF-2.2, NF-3.5, NF-6.3, NF-9.2 |
| 4 | FR 4.1–4.4 | NF-1.3, NF-2.4, NF-3.2, NF-6.1, NF-6.4 |
| 5 | FR 5.1–5.4 | NF-1.1, NF-1.5, NF-7.1–7.2, NF-7.4 |
| 6 | FR 6.1–6.5 | NF-1.4, NF-6.2–6.3 |
| 7 | FR 7.1–7.5, FR 8.1–8.3 | NF-6.2, NF-7.1–7.2, NF-9.4 |
| 8 | FR 9.1–9.3, FR 11.1–11.4 | NF-4.1–4.4, NF-5.1–5.2 |
| Cross | FR 10.1–10.5 | NF-2.1–2.3, NF-5.3–5.4, NF-8.2–8.3 |

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 2.2 | 2025-12-06 | Improvements from doc/plan/improve.md: Added checksum validation (Phase 3), cache stampede prevention with jittered TTL (Phase 4), canonical metric names and SLO thresholds (Cross-cutting), HTTP security headers (Phase 1), JWT security enhancements (Phase 2), ADR documentation requirements, CI pipeline enhancements, TimeProvider/IClock abstraction, property-based testing |
| 2.1 | 2025-11-23 | Simplified Phase 1: use existing Aspire structure instead of layered architecture, defer abstraction to when complexity demands it |
| 2.0 | 2025-11-23 | Comprehensive rewrite: added metadata, legend, phase overview table, dependency matrix, detailed per-phase sections with FR/NFR coverage, deliverables, exit criteria, risks, cross-cutting concerns, milestone summary, traceability matrix |
| 1.0 | 2025-11-22 | Initial 8-phase outline |
