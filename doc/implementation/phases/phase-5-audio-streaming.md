# Phase 5: Audio Streaming (FR 5.x)

> **Status:** ⏳ Pending
> **Dependencies:** Phase 1, Phase 2, Phase 4 (presigned URLs)
> **Milestone:** M3 - Playback

## Objective

Deliver seamless audio playback through a secure streaming gateway with range request support and automatic URL refresh handling.

---

## FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 5.1 | Playback Gateway | P1 | E2E |
| FR 5.2 | Signed Streaming URLs | P1 | Test |
| FR 5.3 | Player Controls | P1 | E2E |
| FR 5.4 | Expiry Handling | P2 | Test |

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-1.1 | Throughput | 200 concurrent streams per node |
| NF-1.5 | API Gateway Performance | <10ms YARP overhead |
| NF-7.1 | Responsiveness | Keyboard accessible controls |
| NF-7.2 | Feedback | Loading indicators, error messages |
| NF-7.4 | Frontend Performance | <2.5s LCP |

---

## Tasks

### Task 5.1: Streaming Gateway Endpoint

**Priority:** P1 (Must-have)

Create the main streaming endpoint for audio playback.

#### Subtasks

- [ ] **5.1.1** Create `GET /api/v1/tracks/{id}/stream`:
  ```csharp
  app.MapGet("/api/v1/tracks/{id}/stream", async (
      string id,
      HttpContext context,
      IStreamingService streamingService,
      CancellationToken ct) =>
  {
      var userId = context.User.GetUserId();

      var result = await streamingService.GetStreamAsync(id, userId, ct);

      return result.Match(
          stream => Results.Stream(
              stream.Content,
              stream.ContentType,
              enableRangeProcessing: true),
          error => error switch
          {
              NotFoundError => Results.NotFound(),
              ForbiddenError => Results.Forbid(),
              ExpiredUrlError => Results.StatusCode(410), // Gone
              _ => Results.Problem()
          });
  })
  .RequireAuthorization()
  .WithName("StreamTrack")
  .WithOpenApi(op =>
  {
      op.Summary = "Stream audio track";
      op.Description = "Returns audio stream with range request support";
      return op;
  });
  ```

- [ ] **5.1.2** Implement `IStreamingService`:
  ```csharp
  public interface IStreamingService
  {
      Task<Result<AudioStream, StreamError>> GetStreamAsync(
          string trackId,
          string userId,
          CancellationToken ct);

      Task<Result<string, StreamError>> GetStreamUrlAsync(
          string trackId,
          string userId,
          CancellationToken ct);
  }

  public record AudioStream(
      Stream Content,
      string ContentType,
      long ContentLength,
      string? ContentRange);
  ```

- [ ] **5.1.3** Implement streaming service:
  ```csharp
  public sealed class StreamingService : IStreamingService
  {
      public async Task<Result<AudioStream, StreamError>> GetStreamAsync(
          string trackId,
          string userId,
          CancellationToken ct)
      {
          // 1. Check access
          var access = await _accessControl.CheckAccessAsync(
              userId, trackId, ResourceType.Track, Permission.Read, ct);

          if (!access.Allowed)
              return access.DenialReason == "Resource not found"
                  ? new NotFoundError()
                  : new ForbiddenError();

          // 2. Get presigned URL (from cache or generate)
          var urlResult = await _presignedUrls.GenerateAsync(trackId, userId, ct: ct);

          // 3. Get track metadata for content type
          var track = await _tracks.GetByIdAsync(trackId, ct);

          // 4. Create proxied stream
          var response = await _httpClient.GetAsync(
              urlResult.Url,
              HttpCompletionOption.ResponseHeadersRead,
              ct);

          if (!response.IsSuccessStatusCode)
          {
              if (response.StatusCode == HttpStatusCode.Forbidden)
              {
                  // URL expired, refresh and retry
                  return await HandleExpiredUrlAsync(trackId, userId, ct);
              }
              return new StreamError(response.StatusCode.ToString());
          }

          return new AudioStream(
              await response.Content.ReadAsStreamAsync(ct),
              track!.Metadata!.MimeType!,
              response.Content.Headers.ContentLength ?? 0,
              null);
      }
  }
  ```

- [ ] **5.1.4** Add streaming metrics:
  ```csharp
  novatune_stream_active_total{userId_hash="..."}
  novatune_stream_bytes_total
  novatune_stream_duration_seconds
  novatune_stream_errors_total{reason="..."}
  ```

- [ ] **5.1.5** Write integration tests

#### Acceptance Criteria
- Stream endpoint returns audio content
- Proper content type headers set
- Access control enforced
- Metrics emitted

---

### Task 5.2: HTTP Range Request Support

**Priority:** P1 (Must-have)

Implement range request handling for seeking.

#### Subtasks

- [ ] **5.2.1** Configure range processing:
  ```csharp
  app.MapGet("/api/v1/tracks/{id}/stream", async (
      string id,
      HttpContext context,
      IStreamingService streamingService,
      CancellationToken ct) =>
  {
      var rangeHeader = context.Request.Headers.Range;
      var ifRange = context.Request.Headers.IfRange;

      // Parse range header
      var range = RangeHeaderValue.TryParse(rangeHeader, out var parsedRange)
          ? parsedRange.Ranges.FirstOrDefault()
          : null;

      var stream = await streamingService.GetRangeStreamAsync(
          id, range, context.User.GetUserId(), ct);

      // Set response headers
      context.Response.Headers.AcceptRanges = "bytes";

      if (range is not null)
      {
          context.Response.StatusCode = StatusCodes.Status206PartialContent;
          context.Response.Headers.ContentRange =
              $"bytes {stream.Start}-{stream.End}/{stream.TotalLength}";
      }

      context.Response.ContentType = stream.ContentType;
      context.Response.ContentLength = stream.Length;

      await stream.Content.CopyToAsync(context.Response.Body, ct);
  });
  ```

- [ ] **5.2.2** Implement range parsing:
  ```csharp
  public record RangeRequest(long? Start, long? End);

  public static RangeRequest? ParseRange(string? rangeHeader, long totalLength)
  {
      if (string.IsNullOrEmpty(rangeHeader))
          return null;

      // Parse "bytes=start-end" format
      var match = Regex.Match(rangeHeader, @"bytes=(\d*)-(\d*)");
      if (!match.Success)
          return null;

      var start = string.IsNullOrEmpty(match.Groups[1].Value)
          ? null
          : (long?)long.Parse(match.Groups[1].Value);

      var end = string.IsNullOrEmpty(match.Groups[2].Value)
          ? null
          : (long?)long.Parse(match.Groups[2].Value);

      // Handle suffix-length (bytes=-500)
      if (start is null && end is not null)
      {
          start = totalLength - end;
          end = totalLength - 1;
      }

      return new RangeRequest(start, end);
  }
  ```

- [ ] **5.2.3** Handle edge cases:
  - Unsatisfiable range (416)
  - Multiple ranges (not supported, return 200)
  - If-Range header validation

- [ ] **5.2.4** Set correct response headers:
  ```csharp
  Accept-Ranges: bytes
  Content-Range: bytes 0-1023/10240
  Content-Length: 1024
  ```

- [ ] **5.2.5** Write range request tests:
  ```csharp
  [Theory]
  [InlineData("bytes=0-499", 0, 499, 500)]
  [InlineData("bytes=500-999", 500, 999, 500)]
  [InlineData("bytes=-500", 9500, 9999, 500)] // Suffix
  [InlineData("bytes=9500-", 9500, 9999, 500)] // Open-ended
  public async Task RangeRequest_ReturnsPartialContent(
      string rangeHeader,
      long expectedStart,
      long expectedEnd,
      long expectedLength)
  {
      // Test implementation
  }
  ```

#### Acceptance Criteria
- Range requests return 206
- Seeking works correctly
- Content-Range header accurate
- Unsatisfiable ranges return 416

---

### Task 5.3: YARP Reverse Proxy Configuration

**Priority:** P1 (Must-have)

Configure YARP for transparent MinIO proxying.

#### Subtasks

- [ ] **5.3.1** Add YARP configuration:
  ```csharp
  builder.Services.AddReverseProxy()
      .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

  // appsettings.json
  {
    "ReverseProxy": {
      "Routes": {
        "audio-stream": {
          "ClusterId": "minio",
          "Match": {
            "Path": "/stream/{**catch-all}"
          },
          "Transforms": [
            { "PathRemovePrefix": "/stream" },
            { "RequestHeader": "Host", "Set": "minio.internal" }
          ]
        }
      },
      "Clusters": {
        "minio": {
          "Destinations": {
            "primary": {
              "Address": "http://minio:9000"
            }
          },
          "HttpClient": {
            "RequestHeaderEncoding": "Latin1"
          }
        }
      }
    }
  }
  ```

- [ ] **5.3.2** Create custom transform for presigned URLs:
  ```csharp
  public class PresignedUrlTransform : RequestTransform
  {
      public override async ValueTask ApplyAsync(RequestTransformContext context)
      {
          var trackId = context.HttpContext.Request.RouteValues["id"]?.ToString();
          var userId = context.HttpContext.User.GetUserId();

          var presignedUrl = await _presignedUrls.GenerateAsync(trackId!, userId!);

          // Replace destination with presigned URL
          var uri = new Uri(presignedUrl.Url);
          context.ProxyRequest.RequestUri = uri;
      }
  }
  ```

- [ ] **5.3.3** Configure rate limiting:
  ```csharp
  builder.Services.AddRateLimiter(options =>
  {
      options.AddPolicy("streaming", context =>
          RateLimitPartition.GetSlidingWindowLimiter(
              partitionKey: context.User.GetUserId(),
              factory: _ => new SlidingWindowRateLimiterOptions
              {
                  PermitLimit = 100,
                  Window = TimeSpan.FromMinutes(1),
                  SegmentsPerWindow = 6
              }));
  });

  app.MapGet("/api/v1/tracks/{id}/stream", ...)
      .RequireRateLimiting("streaming");
  ```

- [ ] **5.3.4** Configure CORS for streaming:
  ```csharp
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("Streaming", policy =>
      {
          policy.WithOrigins(
                  "https://app.novatune.local",
                  "https://novatune.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders(
                  "Content-Range",
                  "Accept-Ranges",
                  "Content-Length");
      });
  });
  ```

- [ ] **5.3.5** Add request/response logging

- [ ] **5.3.6** Measure YARP overhead:
  ```csharp
  novatune_yarp_latency_seconds{route="audio-stream"}
  ```

- [ ] **5.3.7** Write integration tests

#### Acceptance Criteria
- YARP proxies transparently
- Client doesn't see MinIO URLs
- Rate limiting works (100 req/min)
- CORS configured correctly
- Overhead <10ms p95

---

### Task 5.4: URL Expiry Handling

**Priority:** P2 (Should-have)

Handle expired presigned URLs gracefully.

#### Subtasks

- [ ] **5.4.1** Detect 403 from MinIO:
  ```csharp
  public async Task<AudioStream> GetStreamWithRetryAsync(
      string trackId,
      string userId,
      int maxRetries = 3,
      CancellationToken ct = default)
  {
      for (var attempt = 1; attempt <= maxRetries; attempt++)
      {
          var urlResult = await _presignedUrls.GenerateAsync(
              trackId, userId,
              new PresignedUrlOptions(ForceRefresh: attempt > 1),
              ct);

          try
          {
              var response = await _httpClient.GetAsync(
                  urlResult.Url,
                  HttpCompletionOption.ResponseHeadersRead,
                  ct);

              if (response.StatusCode == HttpStatusCode.Forbidden)
              {
                  _logger.LogWarning(
                      "Presigned URL expired. Track={TrackId}, Attempt={Attempt}",
                      trackId, attempt);

                  // Invalidate cache
                  await _presignedUrls.InvalidateAsync(trackId, ct);
                  continue;
              }

              response.EnsureSuccessStatusCode();
              return CreateAudioStream(response);
          }
          catch (HttpRequestException) when (attempt < maxRetries)
          {
              await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
          }
      }

      throw new StreamingException("Failed to obtain valid stream URL");
  }
  ```

- [ ] **5.4.2** Return proper status codes:
  - 410 Gone - Permanent expiry
  - 503 Service Unavailable - Temporary issue
  - Retry-After header

- [ ] **5.4.3** Add client-side retry hints:
  ```csharp
  context.Response.Headers["X-Retry-Url"] = "/api/v1/tracks/{id}/stream";
  context.Response.Headers["Retry-After"] = "1";
  ```

- [ ] **5.4.4** Add expiry metrics:
  ```csharp
  novatune_stream_url_expired_total
  novatune_stream_retry_total{attempt="1|2|3"}
  ```

- [ ] **5.4.5** Write tests for expiry scenarios

#### Acceptance Criteria
- Expired URLs detected and refreshed
- Max 3 retry attempts
- Proper error codes returned
- Metrics track expiry events

---

### Task 5.5: Player Control API

**Priority:** P2 (Should-have)

Provide API support for player state management.

#### Subtasks

- [ ] **5.5.1** Create player state endpoints:
  ```csharp
  // Save playback position (for resume)
  app.MapPost("/api/v1/tracks/{id}/position", async (
      string id,
      SavePositionRequest request,
      IPlayerStateService playerState,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      await playerState.SavePositionAsync(
          user.GetUserId(),
          id,
          request.PositionMs,
          ct);

      return Results.NoContent();
  }).RequireAuthorization();

  // Get last position
  app.MapGet("/api/v1/tracks/{id}/position", async (
      string id,
      IPlayerStateService playerState,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var position = await playerState.GetPositionAsync(
          user.GetUserId(), id, ct);

      return Results.Ok(new { positionMs = position });
  }).RequireAuthorization();
  ```

- [ ] **5.5.2** Implement position storage (NCache):
  ```csharp
  public interface IPlayerStateService
  {
      Task SavePositionAsync(string userId, string trackId, long positionMs, CancellationToken ct);
      Task<long?> GetPositionAsync(string userId, string trackId, CancellationToken ct);
      Task ClearPositionAsync(string userId, string trackId, CancellationToken ct);
  }

  public sealed class PlayerStateService : IPlayerStateService
  {
      public async Task SavePositionAsync(
          string userId, string trackId, long positionMs, CancellationToken ct)
      {
          var key = $"player:{userId}:{trackId}";
          await _cache.SetAsync(key, positionMs, TimeSpan.FromDays(30), ct);
      }
  }
  ```

- [ ] **5.5.3** Track playback events (for analytics):
  ```csharp
  public record PlaybackEvent(
      string TrackId,
      string UserId,
      PlaybackAction Action,
      long PositionMs,
      DateTimeOffset Timestamp);

  public enum PlaybackAction { Start, Pause, Resume, Seek, Complete }
  ```

- [ ] **5.5.4** Publish events to Kafka:
  ```csharp
  await _eventPublisher.PublishAsync(new PlaybackEvent(
      trackId,
      userId,
      PlaybackAction.Start,
      0,
      _timeProvider.GetUtcNow()));
  ```

- [ ] **5.5.5** Write tests for player state

#### Acceptance Criteria
- Position saved and retrieved correctly
- Playback events published to Kafka
- Position persists for 30 days

---

### Task 5.6: Streaming Observability

**Priority:** P2 (Should-have)

Add comprehensive monitoring for streaming operations.

#### Subtasks

- [ ] **5.6.1** Track active streams:
  ```csharp
  public class StreamingMetrics
  {
      private readonly Counter<long> _streamsTotal;
      private readonly UpDownCounter<int> _activeStreams;
      private readonly Histogram<double> _streamDuration;
      private readonly Counter<long> _bytesStreamed;

      public void RecordStreamStart(string userId)
      {
          _streamsTotal.Add(1, new KeyValuePair<string, object?>("user_hash", userId.Hash()));
          _activeStreams.Add(1);
      }

      public void RecordStreamEnd(string userId, TimeSpan duration, long bytes)
      {
          _activeStreams.Add(-1);
          _streamDuration.Record(duration.TotalSeconds);
          _bytesStreamed.Add(bytes);
      }
  }
  ```

- [ ] **5.6.2** Add bandwidth tracking

- [ ] **5.6.3** Create streaming dashboard in Aspire

- [ ] **5.6.4** Add distributed tracing spans

- [ ] **5.6.5** Set up alerts for streaming issues

#### Acceptance Criteria
- Active stream count tracked
- Bandwidth metrics available
- Tracing spans for full request path

---

## Infrastructure Setup

- [ ] YARP configuration in Aspire
- [ ] CORS policy for frontend domains
- [ ] Rate limiting middleware
- [ ] Streaming metrics dashboard

---

## Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | URL expiry detection | 403 handling |
| Unit | Range header parsing | All formats |
| Integration | Range requests | Seek, partial content |
| Integration | YARP routing | Transparent proxy |
| E2E | Full playback | Start to finish |
| E2E | Seek operations | Multiple positions |
| Load | Concurrent streams | 200 simultaneous |

---

## Exit Criteria

- [ ] Audio streams successfully via gateway
- [ ] Range requests enable seeking
- [ ] Expired URLs automatically refreshed
- [ ] CORS allows frontend playback
- [ ] YARP overhead <10ms p95
- [ ] 200 concurrent streams sustained
- [ ] Player state persisted correctly

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| High bandwidth costs | Medium | CDN integration (future) |
| YARP configuration complexity | Medium | Extensive integration tests |
| Client-side seek bugs | Low | Comprehensive E2E tests |

---

## Navigation

← [Phase 4: Storage & Access Control](phase-4-storage-access.md) | [Overview](../overview.md) | [Phase 6: Track Management →](phase-6-track-management.md)
