# Phase 4: Storage & Access Control (FR 4.x)

> **Status:** ⏳ Pending
> **Dependencies:** Phase 1, Phase 2, Phase 3 (partial - needs MinIO objects)
> **Milestone:** M3 - Playback

## Objective

Implement secure access to stored audio with presigned URL generation, caching via NCache, and lifecycle management for orphaned objects.

---

## FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 4.1 | Secure Storage | P1 | Test |
| FR 4.2 | Signed URLs | P1 | Test |
| FR 4.3 | Access Control | P1 | Test |
| FR 4.4 | Lifecycle Management | P2 | Test |

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-1.3 | Statelessness | Presigned URLs, no session state |
| NF-2.4 | Graceful Degradation | Cache bypass fallback |
| NF-3.2 | Data Protection | Bucket policies, SSE |
| NF-6.1 | Lifecycle Rules | Orphan cleanup, tombstone processing |
| NF-6.4 | Cache Management | TTL policies, eviction |

---

## Tasks

### Task 4.1: Presigned URL Service

**Priority:** P1 (Must-have)

Create service for generating time-limited presigned URLs.

#### Subtasks

- [ ] **4.1.1** Create `IPresignedUrlService` interface:
  ```csharp
  public interface IPresignedUrlService
  {
      Task<PresignedUrlResult> GenerateAsync(
          string trackId,
          string userId,
          PresignedUrlOptions? options = null,
          CancellationToken ct = default);

      Task InvalidateAsync(
          string trackId,
          CancellationToken ct = default);
  }

  public record PresignedUrlResult(
      string Url,
      DateTimeOffset ExpiresAt,
      bool FromCache);

  public record PresignedUrlOptions(
      TimeSpan? Ttl = null,
      bool ForceRefresh = false);
  ```

- [ ] **4.1.2** Implement `PresignedUrlService`:
  ```csharp
  public sealed class PresignedUrlService : IPresignedUrlService
  {
      private readonly IStorageService _storage;
      private readonly ITrackRepository _tracks;
      private readonly ICacheService _cache;
      private readonly TimeProvider _timeProvider;
      private readonly IOptions<PresignedUrlOptions> _options;

      public async Task<PresignedUrlResult> GenerateAsync(
          string trackId,
          string userId,
          PresignedUrlOptions? options = null,
          CancellationToken ct = default)
      {
          var effectiveOptions = options ?? _options.Value;
          var cacheKey = $"presigned:{userId}:{trackId}";

          // Check cache first (unless force refresh)
          if (!effectiveOptions.ForceRefresh)
          {
              var cached = await _cache.GetAsync<CachedPresignedUrl>(cacheKey, ct);
              if (cached is not null &&
                  cached.ExpiresAt > _timeProvider.GetUtcNow().AddMinutes(2))
              {
                  return new PresignedUrlResult(cached.Url, cached.ExpiresAt, true);
              }
          }

          // Load track and verify ownership
          var track = await _tracks.GetByIdAsync(trackId, ct);
          if (track is null)
              throw new NotFoundException("Track not found");
          if (track.UserId != userId)
              throw new ForbiddenException("Access denied");

          // Generate presigned URL
          var ttl = effectiveOptions.Ttl ?? _options.Value.DefaultTtl;
          var url = await _storage.GeneratePresignedUrlAsync(
              track.ObjectKey, ttl, ct);

          var expiresAt = _timeProvider.GetUtcNow().Add(ttl);

          // Cache with jittered TTL
          var cacheTtl = CalculateJitteredCacheTtl(ttl);
          await _cache.SetAsync(cacheKey,
              new CachedPresignedUrl(url, expiresAt),
              cacheTtl, ct);

          return new PresignedUrlResult(url, expiresAt, false);
      }

      private TimeSpan CalculateJitteredCacheTtl(TimeSpan urlTtl)
      {
          // Cache for 80% of URL TTL with jitter
          var baseTtl = urlTtl * 0.8;
          var jitter = Random.Shared.NextDouble() * 0.2; // 0-20% jitter
          return baseTtl * (0.9 + jitter);
      }
  }
  ```

- [ ] **4.1.3** Configure default TTLs:
  ```csharp
  public class PresignedUrlConfig
  {
      public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(10);
      public TimeSpan MinTtl { get; set; } = TimeSpan.FromMinutes(5);
      public TimeSpan MaxTtl { get; set; } = TimeSpan.FromHours(1);
      public TimeSpan CacheMargin { get; set; } = TimeSpan.FromMinutes(2);
  }
  ```

- [ ] **4.1.4** Implement URL signature verification

- [ ] **4.1.5** Write unit tests with deterministic TimeProvider

#### Acceptance Criteria
- URLs generated with correct TTL
- Signature verification works
- Configurable TTL limits enforced

---

### Task 4.2: NCache Integration

**Priority:** P1 (Must-have)

Integrate NCache for presigned URL caching with stampede prevention.

#### Subtasks

- [ ] **4.2.1** Create `ICacheService` interface:
  ```csharp
  public interface ICacheService
  {
      Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
          where T : class;

      Task SetAsync<T>(
          string key,
          T value,
          TimeSpan ttl,
          CancellationToken ct = default)
          where T : class;

      Task RemoveAsync(string key, CancellationToken ct = default);

      Task<T> GetOrCreateAsync<T>(
          string key,
          Func<CancellationToken, Task<T>> factory,
          TimeSpan ttl,
          CancellationToken ct = default)
          where T : class;
  }
  ```

- [ ] **4.2.2** Implement `NCacheService`:
  ```csharp
  public sealed class NCacheService : ICacheService
  {
      private readonly ICache _cache;
      private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

      public async Task<T> GetOrCreateAsync<T>(
          string key,
          Func<CancellationToken, Task<T>> factory,
          TimeSpan ttl,
          CancellationToken ct = default)
          where T : class
      {
          // Try cache first
          var cached = await GetAsync<T>(key, ct);
          if (cached is not null)
              return cached;

          // Single-flight pattern for cache stampede prevention
          var lockKey = $"lock:{key}";
          var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

          await semaphore.WaitAsync(ct);
          try
          {
              // Double-check after acquiring lock
              cached = await GetAsync<T>(key, ct);
              if (cached is not null)
                  return cached;

              // Generate value
              var value = await factory(ct);

              // Cache with jittered TTL
              var jitteredTtl = ApplyJitter(ttl);
              await SetAsync(key, value, jitteredTtl, ct);

              return value;
          }
          finally
          {
              semaphore.Release();
              // Cleanup old locks periodically
          }
      }

      private static TimeSpan ApplyJitter(TimeSpan baseTtl)
      {
          // Apply 10-30% jitter to prevent synchronized expiration
          var jitterFactor = 0.9 + (Random.Shared.NextDouble() * 0.2);
          return TimeSpan.FromTicks((long)(baseTtl.Ticks * jitterFactor));
      }
  }
  ```

- [ ] **4.2.3** Configure NCache regions:
  ```csharp
  services.AddNCacheDistributedCache(options =>
  {
      options.CacheName = "novatune-cache";
      options.EnableLogs = true;
      options.ExceptionsEnabled = true;
  });
  ```

- [ ] **4.2.4** Implement cache eviction policies:
  - LRU eviction when >80% capacity
  - Absolute expiration for presigned URLs
  - Sliding expiration for session data

- [ ] **4.2.5** Add graceful degradation on cache failure:
  ```csharp
  public async Task<T?> GetWithFallbackAsync<T>(
      string key,
      Func<Task<T>> fallback,
      CancellationToken ct)
      where T : class
  {
      try
      {
          var cached = await GetAsync<T>(key, ct);
          if (cached is not null)
              return cached;
      }
      catch (NCacheException ex)
      {
          _logger.LogWarning(ex, "Cache unavailable, using fallback");
          _metrics.IncrementCacheFallback();
      }

      return await fallback();
  }
  ```

- [ ] **4.2.6** Add cache metrics:
  ```csharp
  novatune_presigned_cache_hit_total{outcome="hit|miss"}
  novatune_cache_latency_seconds{operation="get|set|delete"}
  novatune_cache_fallback_total
  ```

- [ ] **4.2.7** Write integration tests for cache operations

- [ ] **4.2.8** Write tests for stampede prevention

#### Acceptance Criteria
- Cache hit rate >90% under normal load
- Single-flight pattern prevents stampede
- Jittered TTL prevents synchronized expiration
- Graceful fallback when cache unavailable
- All operations use TimeProvider for testability

---

### Task 4.3: TimeProvider Abstraction

**Priority:** P1 (Must-have)

Implement time abstraction for deterministic testing.

#### Subtasks

- [ ] **4.3.1** Configure .NET 8 TimeProvider:
  ```csharp
  // Registration
  services.AddSingleton(TimeProvider.System);

  // In tests
  services.AddSingleton<TimeProvider>(new FakeTimeProvider());
  ```

- [ ] **4.3.2** Create `FakeTimeProvider` for testing:
  ```csharp
  public sealed class FakeTimeProvider : TimeProvider
  {
      private DateTimeOffset _now = DateTimeOffset.UtcNow;

      public override DateTimeOffset GetUtcNow() => _now;

      public void Advance(TimeSpan duration) => _now = _now.Add(duration);
      public void SetUtcNow(DateTimeOffset value) => _now = value;
  }
  ```

- [ ] **4.3.3** Update all TTL calculations to use TimeProvider

- [ ] **4.3.4** Write tests with fixed time:
  ```csharp
  [Fact]
  public async Task PresignedUrl_ExpiresAtCorrectTime()
  {
      var fakeTime = new FakeTimeProvider();
      fakeTime.SetUtcNow(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));

      var service = new PresignedUrlService(fakeTime, ...);

      var result = await service.GenerateAsync("track-1", "user-1");

      Assert.Equal(
          fakeTime.GetUtcNow().AddMinutes(10),
          result.ExpiresAt);
  }
  ```

#### Acceptance Criteria
- All time-dependent code uses TimeProvider
- Tests are deterministic
- No direct DateTime.UtcNow usage

---

### Task 4.4: Access Control Middleware

**Priority:** P1 (Must-have)

Implement ownership verification and access control.

#### Subtasks

- [ ] **4.4.1** Create `IAccessControlService`:
  ```csharp
  public interface IAccessControlService
  {
      Task<AccessDecision> CheckAccessAsync(
          string userId,
          string resourceId,
          ResourceType resourceType,
          Permission permission,
          CancellationToken ct = default);
  }

  public enum ResourceType { Track, Playlist }
  public enum Permission { Read, Write, Delete }

  public record AccessDecision(
      bool Allowed,
      string? DenialReason);
  ```

- [ ] **4.4.2** Implement ownership verification:
  ```csharp
  public sealed class AccessControlService : IAccessControlService
  {
      public async Task<AccessDecision> CheckAccessAsync(
          string userId,
          string resourceId,
          ResourceType resourceType,
          Permission permission,
          CancellationToken ct)
      {
          var resource = await LoadResourceAsync(resourceId, resourceType, ct);

          if (resource is null)
              return new AccessDecision(false, "Resource not found");

          // Check ownership
          if (resource.OwnerId == userId)
              return new AccessDecision(true, null);

          // Check share rules (Phase 7 preparation)
          var shareRule = await CheckShareRulesAsync(userId, resourceId, permission, ct);
          if (shareRule.Allowed)
              return shareRule;

          // Log access denial
          _logger.LogWarning(
              "Access denied. User={UserId}, Resource={ResourceId}, Permission={Permission}",
              userId.Hash(), resourceId, permission);

          return new AccessDecision(false, "Access denied");
      }
  }
  ```

- [ ] **4.4.3** Create authorization filter:
  ```csharp
  public class TrackOwnershipFilter : IEndpointFilter
  {
      public async ValueTask<object?> InvokeAsync(
          EndpointFilterInvocationContext context,
          EndpointFilterDelegate next)
      {
          var trackId = context.HttpContext
              .GetRouteValue("id")?.ToString();
          var userId = context.HttpContext.User.GetUserId();

          var access = await _accessControl.CheckAccessAsync(
              userId, trackId!, ResourceType.Track, Permission.Read);

          if (!access.Allowed)
              return Results.StatusCode(access.DenialReason == "Resource not found"
                  ? 404 : 403);

          return await next(context);
      }
  }
  ```

- [ ] **4.4.4** Apply to track endpoints

- [ ] **4.4.5** Add access denial logging and metrics

- [ ] **4.4.6** Write authorization tests

#### Acceptance Criteria
- Ownership verified on all track operations
- 403 returned for unauthorized access
- 404 returned for non-existent resources
- Access denials logged

---

### Task 4.5: Lifecycle Background Jobs

**Priority:** P2 (Should-have)

Implement cleanup jobs for deleted content and orphaned objects.

#### Subtasks

- [ ] **4.5.1** Create `TrackDeletionConsumer`:
  ```csharp
  public class TrackDeletionConsumer : IKafkaConsumer<TrackDeleted>
  {
      public async Task ConsumeAsync(
          TrackDeleted @event,
          CancellationToken ct)
      {
          _logger.LogInformation(
              "Processing track deletion. TrackId={TrackId}, UserId={UserId}",
              @event.TrackId, @event.UserId);

          // 24-hour grace period
          var graceEnd = @event.DeletedAt.AddHours(24);
          if (_timeProvider.GetUtcNow() < graceEnd)
          {
              // Requeue for later processing
              await _messageQueue.DelayAsync(@event, graceEnd - _timeProvider.GetUtcNow());
              return;
          }

          // Delete from MinIO
          await _storage.DeleteAsync(@event.ObjectKey, ct);

          // Remove from cache
          await _cache.RemoveAsync($"presigned:*:{@event.TrackId}", ct);

          // Publish cleanup complete event
          await _eventPublisher.PublishAsync(
              new TrackCleanupCompleted(@event.TrackId, @event.ObjectKey));

          _metrics.IncrementCleanupCounter("deleted");
      }
  }
  ```

- [ ] **4.5.2** Create orphan detection job:
  ```csharp
  public class OrphanDetectionJob : IScheduledJob
  {
      public async Task ExecuteAsync(CancellationToken ct)
      {
          var objects = await _storage.ListObjectsAsync("novatune-audio", ct);
          var orphans = new List<string>();

          foreach (var obj in objects)
          {
              var trackExists = await _tracks.ExistsByObjectKeyAsync(obj.Key, ct);
              if (!trackExists)
              {
                  var age = _timeProvider.GetUtcNow() - obj.LastModified;
                  if (age > TimeSpan.FromDays(7))
                  {
                      orphans.Add(obj.Key);
                  }
              }
          }

          foreach (var orphan in orphans)
          {
              _logger.LogWarning("Deleting orphan object: {ObjectKey}", orphan);
              await _storage.DeleteAsync(orphan, ct);
              _metrics.IncrementCleanupCounter("skipped");
          }
      }
  }
  ```

- [ ] **4.5.3** Configure Kafka consumer:
  - Topic: `track-deletions` (compacted)
  - Consumer group: `lifecycle-processor`

- [ ] **4.5.4** Schedule orphan detection:
  - Run daily at 3 AM UTC
  - 7-day grace period before deletion

- [ ] **4.5.5** Add cleanup metrics:
  ```csharp
  novatune_cleanup_objects_total{action="deleted|skipped"}
  novatune_cleanup_duration_seconds
  novatune_orphan_detected_total
  ```

- [ ] **4.5.6** Add audit logging for deletions

- [ ] **4.5.7** Write integration tests

#### Acceptance Criteria
- Deletions processed after 24-hour grace
- Orphans detected and cleaned after 7 days
- Metrics published for all actions
- Audit trail maintained

---

### Task 4.6: Bucket Security Configuration

**Priority:** P1 (Must-have)

Configure MinIO buckets with proper security settings.

#### Subtasks

- [ ] **4.6.1** Configure bucket policies:
  ```json
  {
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Deny",
        "Principal": "*",
        "Action": "s3:*",
        "Resource": [
          "arn:aws:s3:::novatune-*-audio/*"
        ],
        "Condition": {
          "Bool": {
            "aws:SecureTransport": "false"
          }
        }
      }
    ]
  }
  ```

- [ ] **4.6.2** Enable server-side encryption:
  ```csharp
  await _client.SetBucketEncryptionAsync(
      new SetBucketEncryptionArgs()
          .WithBucket(bucketName)
          .WithEncryptionConfig(new ServerSideEncryptionConfiguration
          {
              Rules = new List<ServerSideEncryptionRule>
              {
                  new()
                  {
                      Apply = new ServerSideEncryptionByDefault
                      {
                          SSEAlgorithm = ServerSideEncryptionAlgorithm.AES256
                      }
                  }
              }
          }));
  ```

- [ ] **4.6.3** Configure lifecycle rules:
  ```csharp
  // Keep last 3 versions, expire older after 7 days
  var lifecycleConfig = new LifecycleConfiguration
  {
      Rules = new List<LifecycleRule>
      {
          new()
          {
              Id = "version-cleanup",
              Status = "Enabled",
              NoncurrentVersionExpiration = new NoncurrentVersionExpiration
              {
                  NoncurrentDays = 7,
                  NewerNoncurrentVersions = 3
              }
          }
      }
  };
  ```

- [ ] **4.6.4** Block public access

- [ ] **4.6.5** Configure bucket versioning

- [ ] **4.6.6** Write infrastructure tests

#### Acceptance Criteria
- Buckets private by default
- SSE-S3 encryption enabled
- Lifecycle rules active
- Versioning enabled
- No public access possible

---

## Infrastructure Setup

- [ ] NCache cluster configuration
- [ ] NCache regions: `presigned-urls`, `access-tokens`
- [ ] Kafka topic: `track-deletions` (compacted)
- [ ] Background worker service for lifecycle jobs
- [ ] MinIO bucket policies and lifecycle rules

---

## Testing Requirements

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

---

## Exit Criteria

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

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Cache stampede | Medium | Request coalescing, jittered TTL |
| NCache cluster failure | High | Graceful degradation to direct MinIO |
| Orphan false positives | High | 7-day grace period, audit logging |

---

## Navigation

← [Phase 3: Audio Upload](phase-3-audio-upload.md) | [Overview](../overview.md) | [Phase 5: Audio Streaming →](phase-5-audio-streaming.md)
