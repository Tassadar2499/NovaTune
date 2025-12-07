# Cross-Cutting Concerns

> These concerns are implemented across all phases and must be maintained throughout the project.

---

## Security (FR 10.x)

Security requirements that span multiple phases.

### Implementation by Phase

| FR ID | Requirement | Phase | Implementation |
|-------|-------------|-------|----------------|
| FR 10.1 | Transport Security | Phase 1 | TLS configuration, HSTS |
| FR 10.2 | Authentication Coverage | Phase 2 | JWT middleware on all protected endpoints |
| FR 10.3 | Data Isolation | Phase 2+ | User ID query filters on all data access |
| FR 10.4 | Storage Governance | Phase 3-4 | MinIO bucket policies, SSE encryption |
| FR 10.5 | Token Lifecycle | Phase 2 | JWT rotation, refresh token revocation |

### Security Checklist

Every endpoint must verify:

- [ ] Authentication required (unless explicitly public)
- [ ] Authorization checked (ownership, roles)
- [ ] Input validated and sanitized
- [ ] Output doesn't leak sensitive data
- [ ] Rate limiting applied
- [ ] Audit logging for sensitive operations

### Secure Coding Guidelines

```csharp
// ALWAYS: Use parameterized queries
var tracks = await session.Query<Track>()
    .Where(t => t.UserId == userId) // ✓ Safe
    .ToListAsync();

// NEVER: String concatenation in queries
var query = $"FROM Tracks WHERE UserId = '{userId}'"; // ✗ Vulnerable

// ALWAYS: Validate ownership before operations
public async Task<Track?> GetTrackAsync(string trackId, string userId)
{
    var track = await _repository.GetByIdAsync(trackId);
    if (track?.UserId != userId)
        return null; // Return null, not the track
    return track;
}

// ALWAYS: Hash user IDs in logs
_logger.LogInformation("User action. UserId={UserId}", userId.Hash());
```

---

## Observability (NF-4.x)

### Observability Milestones by Phase

| Phase | Milestone |
|-------|-----------|
| 1 | OpenTelemetry exporters, Serilog setup, HTTP security headers |
| 2 | Auth metrics (`novatune_auth_login_total`), login/logout traces |
| 3 | Upload metrics (`novatune_upload_duration_seconds`), processing traces |
| 4 | Cache hit rates (`novatune_presigned_cache_hit_total`), lifecycle job metrics |
| 5 | Streaming metrics (`novatune_stream_active_total`), bandwidth metrics |
| 6 | Query performance, index stats |
| 7 | Playlist/share metrics |
| 8 | Full dashboards, alerting |

### Canonical Metric Names (OTEL Semantic Conventions)

| Metric | Type | Labels | FR/NF Reference |
|--------|------|--------|-----------------|
| `novatune_upload_duration_seconds` | Histogram | `{status, format}` | FR 2.5, NF-1.2 |
| `novatune_upload_bytes_total` | Counter | `{format}` | FR 2.3 |
| `novatune_stream_active_total` | Gauge | `{userId_hash}` | FR 5.1, NF-1.1 |
| `novatune_stream_bytes_total` | Counter | | FR 5.1 |
| `novatune_presigned_cache_hit_total` | Counter | `{outcome=hit\|miss}` | FR 5.2, NF-6.4 |
| `novatune_cleanup_objects_total` | Counter | `{action=deleted\|skipped}` | FR 4.4, NF-6.1 |
| `novatune_auth_login_total` | Counter | `{status=success\|failure\|lockout}` | FR 1.2, NF-3.4 |
| `novatune_auth_token_refresh_total` | Counter | `{status}` | FR 1.2 |
| `novatune_track_search_duration_seconds` | Histogram | | FR 6.4, NF-1.4 |
| `novatune_playlist_operations_total` | Counter | `{operation}` | FR 7.x |

### Metric Implementation Example

```csharp
public class UploadMetrics
{
    private readonly Counter<long> _uploadTotal;
    private readonly Counter<long> _uploadBytesTotal;
    private readonly Histogram<double> _uploadDuration;

    public UploadMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("NovaTune.Upload");

        _uploadTotal = meter.CreateCounter<long>(
            "novatune_upload_total",
            description: "Total number of upload attempts");

        _uploadBytesTotal = meter.CreateCounter<long>(
            "novatune_upload_bytes_total",
            unit: "bytes",
            description: "Total bytes uploaded");

        _uploadDuration = meter.CreateHistogram<double>(
            "novatune_upload_duration_seconds",
            unit: "s",
            description: "Upload processing duration");
    }

    public void RecordUpload(string status, string format, long bytes, TimeSpan duration)
    {
        _uploadTotal.Add(1,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("format", format));

        if (status == "success")
        {
            _uploadBytesTotal.Add(bytes,
                new KeyValuePair<string, object?>("format", format));
        }

        _uploadDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("format", format));
    }
}
```

### SLO Alert Thresholds

| Metric | Threshold | Severity | Response Time |
|--------|-----------|----------|---------------|
| `novatune_upload_duration_seconds` p95 | >5s | P2-High | 1 hour |
| `novatune_presigned_cache_hit_total` rate | <80% | P4-Low | Next day |
| `novatune_auth_login_total{status=failure}` rate | >10% | P2-High | 1 hour |
| RabbitMQ DLQ depth | >100 | P3-Medium | 4 hours |
| Error rate (5xx) | >5% for 5min | P1-Critical | 15 min |
| Latency p95 | 2x baseline | P2-High | 1 hour |

### Distributed Tracing

Every request should:

1. Propagate W3C trace context
2. Create spans for significant operations
3. Add relevant attributes

```csharp
public async Task<UploadResult> UploadAsync(...)
{
    using var activity = ActivitySource.StartActivity("Upload.Process");
    activity?.SetTag("upload.userId", userId.Hash());
    activity?.SetTag("upload.format", contentType);

    using (var validationSpan = ActivitySource.StartActivity("Upload.Validate"))
    {
        // Validation logic
        validationSpan?.SetTag("validation.result", "success");
    }

    using (var storageSpan = ActivitySource.StartActivity("Upload.Store"))
    {
        // Storage logic
        storageSpan?.SetTag("storage.bytes", fileSize);
    }

    activity?.SetStatus(ActivityStatusCode.Ok);
    return result;
}
```

### Structured Logging Requirements

All logs must include:

- Correlation ID (automatically via middleware)
- Timestamp (ISO 8601)
- Log level
- Service name
- Requirement ID (when applicable)

```csharp
// Good logging
_logger.LogInformation(
    "Track uploaded successfully. TrackId={TrackId}, UserId={UserId}, Size={SizeBytes}, Format={Format}",
    trackId,
    userId.Hash(),
    sizeBytes,
    format);

// Bad logging (avoid)
_logger.LogInformation($"Track {trackId} uploaded by user {userId}"); // No structured data
```

---

## Resilience (NF-2.x)

### Polly Policies by Component

| Component | Retry Policy | Circuit Breaker | Fallback |
|-----------|--------------|-----------------|----------|
| MinIO | 5x exponential (100ms-3.2s) | 5 failures/30s | Fail with error |
| RavenDB | 3x exponential | 5 failures/30s | Fail with error |
| NCache | 2x (100ms) | 3 failures/15s | Bypass cache |
| Kafka | 3x exponential | 10 failures/60s | Dead-letter queue |
| RabbitMQ | 3x exponential | 5 failures/30s | Fail with error |

### Policy Implementation

```csharp
public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetMinioPolicy()
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<MinioException>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Log.Warning(exception,
                        "MinIO retry {RetryCount} after {Delay}ms",
                        retryCount, timeSpan.TotalMilliseconds);
                });

        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<MinioException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    Log.Error(exception,
                        "MinIO circuit breaker opened for {Duration}s",
                        duration.TotalSeconds);
                },
                onReset: () => Log.Information("MinIO circuit breaker reset"));

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }
}
```

### Graceful Degradation Patterns

```csharp
// Cache bypass fallback
public async Task<PresignedUrlResult> GetPresignedUrlAsync(string trackId, string userId)
{
    try
    {
        var cached = await _cache.GetAsync<PresignedUrlResult>($"presigned:{userId}:{trackId}");
        if (cached is not null)
            return cached with { FromCache = true };
    }
    catch (NCacheException ex)
    {
        _logger.LogWarning(ex, "Cache unavailable, generating fresh URL");
        _metrics.IncrementCacheFallback();
    }

    // Generate fresh URL (fallback)
    return await GeneratePresignedUrlAsync(trackId, userId);
}
```

---

## CI/CD Pipeline Evolution

### Pipeline Additions by Phase

| Phase | Additions |
|-------|-----------|
| 1 | Build, format check, basic tests, secret scanning |
| 2 | Coverage gate (80% Services), auth tests |
| 3 | Integration tests (Testcontainers), property-based tests |
| 4 | NCache/MinIO integration tests, cache stampede tests |
| 5 | E2E streaming tests, HTTP Range request tests |
| 6 | Performance benchmarks, k6 load tests |
| 7 | Full E2E suite |
| 8 | SAST, DAST, dependency scanning, OpenAPI diff check |

### Complete CI Workflow

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore /p:TreatWarningsAsErrors=true

      - name: Format Check
        run: dotnet format --verify-no-changes

  test:
    runs-on: ubuntu-latest
    needs: build
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4

      - name: Test with Coverage
        run: dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

      - name: Upload Coverage
        uses: actions/upload-artifact@v4
        with:
          name: coverage
          path: '**/coverage.cobertura.xml'

  security:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Secret Scanning
        uses: gitleaks/gitleaks-action@v2

      - name: SAST
        uses: semgrep/semgrep-action@v1

      - name: Dependency Scan
        uses: snyk/actions/dotnet@master
        with:
          args: --severity-threshold=high

  integration:
    runs-on: ubuntu-latest
    needs: build
    services:
      ravendb:
        image: ravendb/ravendb:6.0-ubuntu-latest
        ports:
          - 8080:8080
      minio:
        image: minio/minio:latest
        ports:
          - 9000:9000

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4

      - name: Integration Tests
        run: dotnet test --filter "Category=Integration"

  openapi-check:
    runs-on: ubuntu-latest
    if: github.event_name == 'pull_request'
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Check OpenAPI Breaking Changes
        run: |
          npx openapi-diff main:openapi.json HEAD:openapi.json --fail-on-breaking
```

---

## Documentation Requirements

### Documentation by Phase

| Phase | Documentation Deliverables |
|-------|---------------------------|
| 1 | README.md (quickstart), CLAUDE.md, AGENTS.md, `.env.example` |
| 2 | Auth API docs (Scalar), ADR-0001 (Blazor selection) |
| 3 | Upload API docs, event schemas, ADR-0003 (Kafka vs RabbitMQ) |
| 4 | Cache configuration guide, ADR-0002 (NCache), ADR-0004 (URL TTL) |
| 5 | Streaming integration guide, ADR-0005 (YARP placement) |
| 6 | Search/filter API documentation |
| 7 | Playlist/share API documentation |
| 8 | Admin guide, runbooks, COMMITTING.md |

### Architecture Decision Records (ADRs)

Store in `doc/adr/`:

| ADR ID | Title | Decision Summary |
|--------|-------|------------------|
| ADR-0001 | Frontend Framework | Blazor selected for .NET ecosystem integration |
| ADR-0002 | Caching Solution | NCache selected over Redis for .NET native support |
| ADR-0003 | Message Broker Roles | Kafka for events, RabbitMQ for task queues |
| ADR-0004 | Presigned URL TTL | 10 min TTL with 8 min cache for safety margin |
| ADR-0005 | API Gateway Placement | YARP embedded in ApiService, not edge gateway |

### ADR Template

```markdown
# ADR-XXXX: Title

## Status

Accepted | Proposed | Deprecated | Superseded by ADR-YYYY

## Context

What is the issue that we're seeing that is motivating this decision?

## Decision

What is the change that we're proposing and/or doing?

## Consequences

What becomes easier or more difficult to do because of this change?
```

---

## Testing Standards

### Test Naming Convention

```
{Target}.Tests.cs         # Unit tests
{Target}.IntegrationTests.cs  # Integration tests
```

### Test Organization

```
NovaTuneApp.Tests/
├── Unit/
│   ├── Services/
│   │   ├── AuthServiceTests.cs
│   │   └── UploadServiceTests.cs
│   ├── Validators/
│   │   └── UploadValidatorTests.cs
│   └── Models/
│       └── TrackTests.cs
├── Integration/
│   ├── Api/
│   │   ├── AuthEndpointsTests.cs
│   │   └── TrackEndpointsTests.cs
│   └── Infrastructure/
│       ├── RavenDbTests.cs
│       └── MinioTests.cs
├── E2E/
│   └── PlaybackTests.cs
└── Fixtures/
    ├── TestWebApplicationFactory.cs
    └── TestContainerFixture.cs
```

### Coverage Targets

| Area | Target |
|------|--------|
| Services (business logic) | ≥80% |
| Auth middleware | ≥80% |
| Validators | 100% |
| Infrastructure adapters | ≥60% |
| Models/Entities | Core validation only |

---

## Environment Configuration

### Configuration Hierarchy

1. `appsettings.json` - Base configuration
2. `appsettings.{Environment}.json` - Environment overrides
3. Environment variables - Runtime overrides
4. User secrets - Local development secrets

### Required Environment Variables

```bash
# Database
RAVENDB_URL=http://localhost:8080
RAVENDB_DATABASE=NovaTune

# Storage
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin

# Messaging
KAFKA_BOOTSTRAP_SERVERS=localhost:9092
RABBITMQ_HOST=localhost

# Caching
NCACHE_SERVER=localhost:9800
NCACHE_CACHE_NAME=novatune-cache

# Security
JWT_ISSUER=https://novatune.local
JWT_AUDIENCE=novatune-api
JWT_SIGNING_KEY_PATH=./keys/signing.pem

# Observability
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

---

## Navigation

← [Phase 8: Observability & Admin](phases/phase-8-observability-admin.md) | [Overview](overview.md)
