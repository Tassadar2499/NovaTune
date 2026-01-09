# Stage 4 — Streaming URL Issuance + Caching

**Goal:** Allow streaming via short-lived presigned GET URLs without proxying bytes.

## Overview

```
┌─────────┐  POST /tracks/{trackId}/stream  ┌─────────────┐
│ Client  │ ───────────────────────────────►│ API Service │
└────┬────┘ ◄─────────────────────────────── └──────┬──────┘
     │       StreamResponse (presigned URL)        │
     │                                             │ 1. Validate ownership/status
     │                                             │ 2. Check cache (Garnet)
     │                                             │ 3. Generate presigned URL if miss
     │                                             │ 4. Encrypt + cache URL
     │                                             ▼
     │                                    ┌─────────────────┐
     │       GET (presigned, range)       │     MinIO       │
     │ ──────────────────────────────────►│ (audio bucket)  │
     │ ◄─────────────────────────────────┘└─────────────────┘
     │       Audio bytes (206 Partial Content)
     │
     │                                    ┌─────────────────┐
     │                                    │     Garnet      │
     │                                    │ (encrypted URL) │
     │                                    └─────────────────┘
```

---

## 1. API Endpoint: `POST /tracks/{trackId}/stream`

### Request

- **Method:** `POST`
- **Path:** `/tracks/{trackId}/stream`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the track or have explicit access

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `trackId` | string | ULID identifier for the track |

### Response Schema (Success: 200 OK)

```json
{
  "streamUrl": "https://minio.example.com/...",
  "expiresAt": "2025-01-08T12:32:00Z",
  "contentType": "audio/mpeg",
  "fileSizeBytes": 15728640,
  "supportsRangeRequests": true
}
```

### Validation Rules (Req 5.1, NF-6.1)

| Check | Rule | Error |
|-------|------|-------|
| Track exists | Track document must exist in RavenDB | `404 Not Found` |
| Ownership | `Track.UserId` must match authenticated user | `403 Forbidden` |
| Track status | `Track.Status` must be `Ready` | `409 Conflict` |
| User status | User must be `Active` | `403 Forbidden` |

### Rate Limiting (Req 8.2, NF-2.5)

- Policy: `stream-url`
- Default: 60 requests/minute per user
- Response on limit: `429 Too Many Requests` with `Retry-After` header

### Error Responses (RFC 7807)

```json
{
  "type": "https://novatune.dev/errors/track-not-ready",
  "title": "Track not ready for streaming",
  "status": 409,
  "detail": "Track is currently processing. Please wait until processing completes.",
  "instance": "/tracks/01HXK.../stream",
  "extensions": {
    "trackId": "01HXK...",
    "currentStatus": "Processing"
  }
}
```

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-track-id` | Malformed ULID |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own track or is suspended |
| `404` | `track-not-found` | Track does not exist |
| `409` | `track-not-ready` | Track status is not `Ready` |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |
| `503` | `service-unavailable` | MinIO or cache unavailable |

---

## 2. Streaming Service

### Interface: `IStreamingService`

```csharp
namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for generating and caching presigned streaming URLs.
/// </summary>
public interface IStreamingService
{
    /// <summary>
    /// Gets or generates a presigned streaming URL for a track.
    /// </summary>
    /// <param name="trackId">Track identifier (ULID).</param>
    /// <param name="userId">Requesting user identifier (ULID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Streaming URL response with expiry.</returns>
    Task<StreamUrlResult> GetStreamUrlAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates cached streaming URL for a track.
    /// Called on track deletion or user logout.
    /// </summary>
    Task InvalidateCacheAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates all cached streaming URLs for a user.
    /// Called on user logout (all sessions).
    /// </summary>
    Task InvalidateAllUserCacheAsync(
        string userId,
        CancellationToken ct = default);
}

/// <summary>
/// Result of streaming URL generation.
/// </summary>
public record StreamUrlResult(
    string StreamUrl,
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes,
    bool SupportsRangeRequests);
```

### Implementation: `StreamingService`

```csharp
public class StreamingService : IStreamingService
{
    private readonly IStorageService _storageService;
    private readonly IEncryptedCacheService _encryptedCache;
    private readonly IDocumentSession _session;
    private readonly IOptions<StreamingOptions> _options;
    private readonly ILogger<StreamingService> _logger;

    public async Task<StreamUrlResult> GetStreamUrlAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        // 1. Check cache first
        var cacheKey = BuildCacheKey(userId, trackId);
        var cached = await _encryptedCache.GetAsync<CachedStreamUrl>(cacheKey, ct);

        if (cached is not null && cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            _logger.LogDebug("Cache hit for streaming URL {TrackId}", trackId);
            return new StreamUrlResult(
                cached.Url,
                cached.ExpiresAt,
                cached.ContentType,
                cached.FileSizeBytes,
                true);
        }

        // 2. Load track from RavenDB
        var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);
        // Validation happens in endpoint; service assumes valid input

        // 3. Generate presigned GET URL
        var expiry = _options.Value.PresignExpiry;
        var url = await _storageService.GeneratePresignedDownloadUrlAsync(
            track.ObjectKey,
            expiry,
            ct);

        // 4. Cache with encryption (TTL slightly shorter than presign)
        var cacheTtl = expiry - TimeSpan.FromSeconds(30);
        var cacheEntry = new CachedStreamUrl(
            url.Url,
            url.ExpiresAt,
            track.MimeType,
            track.FileSizeBytes);

        await _encryptedCache.SetAsync(cacheKey, cacheEntry, cacheTtl, ct);

        _logger.LogDebug(
            "Generated and cached streaming URL for {TrackId}, expires at {ExpiresAt}",
            trackId, url.ExpiresAt);

        return new StreamUrlResult(
            url.Url,
            url.ExpiresAt,
            track.MimeType,
            track.FileSizeBytes,
            true);
    }

    public async Task InvalidateCacheAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(userId, trackId);
        await _encryptedCache.RemoveAsync(cacheKey, ct);
        _logger.LogDebug("Invalidated streaming URL cache for {TrackId}", trackId);
    }

    public async Task InvalidateAllUserCacheAsync(
        string userId,
        CancellationToken ct = default)
    {
        // Use pattern-based deletion: stream:{userId}:*
        var pattern = $"stream:{userId}:*";
        await _encryptedCache.RemoveByPatternAsync(pattern, ct);
        _logger.LogDebug("Invalidated all streaming URL caches for user {UserId}", userId);
    }

    private static string BuildCacheKey(string userId, string trackId)
        => $"stream:{userId}:{trackId}";
}
```

---

## 3. Cache Behavior (Req 10.2, Req 10.3, NF-3.3)

### Cache Key Design

| Component | Format | Example |
|-----------|--------|---------|
| Prefix | `stream` | `stream` |
| User ID | ULID | `01HXK...` |
| Track ID | ULID | `01HYZ...` |
| Full Key | `stream:{userId}:{trackId}` | `stream:01HXK...:01HYZ...` |

### Cached Value Schema

```csharp
internal record CachedStreamUrl(
    string Url,          // Encrypted presigned URL
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes);
```

### TTL Strategy (NF-3.3)

| Environment | Presign Expiry | Cache TTL | Buffer |
|-------------|----------------|-----------|--------|
| `dev`/`staging` | 2 minutes | 90 seconds | 30s |
| `prod` | 60-120 seconds | 30-90 seconds | 30s |

The cache TTL is always **30 seconds shorter** than the presign expiry to prevent serving near-expired URLs.

### Encryption at Rest (Req 10.3, NF-3.2)

#### Interface: `IEncryptedCacheService`

```csharp
namespace NovaTuneApp.ApiService.Infrastructure.Caching;

/// <summary>
/// Cache service that encrypts sensitive values at rest.
/// Used for presigned URLs per Req 10.3.
/// </summary>
public interface IEncryptedCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
```

#### Encryption Implementation

```csharp
public class EncryptedCacheService : IEncryptedCacheService
{
    private readonly ICacheService _innerCache;
    private readonly ICacheEncryptionProvider _encryption;

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        var encrypted = await _encryption.EncryptAsync(json, ct);
        var wrapper = new EncryptedCacheEntry(encrypted, _encryption.CurrentKeyVersion);
        await _innerCache.SetAsync(key, wrapper, ttl, ct);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var wrapper = await _innerCache.GetAsync<EncryptedCacheEntry>(key, ct);
        if (wrapper is null) return default;

        var json = await _encryption.DecryptAsync(wrapper.Ciphertext, wrapper.KeyVersion, ct);
        return JsonSerializer.Deserialize<T>(json);
    }
}

internal record EncryptedCacheEntry(byte[] Ciphertext, string KeyVersion);
```

#### Key Management (NF-3.2)

| Environment | Key Source | Rotation |
|-------------|------------|----------|
| `prod` | External KMS (AWS KMS, Azure Key Vault, etc.) | Quarterly or on incident |
| `dev`/`staging` | Kubernetes Secret or environment variable | Manual |

```csharp
public interface ICacheEncryptionProvider
{
    string CurrentKeyVersion { get; }
    Task<byte[]> EncryptAsync(string plaintext, CancellationToken ct = default);
    Task<string> DecryptAsync(byte[] ciphertext, string keyVersion, CancellationToken ct = default);
}
```

**Key rotation support:** The `keyVersion` field in cached entries allows decryption with previous keys during rotation windows.

### Cache Invalidation Triggers (Req 10.2)

| Event | Action | Implementation |
|-------|--------|----------------|
| Track deletion | Invalidate `stream:{userId}:{trackId}` | `TrackDeletedHandler` |
| User logout (single session) | No action (URLs expire naturally) | — |
| User logout (all sessions) | Invalidate `stream:{userId}:*` | `AuthService.LogoutAllAsync()` |
| Track status change to non-Ready | Invalidate `stream:{userId}:{trackId}` | `TrackService` |

---

## 4. Storage Service Extension

Add presigned GET URL generation to `IStorageService`:

```csharp
/// <summary>
/// Generates a presigned GET URL for streaming.
/// </summary>
/// <param name="objectKey">The storage object key.</param>
/// <param name="expiry">URL expiry duration.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Presigned URL and expiry timestamp.</returns>
Task<PresignedDownloadResult> GeneratePresignedDownloadUrlAsync(
    string objectKey,
    TimeSpan expiry,
    CancellationToken ct = default);

public record PresignedDownloadResult(string Url, DateTimeOffset ExpiresAt);
```

### Implementation

```csharp
public async Task<PresignedDownloadResult> GeneratePresignedDownloadUrlAsync(
    string objectKey,
    TimeSpan expiry,
    CancellationToken ct = default)
{
    return await _presignPipeline.ExecuteAsync(async token =>
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(_audioBucket)
            .WithObject(objectKey)
            .WithExpiry((int)expiry.TotalSeconds);

        var url = await _minioClient.PresignedGetObjectAsync(args);
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

        _logger.LogDebug(
            "Generated presigned download URL for {ObjectKey}, expires at {ExpiresAt}",
            objectKey, expiresAt);

        return new PresignedDownloadResult(url, expiresAt);
    }, ct);
}
```

---

## 5. Range Requests (Req 5.3)

### MinIO Configuration

MinIO natively supports HTTP Range requests. Ensure the bucket and presigned URLs allow byte-range playback:

1. **Bucket CORS configuration** (if cross-origin playback needed):

```json
{
  "CORSRules": [{
    "AllowedOrigins": ["https://app.novatune.dev"],
    "AllowedMethods": ["GET", "HEAD"],
    "AllowedHeaders": ["Range", "Content-Range"],
    "ExposeHeaders": ["Accept-Ranges", "Content-Range", "Content-Length"],
    "MaxAgeSeconds": 3600
  }]
}
```

2. **Client behavior:**
   - Clients send `Range: bytes=0-1048575` header
   - MinIO responds with `206 Partial Content` and `Content-Range` header
   - Presigned URLs include all necessary auth for range requests

### Response Headers (from MinIO)

```
HTTP/1.1 206 Partial Content
Accept-Ranges: bytes
Content-Range: bytes 0-1048575/15728640
Content-Length: 1048576
Content-Type: audio/mpeg
```

---

## 6. Configuration

### `StreamingOptions`

```csharp
public class StreamingOptions
{
    public const string SectionName = "Streaming";

    /// <summary>
    /// Presigned URL expiry duration.
    /// Default: 2 minutes (dev), 60-120 seconds (prod).
    /// </summary>
    public TimeSpan PresignExpiry { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Cache TTL buffer (subtracted from presign expiry).
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CacheTtlBuffer { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Rate limit: requests per minute per user.
    /// Default: 60.
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 60;
}
```

### `appsettings.json` Example

```json
{
  "Streaming": {
    "PresignExpiry": "00:02:00",
    "CacheTtlBuffer": "00:00:30",
    "RateLimitPerMinute": 60
  },
  "CacheEncryption": {
    "KeyVersion": "v1",
    "Algorithm": "AES-256-GCM"
  }
}
```

---

## 7. Endpoint Implementation

### `StreamEndpoints.cs`

```csharp
namespace NovaTuneApp.ApiService.Endpoints;

public static class StreamEndpoints
{
    public static void MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tracks")
            .RequireAuthorization(PolicyNames.ActiveUser)
            .WithTags("Streaming");

        group.MapPost("/{trackId}/stream", HandleStreamRequest)
            .WithName("GetStreamUrl")
            .WithSummary("Get presigned streaming URL for a track")
            .Produces<StreamResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting("stream-url");
    }

    private static async Task<IResult> HandleStreamRequest(
        [FromRoute] string trackId,
        [FromServices] IStreamingService streamingService,
        [FromServices] IAsyncDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // 1. Validate track ID format
        if (!Ulid.TryParse(trackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        // 2. Load and validate track
        var track = await session.LoadAsync<Track>($"Tracks/{trackId}", ct);
        if (track is null)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }

        // 3. Check ownership
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (track.UserId != userId)
        {
            return Results.Problem(
                title: "Access denied",
                detail: "You do not have permission to access this track.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/forbidden");
        }

        // 4. Check track status
        if (track.Status != TrackStatus.Ready)
        {
            return Results.Problem(
                title: "Track not ready for streaming",
                detail: $"Track is currently {track.Status}. Please wait until processing completes.",
                statusCode: StatusCodes.Status409Conflict,
                type: "https://novatune.dev/errors/track-not-ready",
                extensions: new Dictionary<string, object?>
                {
                    ["trackId"] = trackId,
                    ["currentStatus"] = track.Status.ToString()
                });
        }

        // 5. Get or generate streaming URL
        var result = await streamingService.GetStreamUrlAsync(trackId, userId, ct);

        return Results.Ok(new StreamResponse(
            result.StreamUrl,
            result.ExpiresAt,
            result.ContentType,
            result.FileSizeBytes,
            result.SupportsRangeRequests));
    }
}

public record StreamResponse(
    string StreamUrl,
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes,
    bool SupportsRangeRequests);
```

---

## 8. Observability (NF-4.x)

### Logging

| Event | Level | Fields |
|-------|-------|--------|
| Stream URL requested | Info | `UserId`, `TrackId`, `CorrelationId` |
| Cache hit | Debug | `TrackId`, `ExpiresAt` |
| Cache miss - generating URL | Debug | `TrackId` |
| Presigned URL generated | Debug | `TrackId`, `ExpiresAt` |
| Cache entry encrypted and stored | Debug | `TrackId`, `KeyVersion` |
| Access denied (ownership) | Warning | `UserId`, `TrackId`, `OwnerId` |
| Track not ready | Warning | `TrackId`, `Status` |
| Cache invalidated | Debug | `TrackId`, `UserId` |

**Redaction (NF-4.5):** Never log presigned URLs or object keys.

### Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `stream_url_requests_total` | Counter | `status` (success/error), `cache_hit` (true/false) |
| `stream_url_request_duration_ms` | Histogram | — |
| `stream_url_cache_hits_total` | Counter | — |
| `stream_url_cache_misses_total` | Counter | — |
| `stream_url_presign_generated_total` | Counter | — |
| `stream_cache_invalidations_total` | Counter | `reason` (deletion/logout/status_change) |

### Tracing

- Propagate `CorrelationId` from request to cache and storage operations
- Span hierarchy:
  - `stream.get_url` (parent)
    - `cache.get` (child)
    - `storage.presign` (child, on cache miss)
    - `cache.set` (child, on cache miss)

---

## 9. Resilience (NF-1.4)

### Timeouts

| Operation | Timeout | Retries |
|-----------|---------|---------|
| Cache read | 500ms | 0 (cache is optional) |
| Cache write | 500ms | 0 (cache is optional) |
| MinIO presign generation | 5s | 1 |
| RavenDB read (Track) | 2s | 1 |

### Circuit Breaker

| Dependency | Failure Threshold | Half-Open After |
|------------|-------------------|-----------------|
| Garnet/Redis | 5 consecutive | 30s |
| MinIO | 5 consecutive | 30s |

### Fail-Open vs Fail-Closed

| Dependency | Behavior |
|------------|----------|
| Cache unavailable | **Fail-open**: Generate presigned URL directly (no caching) |
| MinIO unavailable | **Fail-closed**: Return `503 Service Unavailable` |
| RavenDB unavailable | **Fail-closed**: Return `503 Service Unavailable` |

---

## 10. Security Considerations

### URL Security (NF-3.3)

- Presigned URLs are short-lived (60-120 seconds in production)
- URLs are user+track scoped (cannot access other users' tracks)
- URLs are encrypted in cache (AES-256-GCM)
- Object keys are guess-resistant (contain random suffix)

### Access Control

- Only track owners can request streaming URLs
- Deleted tracks (`Status=Deleted`) cannot be streamed
- Suspended users cannot request streaming URLs
- Rate limiting prevents abuse

### Audit Trail

Stream URL requests are logged with:
- User ID
- Track ID
- Correlation ID
- Timestamp
- Success/failure status

---

## 11. Test Strategy

### Unit Tests

- `StreamingService`: Cache hit/miss scenarios
- `StreamingService`: TTL calculation
- `EncryptedCacheService`: Encrypt/decrypt round-trip
- `EncryptedCacheService`: Key version handling
- Cache key generation
- Validation logic in endpoint

### Integration Tests

- End-to-end stream URL flow (cache miss → presign → cache)
- Cache invalidation on track deletion
- Cache invalidation on logout
- Rate limiting enforcement
- Range request verification (client → MinIO)

---

## 12. Implementation Tasks

### API Service

- [ ] Add `IStreamingService` interface and `StreamingService` implementation
- [ ] Add `IEncryptedCacheService` interface and `EncryptedCacheService` implementation
- [ ] Add `ICacheEncryptionProvider` interface with AES-256-GCM implementation
- [ ] Add `GeneratePresignedDownloadUrlAsync` to `IStorageService`
- [ ] Add `POST /tracks/{trackId}/stream` endpoint
- [ ] Add `StreamingOptions` configuration class
- [ ] Add rate limiting policy `stream-url`
- [ ] Add streaming metrics to `NovaTuneMetrics`

### Infrastructure

- [ ] Add cache encryption key configuration to AppHost
- [ ] Configure MinIO CORS for range requests (if cross-origin needed)
- [ ] Add `CacheEncryption` configuration section

### Event Handlers

- [ ] Update `TrackDeletedHandler` to invalidate streaming cache
- [ ] Add cache invalidation to `AuthService.LogoutAllAsync()`

### Testing

- [ ] Unit tests for `StreamingService`
- [ ] Unit tests for `EncryptedCacheService`
- [ ] Integration tests for stream URL flow
- [ ] Integration tests for cache invalidation

---

## Requirements Covered

- `Req 5.1` — Listener can request playback for owned tracks
- `Req 5.2` — Short-lived presigned GET URL with cache reuse
- `Req 5.3` — Range request support for byte-range playback
- `Req 10.2` — Cache presigned URLs by user+track with invalidation
- `Req 10.3` — Encrypted cache entries for presigned URLs
- `Req 10.4` — Configurable cache behavior (key prefix, TTLs)
- `NF-2.3` — Efficient caching reduces MinIO presign calls
- `NF-3.3` — Short-lived, scoped presigned URLs
- `NF-6.1` — No streaming for deleted tracks

---

## Open Items

- [ ] Determine exact presign TTL for production (60s vs 120s)
- [ ] Finalize KMS integration for production encryption keys
- [ ] Define CORS policy for cross-origin audio players
- [ ] Determine if waveform data should also use presigned URLs
- [ ] Consider adding track-level access sharing (future scope)
