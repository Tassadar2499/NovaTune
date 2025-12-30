# Phase 7: Documentation Updates

This phase updates ALL documentation across the `doc/` directory and project root to reflect the Redpanda and Garnet migration. Documentation must be updated before the migration is considered complete.

---

## 7.1 Update doc/requirements/stack.md

**File:** `doc/requirements/stack.md`

Replace entire Messaging and Data & Storage sections:

```markdown
## Runtime & Framework
- C# 12
- ASP.NET Core (.NET 9.0)
- Dotnet Aspire (orchestration)

## Data & Storage
- RavenDB (document database, sole data store)
- MinIO (S3-compatible object storage for audio files)
- Garnet (Redis-compatible distributed cache from Microsoft)
  - AOF persistence enabled for durability
  - Used for presigned URL caching, session state
  - StackExchange.Redis client

## Messaging
- Redpanda (Kafka-compatible event streaming platform)
  - Replaces both Apache Kafka and RabbitMQ
  - Topics: `{env}-audio-events`, `{env}-track-deletions`
  - JSON message format with schema versioning
  - SASL/SCRAM + TLS in stage/prod environments
- KafkaFlow (.NET Kafka client framework)
  - Typed message handlers with dependency injection
  - Middleware pipeline for serialization/retry
  - Admin dashboard for debugging

## Authentication
- ASP.NET Identity (custom RavenDB IUserStore/IRoleStore implementation required)
- JWT tokens with refresh flow

## Audio Processing
- FFmpeg/FFprobe (metadata extraction, format validation; deployed via base Docker image)

## Observability
- Serilog (structured JSON logging with correlation IDs)
- OpenTelemetry (metrics, traces via Dotnet Aspire)

## API & Gateway
- YARP (reverse proxy, API gateway)
- Scalar (OpenAPI documentation UI)

## Infrastructure
- Docker
- Kubernetes
- GitHub Actions (CI/CD)

## Testing
- xUnit
- Testcontainers (integration tests with MinIO, RavenDB, Redpanda, Garnet)

## Frontend
- Vue.js
- TypeScript
```

---

## 7.2 Update doc/implementation/init.md

**File:** `doc/implementation/init.md`

Apply these changes throughout the document:

### 7.2.1 Phase 1 Changes
- Line ~106: Change `Infrastructure/ ... Kafka, NCache` to `Infrastructure/ ... Redpanda, Garnet`
- Line ~131: Change `Docker Compose: RavenDB, MinIO, NCache, Kafka, RabbitMQ` to `Docker Compose: RavenDB, MinIO, Garnet, Redpanda`

### 7.2.2 Phase 2 Changes
- Line ~216: Change `Token revocation via NCache blocklist` to `Token revocation via Garnet blocklist`
- Line ~245: Change `NCache entry invalidation` to `Garnet cache invalidation`
- Line ~252: Change `NCache region: auth-tokens` to `Garnet cache prefix: auth-tokens`
- Line ~289: Change `NCache with sub-5s propagation` to `Garnet with sub-5s propagation`
- Line ~289: Change `retry with RabbitMQ` to `retry with Redpanda topic`

### 7.2.3 Phase 3 Changes
- Line ~361-374: Update Kafka event schema comment to mention Redpanda compatibility
- Line ~381: Change `RabbitMQ waveform-jobs queue` to `Redpanda waveform-jobs topic`
- Line ~391: Change `Kafka topic: audio-events` to `Redpanda topic: {env}-audio-events`
- Line ~392: Change `RabbitMQ queue: waveform-jobs` to `Redpanda topic: {env}-waveform-jobs`
- Line ~436: Change `Kafka unavailability | RabbitMQ dead-letter` to `Redpanda unavailability | Dead-letter topic`

### 7.2.4 Phase 4 Changes
- Line ~443-445: Change `caching via NCache` to `caching via Garnet`
- Line ~469-486: Replace NCache references with Garnet/StackExchange.Redis
- Line ~505-509: Change `NCache cluster configuration` to `Garnet configuration`
- Line ~506: Change `NCache regions` to `Garnet cache prefixes`
- Line ~507: Change `Kafka topic` to `Redpanda topic`
- Line ~551: Change `NCache cluster failure` to `Garnet unavailability`

### 7.2.5 Phase 6 Changes
- Line ~718-719: Change `Kafka tombstone to track-deletions` to `Redpanda tombstone to {env}-track-deletions`
- Line ~719: Change `NCache invalidation` to `Garnet cache invalidation`
- Line ~760: Change `Kafka infrastructure` to `Redpanda infrastructure`

### 7.2.6 Cross-Cutting Changes
- Line ~1031: Change `RabbitMQ DLQ depth` to `Redpanda DLQ topic depth`
- Lines ~1037-1041: Update Polly policies table:
  ```markdown
  | Component | Polly Policy |
  |-----------|--------------|
  | MinIO | Retry (5x exponential), Circuit breaker (5 failures/30s) |
  | RavenDB | Retry (3x), Circuit breaker |
  | Garnet | Retry (2x), Graceful degradation |
  | Redpanda (KafkaFlow) | Retry (3x), Dead-letter topic |
  ```
- Lines ~1066-1068: Update documentation requirements for Phase 4:
  ```markdown
  | 4 | Cache configuration guide, ADR-0002 (Garnet selection), ADR-0004 (Presigned URL TTL) |
  ```
- Lines ~1076-1083: Update ADR table:
  ```markdown
  | ADR ID | Title | Decision Summary |
  |--------|-------|------------------|
  | ADR-0001 | Frontend Framework | Blazor selected for .NET ecosystem integration |
  | ADR-0002 | Caching Solution | Garnet selected for Redis compatibility and Microsoft support |
  | ADR-0003 | Message Broker | Redpanda selected to unify Kafka+RabbitMQ workloads |
  | ADR-0004 | Presigned URL TTL | 10 min TTL with 8 min cache for safety margin |
  | ADR-0005 | API Gateway Placement | YARP embedded in ApiService, not edge gateway |
  | ADR-0006 | Redpanda Migration | Redpanda replaces Kafka+RabbitMQ for unified streaming |
  | ADR-0007 | Garnet Migration | Garnet replaces NCache for Redis compatibility |
  ```

---

## 7.3 Update doc/implementation/overview.md

**File:** `doc/implementation/overview.md`

Update Phase Overview table row for Phase 4:
```markdown
| 4 | Storage & Access Control | FR 4.x | NF-1.3, NF-3.2, NF-6.1, NF-6.4 | Presigned URLs, Garnet cache, lifecycle jobs, stampede prevention | ⏳ |
```

---

## 7.4 Update doc/implementation/cross-cutting.md

**File:** `doc/implementation/cross-cutting.md`

### 7.4.1 Update Resilience Policies Table (~lines 207-213)
```markdown
### Polly Policies by Component

| Component | Retry Policy | Circuit Breaker | Fallback |
|-----------|--------------|-----------------|----------|
| MinIO | 5x exponential (100ms-3.2s) | 5 failures/30s | Fail with error |
| RavenDB | 3x exponential | 5 failures/30s | Fail with error |
| Garnet | 2x (100ms) | 3 failures/15s | Bypass cache |
| Redpanda (KafkaFlow) | 3x exponential | 10 failures/60s | Dead-letter topic |
```

### 7.4.2 Update Policy Implementation Example (~lines 218-253)
Replace `NCacheException` references with Redis exceptions:
```csharp
var retryPolicy = Policy
    .Handle<RedisConnectionException>()
    .Or<RedisTimeoutException>()
    .WaitAndRetryAsync(...);
```

### 7.4.3 Update Graceful Degradation Example (~lines 259-276)
Replace `NCacheException` with `RedisException`:
```csharp
catch (RedisException ex)
{
    _logger.LogWarning(ex, "Garnet cache unavailable, generating fresh URL");
    _metrics.IncrementCacheFallback();
}
```

### 7.4.4 Update SLO Alert Thresholds Table (~line 142)
```markdown
| Redpanda DLQ topic depth | >100 | P3-Medium | 4 hours |
```

### 7.4.5 Update Environment Variables Section (~lines 510-525)
```bash
# Messaging (Redpanda)
REDPANDA_BROKERS=localhost:19092
KAFKA_BOOTSTRAP_SERVERS=localhost:19092
KAFKA_TOPIC_PREFIX=dev
KAFKA_SASL_ENABLED=false

# Caching (Garnet)
GARNET_CONNECTION=localhost:6379
GARNET_PASSWORD=
GARNET_SSL_ENABLED=false
```

---

## 7.5 Update doc/implementation/phases/phase-1-infrastructure.md

**File:** `doc/implementation/phases/phase-1-infrastructure.md`

### 7.5.1 Update Task 1.1.2 Folder Structure (~line 49)
```
└── Infrastructure/   # External adapters (MinIO, RavenDB, Redpanda, Garnet)
```

### 7.5.2 Replace Task 1.3.1 Docker Compose (~lines 139-200)
Replace entire docker-compose.yml content with Redpanda and Garnet:
```yaml
services:
  ravendb:
    image: ravendb/ravendb:6.0-ubuntu-latest
    # ... (unchanged)

  minio:
    image: minio/minio:latest
    # ... (unchanged)

  redpanda:
    image: redpandadata/redpanda:v24.2.4
    container_name: novatune-redpanda
    command:
      - redpanda start
      - --smp 1
      - --memory 1G
      - --reserve-memory 0M
      - --overprovisioned
      - --node-id 0
      - --kafka-addr internal://0.0.0.0:9092,external://0.0.0.0:19092
      - --advertise-kafka-addr internal://redpanda:9092,external://localhost:19092
      - --pandaproxy-addr internal://0.0.0.0:8082,external://0.0.0.0:18082
      - --advertise-pandaproxy-addr internal://redpanda:8082,external://localhost:18082
    ports:
      - "19092:19092"  # Kafka API (external)
      - "18082:18082"  # Pandaproxy (REST)
      - "9644:9644"    # Admin API
    volumes:
      - redpanda-data:/var/lib/redpanda/data
    healthcheck:
      test: ["CMD", "rpk", "cluster", "health"]
      interval: 30s
      timeout: 10s
      retries: 5

  garnet:
    image: ghcr.io/microsoft/garnet:1.0.44
    container_name: novatune-garnet
    ports:
      - "6379:6379"
    command: ["--checkpointdir", "/data/checkpoints", "--aof", "--aof-path", "/data/aof"]
    volumes:
      - garnet-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 30s
      timeout: 10s
      retries: 5

volumes:
  ravendb-data:
  minio-data:
  redpanda-data:
  garnet-data:
```

### 7.5.3 Update Task 1.3.3 Environment Variables (~lines 204-232)
```bash
# RavenDB
RAVENDB_URL=http://localhost:8080
RAVENDB_DATABASE=NovaTune

# MinIO
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_BUCKET=novatune-dev-audio

# Messaging (Redpanda via Kafka protocol)
KAFKA_BOOTSTRAP_SERVERS=localhost:19092
KAFKA_TOPIC_PREFIX=dev
KAFKA_SASL_ENABLED=false
KAFKA_SASL_USERNAME=
KAFKA_SASL_PASSWORD=

# Caching (Garnet via Redis protocol)
GARNET_CONNECTION=localhost:6379
GARNET_PASSWORD=
GARNET_SSL_ENABLED=false

# JWT
JWT_ISSUER=https://novatune.local
JWT_AUDIENCE=novatune-api
JWT_SIGNING_KEY_PATH=./keys/signing.pem
```

### 7.5.4 Update Task 1.4.1 Aspire AppHost (~lines 256-280)
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// External resources
var ravendb = builder.AddConnectionString("ravendb");
var minio = builder.AddConnectionString("minio");

// Garnet (Redis-compatible cache)
var cache = builder.AddRedis("cache")
    .WithDataVolume("garnet-data");

// Redpanda (Kafka-compatible messaging)
var messaging = builder.AddKafka("messaging")
    .WithDataVolume("redpanda-data");

// API Service
var apiService = builder.AddProject<Projects.NovaTuneApp_ApiService>("apiservice")
    .WithReference(ravendb)
    .WithReference(minio)
    .WithReference(cache)
    .WithReference(messaging);

// Web Frontend
builder.AddProject<Projects.NovaTuneApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WithReference(cache);

builder.Build().Run();
```

---

## 7.6 Update doc/implementation/phases/phase-2-user-management.md

**File:** `doc/implementation/phases/phase-2-user-management.md`

### 7.6.1 Update Task 2.2.6 Token Revocation (~line 245)
Change `Implement token revocation via NCache blocklist` to:
```
Implement token revocation via Garnet blocklist
```

### 7.6.2 Update Task 2.5.4 (~line 468)
Change `Implement NCache session invalidation` to:
```
Implement Garnet session invalidation
```

### 7.6.3 Update Task 2.6.6 (~line 529)
Change `Add RabbitMQ queue for async email delivery` to:
```
Add Redpanda topic for async email delivery
```

### 7.6.4 Update Infrastructure Setup (~lines 577-582)
```markdown
- [ ] RavenDB database and collections: `Users`, `RefreshTokens`
- [ ] RavenDB indexes: `Users_ByEmail`, `Users_ByStatus`
- [ ] Garnet cache prefix: `auth-tokens:`
- [ ] Email service integration (SMTP or SendGrid)
- [ ] RSA key pair for JWT signing
```

### 7.6.5 Update Risks & Mitigation Table (~lines 609-616)
```markdown
| Risk | Impact | Mitigation |
|------|--------|------------|
| RavenDB Identity store complexity | High | Reference existing implementations, extensive tests |
| Token revocation latency | Medium | Garnet with sub-5s propagation |
| Email delivery delays | Low | Async processing, retry with Redpanda topic |
```

---

## 7.7 Update doc/implementation/phases/phase-3-audio-upload.md

**File:** `doc/implementation/phases/phase-3-audio-upload.md`

### 7.7.1 Update Objective (~line 9)
Change `event publishing to Kafka` to:
```
event publishing to Redpanda via KafkaFlow
```

### 7.7.2 Update Task 3.6 Title (~line 613)
Change `Kafka Event Publishing` to:
```
### Task 3.6: Redpanda Event Publishing (via KafkaFlow)
```

### 7.7.3 Replace Task 3.6.3 KafkaEventPublisher (~lines 673-689)
```csharp
// Infrastructure/Messaging/MessageProducerService.cs
public sealed class MessageProducerService : IMessageProducerService
{
    private readonly IMessageProducer _audioProducer;

    public MessageProducerService(IProducerAccessor producerAccessor)
    {
        _audioProducer = producerAccessor.GetProducer("audio-producer");
    }

    public async Task PublishAudioUploadedAsync(AudioUploadedEvent evt, CancellationToken ct)
    {
        await _audioProducer.ProduceAsync(
            messageKey: evt.TrackId,
            messageValue: evt,
            headers: new MessageHeaders { { "schema-version", "1"u8.ToArray() } }
        );
    }
}
```

### 7.7.4 Update Task 3.6.4 Topic Configuration (~lines 691-696)
```markdown
- Topic: `{env}-audio-events` (Redpanda)
  - Retention: 30 days
  - Partitions: 6 (scale with upload volume)
  - Environment prefix from KAFKA_TOPIC_PREFIX
```

### 7.7.5 Update Task 3.6.5 (~line 697)
Change `Add dead-letter queue fallback to RabbitMQ` to:
```
Add dead-letter topic fallback in Redpanda
```

### 7.7.6 Update Task 3.7 Title (~line 708)
Change `Background Metadata Processing` to:
```
### Task 3.7: Background Metadata Processing (via Redpanda)
```

### 7.7.7 Update Task 3.7.3 Queue Configuration (~lines 783-786)
```markdown
- Topic: `{env}-metadata-extraction` (Redpanda)
  - Consumer group: `{env}-metadata-processor`
  - Retry with exponential backoff via KafkaFlow middleware
```

### 7.7.8 Update Task 3.8.3 (~line 829)
Change `Configure RabbitMQ queue: waveform-jobs` to:
```
Configure Redpanda topic: `{env}-waveform-jobs`
```

### 7.7.9 Update Infrastructure Setup (~lines 910-915)
```markdown
- [ ] MinIO buckets: `novatune-dev-audio`, `novatune-test-audio`
- [ ] MinIO bucket policies (private, SSE-S3)
- [ ] MinIO lifecycle rules (3 versions, 7 day expiry)
- [ ] Redpanda topics: `{env}-audio-events`, `{env}-metadata-extraction` (via KafkaFlow auto-create)
- [ ] Redpanda topic: `{env}-waveform-jobs` (optional)
- [ ] FFmpeg/FFprobe in Docker image
```

### 7.7.10 Update Risks Table (~lines 952-957)
```markdown
| Risk | Impact | Mitigation |
|------|--------|------------|
| FFprobe timeout on large files | Medium | 30s limit, background processing |
| MinIO connection failures | High | Polly retry with circuit breaker |
| Redpanda unavailability | High | KafkaFlow retry + dead-letter topic |
| Memory pressure from large uploads | High | Streaming (no buffering) |
```

---

## 7.8 Update doc/implementation/phases/phase-4-storage-access.md

**File:** `doc/implementation/phases/phase-4-storage-access.md`

### 7.8.1 Update Objective (~line 9)
Change `caching via NCache` to:
```
caching via Garnet (Redis-compatible)
```

### 7.8.2 Update Task 4.2 Title (~line 154)
Change `NCache Integration` to:
```
### Task 4.2: Garnet Cache Integration
```

### 7.8.3 Replace Task 4.2.2 NCacheService (~lines 189-240)
```csharp
public sealed class GarnetCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public GarnetCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task SetAsync<T>(
        string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(value);
        await db.StringSetAsync(key, json, ttl);
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
        where T : class
    {
        // Single-flight pattern for cache stampede prevention
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        var semaphore = _locks.GetOrAdd($"lock:{key}", _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            cached = await GetAsync<T>(key, ct);
            if (cached is not null)
                return cached;

            var value = await factory(ct);
            var jitteredTtl = ApplyJitter(ttl);
            await SetAsync(key, value, jitteredTtl, ct);
            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static TimeSpan ApplyJitter(TimeSpan baseTtl)
    {
        var jitterFactor = 0.9 + (Random.Shared.NextDouble() * 0.2);
        return TimeSpan.FromTicks((long)(baseTtl.Ticks * jitterFactor));
    }
}
```

### 7.8.4 Update Task 4.2.3 Configuration (~lines 243-250)
```csharp
// Configure Garnet (Redis-compatible) via Aspire
builder.AddRedisClient("cache");
builder.Services.AddSingleton<ICacheService, GarnetCacheService>();
```

### 7.8.5 Update Task 4.2.5 Graceful Degradation (~lines 259-280)
Change `NCacheException` to `RedisException`:
```csharp
catch (RedisException ex)
{
    _logger.LogWarning(ex, "Garnet unavailable, using fallback");
    _metrics.IncrementCacheFallback();
}
```

### 7.8.6 Update Task 4.5.1 Consumer (~lines 469-499)
Change `IKafkaConsumer` to KafkaFlow handler:
```csharp
public class TrackDeletionHandler : IMessageHandler<TrackDeleted>
{
    public async Task Handle(IMessageContext context, TrackDeleted message)
    {
        // Process deletion...
    }
}
```

### 7.8.7 Update Task 4.5.3 Kafka Consumer Config (~lines 537-539)
```markdown
- Topic: `{env}-track-deletions` (Redpanda, compacted)
- Consumer group: `{env}-lifecycle-processor`
- KafkaFlow typed handler
```

### 7.8.8 Update Infrastructure Setup (~lines 649-655)
```markdown
- [ ] Garnet connection via Aspire `AddRedis()`
- [ ] Garnet cache prefixes: `presigned-urls:`, `access-tokens:`
- [ ] Redpanda topic: `{env}-track-deletions` (compacted)
- [ ] Background worker service for lifecycle jobs
- [ ] MinIO bucket policies and lifecycle rules
```

### 7.8.9 Update Risks Table (~lines 689-696)
```markdown
| Risk | Impact | Mitigation |
|------|--------|------------|
| Cache stampede | Medium | Request coalescing, jittered TTL |
| Garnet unavailability | High | Graceful degradation to direct MinIO |
| Orphan false positives | High | 7-day grace period, audit logging |
```

---

## 7.9 Update doc/implementation/phases/phase-8-observability-admin.md

**File:** `doc/implementation/phases/phase-8-observability-admin.md`

### 7.9.1 Update Task 8.1 Title (~line 42)
Change `Build Kafka consumers` to:
```
Build Redpanda consumers via KafkaFlow
```

### 7.9.2 Update Task 8.1.1 Consumer (~lines 48-73)
Change `IKafkaConsumer` to KafkaFlow handler:
```csharp
public class UploadAnalyticsHandler : IMessageHandler<AudioUploadedEvent>
{
    private readonly IAnalyticsService _analytics;
    private readonly ILogger<UploadAnalyticsHandler> _logger;

    public UploadAnalyticsHandler(
        IAnalyticsService analytics,
        ILogger<UploadAnalyticsHandler> logger)
    {
        _analytics = analytics;
        _logger = logger;
    }

    public async Task Handle(IMessageContext context, AudioUploadedEvent message)
    {
        await _analytics.RecordUploadAsync(new UploadMetric
        {
            UserId = message.UserId,
            TrackId = message.TrackId,
            Format = message.MimeType,
            SizeBytes = message.FileSizeBytes,
            Timestamp = message.Timestamp
        });

        _logger.LogInformation(
            "Recorded upload analytics. TrackId={TrackId}, Size={Size}",
            message.TrackId, message.FileSizeBytes);
    }
}
```

---

## 7.10 Update CLAUDE.md

**File:** `CLAUDE.md` (project root)

Update Tech Stack section:
```markdown
## Tech Stack

- **Runtime:** .NET 9.0 / C# 12
- **Orchestration:** Dotnet Aspire 13.0
- **Database:** RavenDB (sole document store; custom IUserStore/IRoleStore for ASP.NET Identity)
- **Object Storage:** MinIO (S3-compatible)
- **Caching:** Garnet (Redis-compatible distributed cache via StackExchange.Redis)
- **Messaging:** Redpanda (Kafka-compatible) with KafkaFlow client framework
- **Auth:** ASP.NET Identity with JWT + refresh tokens
- **Audio:** FFmpeg/FFprobe (via base Docker image)
- **Gateway:** YARP (reverse proxy)
- **API Docs:** Scalar (OpenAPI UI)
- **Logging:** Serilog (structured JSON with correlation IDs)
- **Observability:** OpenTelemetry (metrics, traces via Aspire)
- **Testing:** xUnit, Testcontainers
- **Frontend:** Vue.js + TypeScript
```

---

## 7.11 Create ADRs

### 7.11.1 Create doc/adr/ADR-0006-redpanda-migration.md
```markdown
# ADR-0006: Redpanda Migration

## Status
Accepted

## Context
NovaTune originally used Apache Kafka for event streaming and RabbitMQ for task queues. Managing two separate messaging systems increases operational complexity and infrastructure costs.

## Decision
Replace both Apache Kafka and RabbitMQ with Redpanda, a Kafka-compatible streaming platform. Use KafkaFlow as the .NET client framework.

## Consequences
**Positive:**
- Single messaging platform reduces operational complexity
- Redpanda is simpler to operate (no ZooKeeper dependency)
- KafkaFlow provides better .NET integration with typed handlers and DI
- Lower resource footprint than Kafka

**Negative:**
- Team needs to learn Redpanda-specific tooling (rpk CLI)
- Some Kafka-specific features may behave differently
- KafkaFlow learning curve for existing Confluent.Kafka users
```

### 7.11.2 Create doc/adr/ADR-0007-garnet-migration.md
```markdown
# ADR-0007: Garnet Migration

## Status
Accepted

## Context
NovaTune originally used NCache for distributed caching. While NCache is .NET-native, it has licensing costs and limited community adoption compared to Redis-compatible solutions.

## Decision
Replace NCache with Garnet, Microsoft's Redis-compatible cache server. Use StackExchange.Redis as the client library.

## Consequences
**Positive:**
- Open-source with Microsoft backing
- Redis protocol compatibility enables standard tooling
- StackExchange.Redis is widely adopted with excellent documentation
- AOF persistence provides durability
- Works seamlessly with Aspire's `AddRedis()` hosting

**Negative:**
- Garnet is newer/less mature than Redis
- Some NCache-specific features may not have direct equivalents
- Requires migration of cache key patterns
```

---

## 7.12 Update README.md

**File:** `README.md` (project root)

Update infrastructure section:
```markdown
## Local Development

### Prerequisites
- .NET 9.0 SDK
- Docker Desktop (8GB+ RAM recommended)
- Node.js 18+ (for frontend)

### Start Infrastructure
```bash
# Start all infrastructure services
docker compose up -d

# Verify services are healthy
docker compose ps

# Check Redpanda cluster
docker exec novatune-redpanda rpk cluster info

# Check Garnet
docker exec novatune-garnet redis-cli ping
```

### Environment Variables
Copy `.env.example` to `.env` and configure:
```bash
# Messaging (Redpanda)
KAFKA_BOOTSTRAP_SERVERS=localhost:19092
KAFKA_TOPIC_PREFIX=dev

# Caching (Garnet)
GARNET_CONNECTION=localhost:6379
```

---

## 7.13 Update Task 1.3 Subdocuments (if they exist)

Check and update any files in `doc/implementation/phases/phase-1/task-1.3/` that reference Kafka, RabbitMQ, or NCache.

---

## Verification Checklist
- [ ] `grep -r "NCache" doc/` returns no results
- [ ] `grep -r "RabbitMQ" doc/` returns no results
- [ ] `grep -r "Apache Kafka" doc/` returns no results (Kafka protocol references are OK)
- [ ] `grep -r "Confluent" doc/` returns no results (except historical context)
- [ ] `grep -r "Alachisoft" doc/` returns no results
- [ ] All ADR files created
- [ ] CLAUDE.md updated
- [ ] README.md updated

**Exit Criteria:** All documentation across `doc/` directory reflects Redpanda and Garnet; no legacy technology references remain except in historical/migration context.
