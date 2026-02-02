# Stage 7 — Telemetry Ingestion + Analytics Aggregation

**Goal:** Capture client-reported playback telemetry, aggregate analytics, and store short-retention metrics for Admin dashboards.

## Overview

```
┌─────────┐  POST /telemetry/playback    ┌─────────────┐
│ Client  │ ────────────────────────────►│ API Service │
└────┬────┘ ◄─────────────────────────── └──────┬──────┘
     │       202 Accepted                       │
     │                                          │ Validate + Rate Limit
     │                                          │ (Req 8.2, NF-2.5)
     │                                          ▼
     │                                   ┌─────────────────┐
     │                                   │   Redpanda      │
     │                                   │ (telemetry-     │
     │                                   │  events)        │
     │                                   └────────┬────────┘
     │                                            │
     │                                            ▼
     │                                   ┌─────────────────┐
     │                                   │ Telemetry       │
     │                                   │ Worker          │
     │                                   │                 │
     │                                   │ • Consume events│
     │                                   │ • Aggregate     │
     │                                   │ • Store metrics │
     │                                   └────────┬────────┘
     │                                            │
     │                                            ▼
     │                                   ┌─────────────────┐
     │                                   │   RavenDB       │
     │                                   │                 │
     │                                   │ ┌─────────────┐ │
     │                                   │ │ Analytics   │ │
     │                                   │ │ Aggregates  │ │
     │                                   │ └─────────────┘ │
     │                                   └────────┬────────┘
     │                                            │
┌────┴────┐  GET /admin/analytics                 │
│  Admin  │ ─────────────────────────────────────►│
└─────────┘ ◄─────────────────────────────────────┘
            Dashboard data (Req 11.3)
```

---

## 1. Data Models

### Playback Event (Inbound)

```csharp
namespace NovaTuneApp.ApiService.Models.Telemetry;

/// <summary>
/// Client-reported playback event (Req 5.4).
/// </summary>
public sealed class PlaybackEventRequest
{
    /// <summary>
    /// Event type: "play_start", "play_stop", "play_progress", "play_complete".
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Track being played (ULID).
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string TrackId { get; init; } = string.Empty;

    /// <summary>
    /// Client-reported timestamp (ISO 8601).
    /// </summary>
    [Required]
    public DateTimeOffset ClientTimestamp { get; init; }

    /// <summary>
    /// Current playback position in seconds.
    /// </summary>
    public double? PositionSeconds { get; init; }

    /// <summary>
    /// Total duration played in this session (seconds).
    /// </summary>
    public double? DurationPlayedSeconds { get; init; }

    /// <summary>
    /// Unique session identifier for grouping events.
    /// </summary>
    [MaxLength(64)]
    public string? SessionId { get; init; }

    /// <summary>
    /// Client device identifier (hashed).
    /// </summary>
    [MaxLength(64)]
    public string? DeviceId { get; init; }

    /// <summary>
    /// Client application version.
    /// </summary>
    [MaxLength(32)]
    public string? ClientVersion { get; init; }
}

/// <summary>
/// Playback event types.
/// </summary>
public static class PlaybackEventTypes
{
    public const string PlayStart = "play_start";
    public const string PlayStop = "play_stop";
    public const string PlayProgress = "play_progress";
    public const string PlayComplete = "play_complete";
    public const string Seek = "seek";

    public static readonly HashSet<string> Valid = new(StringComparer.OrdinalIgnoreCase)
    {
        PlayStart, PlayStop, PlayProgress, PlayComplete, Seek
    };
}
```

### Telemetry Event (Kafka Message)

```csharp
namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

/// <summary>
/// Internal telemetry event for Kafka/Redpanda pipeline (Req 9.1, 9.4).
/// </summary>
public record TelemetryEvent
{
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Event type from client.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Track ID (ULID) - used as partition key (Req 9.5).
    /// </summary>
    public required string TrackId { get; init; }

    /// <summary>
    /// User who triggered the event.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Client-reported timestamp.
    /// </summary>
    public required DateTimeOffset ClientTimestamp { get; init; }

    /// <summary>
    /// Server-received timestamp.
    /// </summary>
    public required DateTimeOffset ServerTimestamp { get; init; }

    /// <summary>
    /// Playback position in seconds.
    /// </summary>
    public double? PositionSeconds { get; init; }

    /// <summary>
    /// Duration played in seconds.
    /// </summary>
    public double? DurationPlayedSeconds { get; init; }

    /// <summary>
    /// Session identifier for grouping.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Hashed device identifier.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// Client version.
    /// </summary>
    public string? ClientVersion { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing (Req 9.3).
    /// </summary>
    public required string CorrelationId { get; init; }
}
```

### Analytics Aggregates (RavenDB)

```csharp
namespace NovaTuneApp.ApiService.Models.Analytics;

/// <summary>
/// Hourly play count aggregate per track (Req 9.2).
/// </summary>
public sealed class TrackHourlyAggregate
{
    /// <summary>
    /// RavenDB document ID: "TrackHourlyAggregates/{trackId}/{hourBucket}".
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Track ID (ULID).
    /// </summary>
    public string TrackId { get; init; } = string.Empty;

    /// <summary>
    /// Track owner user ID.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Hour bucket (UTC, truncated to hour).
    /// </summary>
    public DateTimeOffset HourBucket { get; init; }

    /// <summary>
    /// Number of play_start events.
    /// </summary>
    public int PlayStartCount { get; set; }

    /// <summary>
    /// Number of play_complete events.
    /// </summary>
    public int PlayCompleteCount { get; set; }

    /// <summary>
    /// Total seconds played across all sessions.
    /// </summary>
    public double TotalSecondsPlayed { get; set; }

    /// <summary>
    /// Unique sessions that played this track.
    /// </summary>
    public int UniqueSessionCount { get; set; }

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Expiration for automatic cleanup (NF-6.3).
    /// RavenDB document expiration.
    /// </summary>
    [JsonPropertyName("@expires")]
    public DateTimeOffset? Expires { get; set; }
}

/// <summary>
/// Daily aggregate for dashboard queries (Req 11.3).
/// </summary>
public sealed class TrackDailyAggregate
{
    public string Id { get; init; } = string.Empty;
    public string TrackId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateOnly DateBucket { get; init; }
    public int TotalPlays { get; set; }
    public int CompletedPlays { get; set; }
    public double TotalSecondsPlayed { get; set; }
    public int UniqueListeners { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("@expires")]
    public DateTimeOffset? Expires { get; set; }
}

/// <summary>
/// User activity summary for admin dashboards.
/// </summary>
public sealed class UserActivityAggregate
{
    public string Id { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateOnly DateBucket { get; init; }
    public int TracksPlayed { get; set; }
    public int TotalPlays { get; set; }
    public double TotalSecondsPlayed { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("@expires")]
    public DateTimeOffset? Expires { get; set; }
}
```

---

## 2. API Endpoint: `POST /telemetry/playback`

### Request

- **Method:** `POST`
- **Path:** `/telemetry/playback`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role

### Request Schema

```json
{
  "eventType": "play_start",
  "trackId": "01HXK...",
  "clientTimestamp": "2025-01-08T10:00:00Z",
  "positionSeconds": 0.0,
  "durationPlayedSeconds": null,
  "sessionId": "sess_abc123",
  "deviceId": "dev_xyz789",
  "clientVersion": "1.2.0"
}
```

### Response (Success: 202 Accepted)

```json
{
  "accepted": true,
  "correlationId": "01HXK..."
}
```

The `202 Accepted` response indicates the event was queued for processing. Analytics are eventually consistent.

### Validation Rules (Req 5.4)

| Field | Rule | Error Code |
|-------|------|------------|
| `eventType` | Must be one of: `play_start`, `play_stop`, `play_progress`, `play_complete`, `seek` | `INVALID_EVENT_TYPE` |
| `trackId` | Must be valid ULID | `INVALID_TRACK_ID` |
| `clientTimestamp` | Must not be more than 24 hours in the past or 5 minutes in the future | `INVALID_TIMESTAMP` |
| `positionSeconds` | Must be >= 0 if provided | `INVALID_POSITION` |
| `durationPlayedSeconds` | Must be >= 0 if provided | `INVALID_DURATION` |
| `sessionId` | Max 64 characters | `INVALID_SESSION_ID` |
| `deviceId` | Max 64 characters | `INVALID_DEVICE_ID` |

### Track Ownership Validation

The API validates that the authenticated user owns or has access to the track:

```csharp
// Light validation - only check track exists and user has access
// Do NOT block on full track load for telemetry (performance)
var trackExists = await _trackAccessValidator.HasAccessAsync(
    request.TrackId,
    userId,
    ct);

if (!trackExists)
{
    _logger.LogWarning(
        "Telemetry rejected: user {UserId} lacks access to track {TrackId}",
        userId, request.TrackId);
    return Results.Problem(
        title: "Track not accessible",
        statusCode: StatusCodes.Status403Forbidden,
        type: "https://novatune.dev/errors/track-access-denied");
}
```

### Rate Limiting (Req 8.2, NF-2.5)

- Policy: `telemetry-ingest`
- Default: 120 requests/minute per device (DeviceId)
- Fallback: 60 requests/minute per user if no DeviceId
- Response on limit: `429 Too Many Requests` with `Retry-After` header
- Server-side sampling: If under heavy load, randomly drop 10-50% of `play_progress` events (non-critical)

```csharp
// Rate limit configuration
services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("telemetry-ingest", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.SegmentsPerWindow = 6;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 10;
    });
});
```

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `validation-error` | Invalid event data |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `track-access-denied` | User does not have access to track |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |
| `503` | `service-unavailable` | Message queue unavailable |

---

## 3. Batch Telemetry Endpoint (Optional)

### `POST /telemetry/playback/batch`

For clients that buffer events offline:

### Request Schema

```json
{
  "events": [
    {
      "eventType": "play_start",
      "trackId": "01HXK...",
      "clientTimestamp": "2025-01-08T10:00:00Z",
      "positionSeconds": 0.0
    },
    {
      "eventType": "play_stop",
      "trackId": "01HXK...",
      "clientTimestamp": "2025-01-08T10:03:42Z",
      "durationPlayedSeconds": 222.5
    }
  ]
}
```

### Validation Rules

| Rule | Limit |
|------|-------|
| Max events per batch | 50 |
| Max age of oldest event | 7 days |
| All tracks must be accessible | Yes |

### Response (Success: 202 Accepted)

```json
{
  "accepted": 2,
  "rejected": 0,
  "correlationId": "01HXK..."
}
```

---

## 4. Event Pipeline

### Topic Configuration (Req 9.1)

```
Topic: {env}-telemetry-events
Partitions: 12 (based on expected track volume)
Retention: 7 days (for replay capability, NF-6.5)
Partition Key: TrackId (Req 9.5)
```

### Message Publishing

```csharp
namespace NovaTuneApp.ApiService.Services;

public class TelemetryIngestionService : ITelemetryIngestionService
{
    private readonly IMessageProducer<TelemetryEvent> _producer;
    private readonly ITrackAccessValidator _trackValidator;
    private readonly IOptions<TelemetryOptions> _options;
    private readonly ILogger<TelemetryIngestionService> _logger;
    private readonly TelemetryMetrics _metrics;

    public async Task<TelemetryIngestionResult> IngestAsync(
        PlaybackEventRequest request,
        string userId,
        string correlationId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Validate timestamp bounds
        var maxPast = now.AddHours(-_options.Value.MaxEventAgeHours);
        var maxFuture = now.AddMinutes(_options.Value.MaxFutureMinutes);

        if (request.ClientTimestamp < maxPast || request.ClientTimestamp > maxFuture)
        {
            _metrics.RecordRejection("invalid_timestamp");
            return TelemetryIngestionResult.Rejected("Invalid timestamp");
        }

        // Validate track access (lightweight check)
        if (!await _trackValidator.HasAccessAsync(request.TrackId, userId, ct))
        {
            _metrics.RecordRejection("access_denied");
            return TelemetryIngestionResult.AccessDenied();
        }

        // Apply server-side sampling for play_progress under load
        if (request.EventType == PlaybackEventTypes.PlayProgress &&
            _metrics.ShouldSample())
        {
            _metrics.RecordSampled();
            return TelemetryIngestionResult.Sampled();
        }

        var evt = new TelemetryEvent
        {
            EventType = request.EventType,
            TrackId = request.TrackId,
            UserId = userId,
            ClientTimestamp = request.ClientTimestamp,
            ServerTimestamp = now,
            PositionSeconds = request.PositionSeconds,
            DurationPlayedSeconds = request.DurationPlayedSeconds,
            SessionId = request.SessionId,
            DeviceId = request.DeviceId,
            ClientVersion = request.ClientVersion,
            CorrelationId = correlationId
        };

        await _producer.ProduceAsync(
            request.TrackId,  // Partition key (Req 9.5)
            evt,
            ct);

        _metrics.RecordIngested(request.EventType);

        _logger.LogDebug(
            "Telemetry event {EventType} ingested for track {TrackId}",
            request.EventType, request.TrackId);

        return TelemetryIngestionResult.Accepted(correlationId);
    }
}
```

---

## 5. Telemetry Aggregation Worker

### Project: `NovaTuneApp.Workers.Telemetry`

Separate deployment per NF-1.1. Consumes telemetry events and maintains aggregates.

### Event Handler: `TelemetryEventHandler`

```csharp
namespace NovaTuneApp.Workers.Telemetry.Handlers;

public class TelemetryEventHandler : IMessageHandler<TelemetryEvent>
{
    private readonly IAggregationService _aggregationService;
    private readonly ILogger<TelemetryEventHandler> _logger;
    private readonly TelemetryWorkerMetrics _metrics;

    public async Task Handle(IMessageContext context, TelemetryEvent message)
    {
        using var activity = TelemetryActivitySource.StartActivity(
            "telemetry.aggregate",
            ActivityKind.Consumer);

        activity?.SetTag("event.type", message.EventType);
        activity?.SetTag("track.id", message.TrackId);

        try
        {
            await _aggregationService.ProcessEventAsync(message, context.ConsumerContext.WorkerStopped);

            _metrics.RecordProcessed(message.EventType);

            _logger.LogDebug(
                "Processed telemetry event {EventType} for track {TrackId}",
                message.EventType, message.TrackId);
        }
        catch (Exception ex)
        {
            _metrics.RecordFailed(message.EventType);
            _logger.LogError(ex,
                "Failed to process telemetry event {EventType} for track {TrackId}",
                message.EventType, message.TrackId);
            throw; // Let KafkaFlow retry
        }
    }
}
```

### Aggregation Service

```csharp
namespace NovaTuneApp.Workers.Telemetry.Services;

public class AggregationService : IAggregationService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IOptions<TelemetryOptions> _options;
    private readonly ILogger<AggregationService> _logger;

    public async Task ProcessEventAsync(
        TelemetryEvent evt,
        CancellationToken ct = default)
    {
        var hourBucket = TruncateToHour(evt.ServerTimestamp);
        var dayBucket = DateOnly.FromDateTime(evt.ServerTimestamp.UtcDateTime);

        // Update hourly aggregate
        await UpdateHourlyAggregateAsync(evt, hourBucket, ct);

        // Update daily aggregate (for dashboard queries)
        await UpdateDailyAggregateAsync(evt, dayBucket, ct);

        // Update user activity aggregate
        await UpdateUserActivityAsync(evt, dayBucket, ct);

        await _session.SaveChangesAsync(ct);
    }

    private async Task UpdateHourlyAggregateAsync(
        TelemetryEvent evt,
        DateTimeOffset hourBucket,
        CancellationToken ct)
    {
        var docId = $"TrackHourlyAggregates/{evt.TrackId}/{hourBucket:yyyyMMddHH}";
        var aggregate = await _session.LoadAsync<TrackHourlyAggregate>(docId, ct);

        if (aggregate is null)
        {
            aggregate = new TrackHourlyAggregate
            {
                Id = docId,
                TrackId = evt.TrackId,
                UserId = evt.UserId,
                HourBucket = hourBucket,
                Expires = hourBucket.AddDays(_options.Value.RetentionDays)
            };
            await _session.StoreAsync(aggregate, ct);
        }

        switch (evt.EventType)
        {
            case PlaybackEventTypes.PlayStart:
                aggregate.PlayStartCount++;
                break;
            case PlaybackEventTypes.PlayComplete:
                aggregate.PlayCompleteCount++;
                break;
        }

        if (evt.DurationPlayedSeconds.HasValue)
        {
            aggregate.TotalSecondsPlayed += evt.DurationPlayedSeconds.Value;
        }

        if (!string.IsNullOrEmpty(evt.SessionId))
        {
            // Track unique sessions (simplified; production may use HyperLogLog)
            aggregate.UniqueSessionCount++;
        }

        aggregate.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task UpdateDailyAggregateAsync(
        TelemetryEvent evt,
        DateOnly dayBucket,
        CancellationToken ct)
    {
        var docId = $"TrackDailyAggregates/{evt.TrackId}/{dayBucket:yyyyMMdd}";
        var aggregate = await _session.LoadAsync<TrackDailyAggregate>(docId, ct);

        if (aggregate is null)
        {
            aggregate = new TrackDailyAggregate
            {
                Id = docId,
                TrackId = evt.TrackId,
                UserId = evt.UserId,
                DateBucket = dayBucket,
                Expires = dayBucket.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    .AddDays(_options.Value.RetentionDays)
            };
            await _session.StoreAsync(aggregate, ct);
        }

        if (evt.EventType == PlaybackEventTypes.PlayStart)
            aggregate.TotalPlays++;

        if (evt.EventType == PlaybackEventTypes.PlayComplete)
            aggregate.CompletedPlays++;

        if (evt.DurationPlayedSeconds.HasValue)
            aggregate.TotalSecondsPlayed += evt.DurationPlayedSeconds.Value;

        aggregate.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task UpdateUserActivityAsync(
        TelemetryEvent evt,
        DateOnly dayBucket,
        CancellationToken ct)
    {
        var docId = $"UserActivityAggregates/{evt.UserId}/{dayBucket:yyyyMMdd}";
        var aggregate = await _session.LoadAsync<UserActivityAggregate>(docId, ct);

        if (aggregate is null)
        {
            aggregate = new UserActivityAggregate
            {
                Id = docId,
                UserId = evt.UserId,
                DateBucket = dayBucket,
                Expires = dayBucket.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    .AddDays(_options.Value.RetentionDays)
            };
            await _session.StoreAsync(aggregate, ct);
        }

        if (evt.EventType == PlaybackEventTypes.PlayStart)
        {
            aggregate.TotalPlays++;
            // TracksPlayed would require deduplication
        }

        if (evt.DurationPlayedSeconds.HasValue)
            aggregate.TotalSecondsPlayed += evt.DurationPlayedSeconds.Value;

        aggregate.LastActivityAt = evt.ServerTimestamp;
        aggregate.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset dt) =>
        new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Offset);
}
```

---

## 6. Telemetry Service Interface

```csharp
namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for telemetry ingestion (Req 5.4).
/// </summary>
public interface ITelemetryIngestionService
{
    /// <summary>
    /// Ingests a single playback event.
    /// </summary>
    Task<TelemetryIngestionResult> IngestAsync(
        PlaybackEventRequest request,
        string userId,
        string correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Ingests a batch of playback events.
    /// </summary>
    Task<TelemetryBatchResult> IngestBatchAsync(
        IReadOnlyList<PlaybackEventRequest> events,
        string userId,
        string correlationId,
        CancellationToken ct = default);
}

/// <summary>
/// Service for querying analytics aggregates (Req 9.2, 11.3).
/// </summary>
public interface IAnalyticsQueryService
{
    /// <summary>
    /// Gets play statistics for a track over a date range.
    /// </summary>
    Task<TrackAnalytics> GetTrackAnalyticsAsync(
        string trackId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    /// <summary>
    /// Gets top tracks by play count for admin dashboard.
    /// </summary>
    Task<IReadOnlyList<TrackPlaySummary>> GetTopTracksAsync(
        int count,
        DateOnly? since,
        CancellationToken ct = default);

    /// <summary>
    /// Gets user activity summary for admin dashboard.
    /// </summary>
    Task<UserActivitySummary> GetUserActivityAsync(
        string userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    /// <summary>
    /// Gets recent activity feed for admin dashboard.
    /// </summary>
    Task<IReadOnlyList<RecentActivityItem>> GetRecentActivityAsync(
        int count,
        CancellationToken ct = default);
}

public record TelemetryIngestionResult(
    bool Accepted,
    string? CorrelationId = null,
    string? RejectionReason = null)
{
    public static TelemetryIngestionResult Accepted(string correlationId) =>
        new(true, correlationId);
    public static TelemetryIngestionResult Rejected(string reason) =>
        new(false, RejectionReason: reason);
    public static TelemetryIngestionResult AccessDenied() =>
        new(false, RejectionReason: "access_denied");
    public static TelemetryIngestionResult Sampled() =>
        new(true, RejectionReason: "sampled");
}

public record TelemetryBatchResult(
    int AcceptedCount,
    int RejectedCount,
    string CorrelationId);

public record TrackAnalytics(
    string TrackId,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalPlays,
    int CompletedPlays,
    TimeSpan TotalListenTime,
    int UniqueListeners,
    IReadOnlyList<DailyPlayCount> DailyBreakdown);

public record DailyPlayCount(DateOnly Date, int Plays, int Completed);

public record TrackPlaySummary(
    string TrackId,
    string Title,
    string? Artist,
    string UserId,
    int PlayCount,
    TimeSpan TotalListenTime);

public record UserActivitySummary(
    string UserId,
    int TracksPlayed,
    int TotalPlays,
    TimeSpan TotalListenTime,
    DateTimeOffset LastActivityAt);

public record RecentActivityItem(
    string UserId,
    string TrackId,
    string EventType,
    DateTimeOffset Timestamp);
```

---

## 7. RavenDB Indexes

### Index: `TrackDailyAggregates_ByDateRange`

```csharp
public class TrackDailyAggregates_ByDateRange : AbstractIndexCreationTask<TrackDailyAggregate>
{
    public TrackDailyAggregates_ByDateRange()
    {
        Map = aggregates => from agg in aggregates
                            select new
                            {
                                agg.TrackId,
                                agg.UserId,
                                agg.DateBucket,
                                agg.TotalPlays,
                                agg.CompletedPlays,
                                agg.TotalSecondsPlayed
                            };
    }
}
```

### Index: `TrackDailyAggregates_TopTracks`

For admin dashboard "top tracks" query:

```csharp
public class TrackDailyAggregates_TopTracks : AbstractIndexCreationTask<TrackDailyAggregate, TrackDailyAggregates_TopTracks.Result>
{
    public class Result
    {
        public string TrackId { get; set; } = string.Empty;
        public int TotalPlays { get; set; }
        public double TotalSecondsPlayed { get; set; }
    }

    public TrackDailyAggregates_TopTracks()
    {
        Map = aggregates => from agg in aggregates
                            select new Result
                            {
                                TrackId = agg.TrackId,
                                TotalPlays = agg.TotalPlays,
                                TotalSecondsPlayed = agg.TotalSecondsPlayed
                            };

        Reduce = results => from result in results
                            group result by result.TrackId into g
                            select new Result
                            {
                                TrackId = g.Key,
                                TotalPlays = g.Sum(x => x.TotalPlays),
                                TotalSecondsPlayed = g.Sum(x => x.TotalSecondsPlayed)
                            };
    }
}
```

### Index: `UserActivityAggregates_ByUser`

```csharp
public class UserActivityAggregates_ByUser : AbstractIndexCreationTask<UserActivityAggregate>
{
    public UserActivityAggregates_ByUser()
    {
        Map = aggregates => from agg in aggregates
                            select new
                            {
                                agg.UserId,
                                agg.DateBucket,
                                agg.TotalPlays,
                                agg.TotalSecondsPlayed,
                                agg.LastActivityAt
                            };
    }
}
```

---

## 8. Retention and Cleanup (NF-6.3)

### Automatic Document Expiration

RavenDB document expiration handles retention automatically:

```csharp
// In RavenDB configuration
store.Conventions.FindCollectionName = type =>
{
    if (type == typeof(TrackHourlyAggregate) ||
        type == typeof(TrackDailyAggregate) ||
        type == typeof(UserActivityAggregate))
    {
        return "AnalyticsAggregates";
    }
    return DocumentConventions.DefaultGetCollectionName(type);
};

// Enable document expiration
store.Maintenance.Send(new ConfigureExpirationOperation(
    new ExpirationConfiguration
    {
        Disabled = false,
        DeleteFrequencyInSec = 60 // Check every minute
    }));
```

### Retention Configuration

```csharp
public class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Analytics data retention in days.
    /// Default: 30 (per NF-6.3).
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Maximum age of accepted events in hours.
    /// Default: 24.
    /// </summary>
    public int MaxEventAgeHours { get; set; } = 24;

    /// <summary>
    /// Maximum future timestamp tolerance in minutes.
    /// Default: 5.
    /// </summary>
    public int MaxFutureMinutes { get; set; } = 5;

    /// <summary>
    /// Sampling rate for play_progress events under load (0.0-1.0).
    /// Default: 0.9 (keep 90%).
    /// </summary>
    public double ProgressEventSamplingRate { get; set; } = 0.9;

    /// <summary>
    /// Maximum events per batch request.
    /// Default: 50.
    /// </summary>
    public int MaxBatchSize { get; set; } = 50;
}
```

---

## 9. Configuration

### `appsettings.json` Example

```json
{
  "Telemetry": {
    "RetentionDays": 30,
    "MaxEventAgeHours": 24,
    "MaxFutureMinutes": 5,
    "ProgressEventSamplingRate": 0.9,
    "MaxBatchSize": 50
  },
  "RateLimiting": {
    "TelemetryIngest": {
      "PermitLimit": 120,
      "WindowMinutes": 1
    }
  }
}
```

---

## 10. Endpoint Implementation

### `TelemetryEndpoints.cs`

```csharp
namespace NovaTuneApp.ApiService.Endpoints;

public static class TelemetryEndpoints
{
    public static void MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/telemetry")
            .RequireAuthorization(PolicyNames.ActiveUser)
            .WithTags("Telemetry");

        group.MapPost("/playback", HandleIngestPlayback)
            .WithName("IngestPlaybackEvent")
            .WithSummary("Report playback telemetry event")
            .Produces<TelemetryAcceptedResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting("telemetry-ingest");

        group.MapPost("/playback/batch", HandleIngestBatch)
            .WithName("IngestPlaybackEventBatch")
            .WithSummary("Report multiple playback telemetry events")
            .Produces<TelemetryBatchResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("telemetry-ingest-batch");
    }

    private static async Task<IResult> HandleIngestPlayback(
        [FromBody] PlaybackEventRequest request,
        [FromServices] ITelemetryIngestionService telemetryService,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Validate event type
        if (!PlaybackEventTypes.Valid.Contains(request.EventType))
        {
            return Results.Problem(
                title: "Invalid event type",
                detail: $"Event type must be one of: {string.Join(", ", PlaybackEventTypes.Valid)}",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-event-type");
        }

        // Validate track ID format
        if (!Ulid.TryParse(request.TrackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var correlationId = Activity.Current?.Id ?? Ulid.NewUlid().ToString();

        var result = await telemetryService.IngestAsync(
            request,
            userId,
            correlationId,
            ct);

        if (!result.Accepted && result.RejectionReason == "access_denied")
        {
            return Results.Problem(
                title: "Track not accessible",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/track-access-denied");
        }

        return Results.Accepted(
            value: new TelemetryAcceptedResponse(true, correlationId));
    }

    private static async Task<IResult> HandleIngestBatch(
        [FromBody] PlaybackEventBatchRequest request,
        [FromServices] ITelemetryIngestionService telemetryService,
        [FromServices] IOptions<TelemetryOptions> options,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (request.Events.Count > options.Value.MaxBatchSize)
        {
            return Results.Problem(
                title: "Batch too large",
                detail: $"Maximum {options.Value.MaxBatchSize} events per batch.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/batch-too-large");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var correlationId = Activity.Current?.Id ?? Ulid.NewUlid().ToString();

        var result = await telemetryService.IngestBatchAsync(
            request.Events,
            userId,
            correlationId,
            ct);

        return Results.Accepted(
            value: new TelemetryBatchResponse(
                result.AcceptedCount,
                result.RejectedCount,
                result.CorrelationId));
    }
}

public record TelemetryAcceptedResponse(bool Accepted, string CorrelationId);
public record TelemetryBatchResponse(int Accepted, int Rejected, string CorrelationId);
public record PlaybackEventBatchRequest(IReadOnlyList<PlaybackEventRequest> Events);
```

---

## 11. Observability (NF-4.x)

### Logging

| Event | Level | Fields |
|-------|-------|--------|
| Telemetry event ingested | Debug | `EventType`, `TrackId`, `CorrelationId` |
| Telemetry event rejected | Warning | `EventType`, `TrackId`, `Reason` |
| Telemetry event sampled | Debug | `EventType`, `TrackId` |
| Telemetry batch processed | Info | `AcceptedCount`, `RejectedCount`, `CorrelationId` |
| Aggregate updated | Debug | `TrackId`, `AggregateType`, `Bucket` |
| Worker event processed | Debug | `EventType`, `TrackId`, `CorrelationId` |
| Worker event failed | Error | `EventType`, `TrackId`, `Error` |
| Rate limit exceeded | Warning | `UserId`, `DeviceId`, `Endpoint` |

**Redaction (NF-4.5):** Never log session IDs or device IDs in production; use hashed values.

### Metrics (NF-4.2)

| Metric | Type | Labels |
|--------|------|--------|
| `telemetry_events_ingested_total` | Counter | `event_type`, `status` (accepted/rejected/sampled) |
| `telemetry_events_processed_total` | Counter | `event_type`, `status` (success/failure) |
| `telemetry_batch_size` | Histogram | — |
| `telemetry_event_age_seconds` | Histogram | — (server time - client time) |
| `telemetry_worker_lag_messages` | Gauge | `partition` |
| `telemetry_worker_processing_duration_ms` | Histogram | `event_type` |
| `telemetry_aggregation_duration_ms` | Histogram | `aggregate_type` |
| `analytics_query_duration_ms` | Histogram | `query_type` |

### Tracing (NF-4.3)

- Propagate `CorrelationId` from client through API to worker (Req 9.3)
- Span hierarchy:
  - `telemetry.ingest` (API)
    - `telemetry.validate` (child)
    - `kafka.produce` (child)
  - `telemetry.aggregate` (Worker)
    - `db.load_aggregate` (child)
    - `db.save_changes` (child)

---

## 12. Resilience (NF-1.4)

### Timeouts

| Operation | Timeout | Retries |
|-----------|---------|---------|
| Kafka produce | 5s | 2 (with exponential backoff) |
| Track access check | 500ms | 0 (fail fast) |
| RavenDB read (aggregate) | 2s | 1 |
| RavenDB write (aggregate) | 5s | 0 (use optimistic concurrency) |

### Circuit Breaker

| Dependency | Failure Threshold | Half-Open After |
|------------|-------------------|-----------------|
| Kafka/Redpanda | 5 consecutive | 30s |
| RavenDB | 5 consecutive | 30s |

### Failure Handling

**API (Ingestion):**
- If Kafka unavailable → Return `503 Service Unavailable`
- If track check fails → Fail open (accept event, log warning)
- If rate limited → Return `429 Too Many Requests`

**Worker (Aggregation):**
- If RavenDB unavailable → Pause consumption, retry with backoff
- If aggregation fails → Log error, move to retry topic
- Dead letter queue for events that fail 5 times

---

## 13. Security Considerations

### Data Privacy

- Device IDs should be hashed client-side before transmission
- Session IDs are opaque identifiers, not user-identifiable
- Client timestamps are validated but not trusted for billing
- Server timestamps are authoritative

### Input Validation

- All fields validated before processing
- Event types whitelist-only
- Timestamp bounds enforced
- Track ownership verified

### Rate Limiting

- Per-device limiting prevents replay attacks
- Per-user fallback for clients without device ID
- Server-side sampling under load

---

## 14. Test Strategy

### Unit Tests

- `TelemetryIngestionService`: Event validation, track access check, sampling
- `AggregationService`: Aggregate calculations, bucket truncation
- `AnalyticsQueryService`: Date range queries, top tracks calculation
- Timestamp validation bounds
- Event type validation
- Rate limit key extraction

### Integration Tests

- End-to-end telemetry flow: API → Kafka → Worker → RavenDB
- Batch ingestion with mixed valid/invalid events
- Rate limiting enforcement
- Document expiration (retention)
- Analytics query accuracy
- Admin dashboard data availability

### Load Tests

- Sustained 120 events/min per device
- Burst handling (500 events in 10 seconds)
- Worker lag recovery
- Sampling behavior under load

---

## 15. Implementation Tasks

### API Service

- [ ] Add `PlaybackEventRequest` and validation models
- [ ] Add `TelemetryEvent` Kafka message type
- [ ] Add `ITelemetryIngestionService` interface and implementation
- [ ] Add `TelemetryEndpoints.cs` with single and batch endpoints
- [ ] Add `ITrackAccessValidator` for lightweight access checks
- [ ] Add rate limiting policies: `telemetry-ingest`, `telemetry-ingest-batch`
- [ ] Add telemetry metrics to `NovaTuneMetrics`
- [ ] Add `TelemetryOptions` configuration class
- [ ] Configure `{env}-telemetry-events` topic in AppHost

### Telemetry Worker

- [ ] Create `NovaTuneApp.Workers.Telemetry` project
- [ ] Add Kafka consumer for `{env}-telemetry-events`
- [ ] Implement `TelemetryEventHandler`
- [ ] Implement `IAggregationService`
- [ ] Add aggregate document models
- [ ] Add health checks (Redpanda, RavenDB)

### Analytics (for Stage 8)

- [ ] Add `IAnalyticsQueryService` interface and implementation
- [ ] Add RavenDB indexes for aggregates
- [ ] Configure document expiration for retention

### RavenDB

- [ ] Add `TrackDailyAggregates_ByDateRange` index
- [ ] Add `TrackDailyAggregates_TopTracks` map-reduce index
- [ ] Add `UserActivityAggregates_ByUser` index
- [ ] Configure document expiration

### Infrastructure

- [ ] Add `{env}-telemetry-events` topic to Redpanda setup (12 partitions)
- [ ] Configure telemetry worker in AppHost
- [ ] Add dead letter topic for failed events

### Testing

- [ ] Unit tests for `TelemetryIngestionService`
- [ ] Unit tests for `AggregationService`
- [ ] Integration tests for telemetry pipeline
- [ ] Integration tests for retention/expiration

---

## Requirements Covered

- `Req 5.4` — Playback telemetry (play start/stop, duration/position)
- `Req 9.1` — Redpanda topics with schema versioning
- `Req 9.2` — Analytics aggregates in RavenDB for Admin
- `Req 9.3` — CorrelationId propagation for tracing
- `Req 9.4` — JSON event encoding
- `Req 9.5` — TrackId as partition key
- `NF-2.5` — Rate limiting (120/min per device)
- `NF-4.2` — Metrics for event consumption and processing
- `NF-6.3` — 30-day analytics retention

---

## Open Items

- [ ] Determine exact client SDKs/libraries for telemetry reporting
- [ ] Finalize sampling thresholds for high-load scenarios
- [ ] Consider HyperLogLog for unique listener counting
- [ ] Evaluate time-series database (InfluxDB/TimescaleDB) for high-volume analytics
- [ ] Design real-time analytics streaming (WebSocket/SSE) for dashboards
- [ ] Consider event deduplication strategy for offline-buffered events
- [ ] Determine if hourly aggregates are needed or daily is sufficient
- [ ] Plan migration path if analytics volume exceeds RavenDB capacity

---

## Claude Skills

The following Claude Code skills are available to assist with implementing Stage 7:

### Core Patterns

| Skill | Use For | Stage 7 Components |
|-------|---------|-------------------|
| `add-api-endpoint` | Minimal API endpoint structure | Telemetry ingestion endpoints |
| `add-kafka-consumer` | KafkaFlow message handlers | `TelemetryEventHandler` |
| `add-aspire-worker-project` | Worker project creation | `NovaTuneApp.Workers.Telemetry` |
| `add-ravendb-index` | RavenDB index creation | Aggregate indexes |
| `add-rate-limiting` | Rate limiting policies | `telemetry-ingest` policy |
| `add-observability` | Metrics, logging, tracing | Telemetry metrics and spans |
| `add-background-service` | Scheduled cleanup tasks | Retention enforcement (if needed) |

### Usage

Invoke skills using the Skill tool:
```
Skill: add-api-endpoint          # For telemetry endpoints
Skill: add-kafka-consumer        # For TelemetryEventHandler
Skill: add-aspire-worker-project # For telemetry worker project
Skill: add-ravendb-index         # For aggregate indexes
Skill: add-rate-limiting         # For telemetry rate limits
Skill: add-observability         # For metrics and tracing
```

---

## Claude Agents

The following Claude Code agents are available for autonomous task execution:

### Implementation Agents

| Agent | Description | Tools |
|-------|-------------|-------|
| `general-purpose` | General implementation tasks | All tools |

### Workflow Example

Use agents for structured implementation:

```
# Phase 1: API Service
Task(subagent_type="general-purpose", prompt="Implement TelemetryIngestionService and TelemetryEndpoints for playback event ingestion")

# Phase 2: Worker
Task(subagent_type="general-purpose", prompt="Create NovaTuneApp.Workers.Telemetry project with TelemetryEventHandler and AggregationService")

# Phase 3: Testing
Task(subagent_type="general-purpose", prompt="Write unit and integration tests for telemetry pipeline")
```
