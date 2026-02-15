# System Patterns

## Architecture Overview
```
Vue.js Apps (Player + Admin)
    |  HTTP/JWT
    v
API Service (Minimal APIs)
    |-> Garnet (Cache)
    |-> RavenDB (Data)
    |-> MinIO (S3 Storage)
    |-> Redpanda (Events)
         |
    Workers: UploadIngestor | AudioProcessor | Lifecycle | Telemetry
```

## Service Registration Pattern
```csharp
// Aspire defaults first
builder.AddServiceDefaults();

// Infrastructure
builder.AddRedisClient("cache");
builder.AddRavenDb();
builder.AddNovaTuneMessaging();

// DI scoping
Singleton: ICacheService, IStorageService, validators, workers
Scoped: ITrackManagementService, IPlaylistService, IAuthService, IAdminService
Transient: handlers, message producers
```

## Middleware Pipeline
```
CORS -> CorrelationId -> SerilogRequestLogging -> LoginRateLimitMiddleware ->
RateLimiter -> Authentication -> Authorization
```

## API Patterns
- **Minimal APIs** with route groups (not controllers)
- **RFC 7807 Problem Details** for all error responses
- **Cursor pagination** with stable ordering for list endpoints
- **OpenAPI/Scalar** for API documentation

## Messaging Pattern
- **Topics**: `{env}-audio-events`, `{env}-track-deletions`, `{env}-telemetry`, `{env}-minio-events`, `{env}-dlq`
- **KafkaFlow** consumers with retry logic (30 attempts, 2s delay for broker connection)
- **Outbox pattern**: Domain events -> OutboxMessage in same RavenDB transaction -> Background processor publishes to Kafka

## Database Patterns
- **RavenDB** as sole data store (document database)
- **15 static indexes** for efficient queries
- **ULID** identifiers for all entities
- **Soft delete** with `DeletedAt` timestamp and `DeletionGracePeriod` (30 days)

## Key RavenDB Indexes
- `Users_ByEmail`, `Users_ForAdminSearch`
- `Tracks_ByUserForSearch`, `Tracks_ByScheduledDeletion`, `Tracks_ForAdminSearch`
- `Playlists_ByUserForSearch`, `Playlists_ByTrackReference`
- `TrackDailyAggregates_ByDateRange`, `TrackDailyAggregates_TopTracks`
- `AuditLogs_ByFilters`
- `OutboxMessages_ByStatus`
- `UploadSessions_ByUserAndStatus`, `UploadSessions_ByStatusAndExpiry`
- `RefreshTokens_ByUserAndHash`
- `UserActivityAggregates_ByUser`

## Security Patterns
- JWT access tokens (15min) + refresh token rotation (1h)
- Argon2id password hashing (65536 KB memory, 3 iterations, 4 parallelism)
- AES-GCM encryption for cached presigned URLs
- Rate limiting on auth endpoints (10 requests/min/IP)
- Role-based authorization (Listener, Admin)

## Resilience Patterns
- Polly pipelines: circuit breaker + retry + timeout
- ResilientTrackManagementService decorator
- Graceful shutdown: 60s timeout for in-flight messages
- Feature flags: `Features__MessagingEnabled`, `Features__StorageEnabled`

## Testing Patterns
- xUnit + Shouldly assertions
- In-memory fakes for unit test isolation
- Aspire.Hosting.Testing for integration tests
- `[Trait("Category", "Aspire")]` for infrastructure-dependent tests
