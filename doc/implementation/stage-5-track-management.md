# Stage 5 — Track Management + Lifecycle Cleanup

**Goal:** Manage the user library and enforce deletion integrity.

## Overview

```
┌─────────┐  GET /tracks (list/search)      ┌─────────────┐
│ Client  │ ───────────────────────────────►│ API Service │
└────┬────┘ ◄─────────────────────────────── └──────┬──────┘
     │       Paginated track list                   │
     │                                              │ Query RavenDB
     │  GET /tracks/{trackId}                       ▼
     │ ───────────────────────────────────► ┌─────────────────┐
     │ ◄─────────────────────────────────── │    RavenDB      │
     │       Track details                  └─────────────────┘
     │
     │  PATCH /tracks/{trackId}             ┌─────────────────┐
     │ ───────────────────────────────────► │ API Service     │
     │ ◄─────────────────────────────────── │                 │
     │       Updated track                  │ 1. Validate     │
     │                                      │ 2. Merge policy │
     │                                      │ 3. Save         │
     │                                      └─────────────────┘
     │
     │  DELETE /tracks/{trackId}
     │ ───────────────────────────────────► ┌─────────────────┐
     │ ◄─────────────────────────────────── │ API Service     │
     │       204 No Content                 └────────┬────────┘
     │                                               │
     │                                               │ 1. Soft-delete (Status=Deleted)
     │                                               │ 2. Publish TrackDeletedEvent
     │                                               │ 3. Invalidate stream cache
     │                                               ▼
     │                                      ┌─────────────────┐
     │                                      │   Redpanda      │
     │                                      │ (track-deletions)│
     │                                      └────────┬────────┘
     │                                               │
     │                                               ▼
     │                                      ┌─────────────────┐
     │                                      │ Lifecycle Worker│
     │                                      │ (scheduled)     │
     │                                      │                 │
     │                                      │ After grace:    │
     │                                      │ - Delete MinIO  │
     │                                      │ - Delete doc    │
     │                                      │ - Update quota  │
     │                                      └─────────────────┘
```

---

## 1. API Endpoint: `GET /tracks`

### Request

- **Method:** `GET`
- **Path:** `/tracks`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; returns only user's own tracks

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `search` | string | — | Full-text search on title and artist |
| `status` | string | — | Filter by status: `Processing`, `Ready`, `Failed` (excludes `Deleted` by default) |
| `sortBy` | string | `createdAt` | Sort field: `createdAt`, `updatedAt`, `title`, `artist`, `duration` |
| `sortOrder` | string | `desc` | Sort direction: `asc`, `desc` |
| `cursor` | string | — | Cursor for pagination (base64-encoded) |
| `limit` | int | 20 | Page size (1-100) |
| `includeDeleted` | bool | `false` | Include soft-deleted tracks (for restore UI) |

### Response Schema (Success: 200 OK)

```json
{
  "items": [
    {
      "trackId": "01HXK...",
      "title": "My Track",
      "artist": "Artist Name",
      "duration": "PT3M42S",
      "status": "Ready",
      "fileSizeBytes": 15728640,
      "mimeType": "audio/mpeg",
      "createdAt": "2025-01-08T10:00:00Z",
      "updatedAt": "2025-01-08T10:05:00Z",
      "processedAt": "2025-01-08T10:05:00Z"
    }
  ],
  "nextCursor": "eyJza...",
  "totalCount": 150,
  "hasMore": true
}
```

### Pagination Strategy (Cursor-Based)

Cursor-based pagination provides stable results when tracks are added/deleted during navigation:

```csharp
internal record TrackListCursor(
    string SortValue,      // Value of the sort field at cursor position
    string TrackId,        // Tie-breaker for stable ordering
    DateTimeOffset Timestamp);
```

- Cursor is base64-encoded JSON
- Sort tie-breaker uses `TrackId` (ULID provides chronological ordering)
- `totalCount` is approximate (computed periodically, not per-request)

### RavenDB Index: `Tracks_ByUserForSearch`

```csharp
public class Tracks_ByUserForSearch : AbstractIndexCreationTask<Track>
{
    public Tracks_ByUserForSearch()
    {
        Map = tracks => from track in tracks
                        where track.Status != TrackStatus.Unknown
                        select new
                        {
                            track.UserId,
                            track.Status,
                            track.Title,
                            track.Artist,
                            track.CreatedAt,
                            track.UpdatedAt,
                            track.Duration,
                            SearchText = new[] { track.Title, track.Artist }
                        };

        Index("SearchText", FieldIndexing.Search);
        Analyze("SearchText", "StandardAnalyzer");
    }
}
```

### Rate Limiting (Req 8.2, NF-2.5)

- Policy: `track-list`
- Default: 60 requests/minute per user
- Response on limit: `429 Too Many Requests` with `Retry-After` header

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-query-parameter` | Invalid sort field, cursor, or limit |
| `401` | `unauthorized` | Missing or invalid authentication |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |

---

## 2. API Endpoint: `GET /tracks/{trackId}`

### Request

- **Method:** `GET`
- **Path:** `/tracks/{trackId}`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the track

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `trackId` | string | ULID identifier for the track |

### Response Schema (Success: 200 OK)

```json
{
  "trackId": "01HXK...",
  "title": "My Track",
  "artist": "Artist Name",
  "duration": "PT3M42S",
  "status": "Ready",
  "fileSizeBytes": 15728640,
  "mimeType": "audio/mpeg",
  "metadata": {
    "bitrate": 320000,
    "sampleRate": 44100,
    "channels": 2,
    "codec": "mp3"
  },
  "hasWaveform": true,
  "createdAt": "2025-01-08T10:00:00Z",
  "updatedAt": "2025-01-08T10:05:00Z",
  "processedAt": "2025-01-08T10:05:00Z",
  "deletedAt": null,
  "scheduledDeletionAt": null
}
```

### Validation Rules (Req 6.4)

| Check | Rule | Error |
|-------|------|-------|
| Track ID format | Must be valid ULID | `400 Bad Request` |
| Track exists | Track document must exist in RavenDB | `404 Not Found` |
| Ownership | `Track.UserId` must match authenticated user | `403 Forbidden` |

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-track-id` | Malformed ULID |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own track |
| `404` | `track-not-found` | Track does not exist |

---

## 3. API Endpoint: `PATCH /tracks/{trackId}`

### Request

- **Method:** `PATCH`
- **Path:** `/tracks/{trackId}`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the track

### Request Schema

```json
{
  "title": "Updated Title",
  "artist": "Updated Artist"
}
```

All fields are optional; only provided fields are updated (merge policy per `NF-6.2`).

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `trackId` | string | ULID identifier for the track |

### Response Schema (Success: 200 OK)

Returns the full updated track (same schema as `GET /tracks/{trackId}`).

### Validation Rules (Req 6.2, NF-6.2)

| Field | Rule | Error Code |
|-------|------|------------|
| `title` | 1-255 characters, non-empty if provided | `INVALID_TITLE` |
| `artist` | 0-255 characters | `INVALID_ARTIST` |
| Track status | Cannot update `Deleted` tracks | `TRACK_DELETED` |

### Merge Policy (NF-6.2)

- Only fields present in the request body are updated
- `null` values clear the field (except `title` which is required)
- `UpdatedAt` is set to current timestamp on any change
- Use optimistic concurrency with RavenDB `@etag`

### Concurrency Handling

```csharp
public async Task<Track> UpdateTrackAsync(
    string trackId,
    UpdateTrackRequest request,
    string userId,
    CancellationToken ct = default)
{
    var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);

    if (track is null)
        throw new TrackNotFoundException(trackId);

    if (track.UserId != userId)
        throw new TrackAccessDeniedException(trackId);

    if (track.Status == TrackStatus.Deleted)
        throw new TrackDeletedException(trackId);

    // Merge policy: only update provided fields
    if (request.Title is not null)
        track.Title = request.Title;

    if (request.Artist is not null)
        track.Artist = request.Artist == "" ? null : request.Artist;

    track.UpdatedAt = DateTimeOffset.UtcNow;

    await _session.SaveChangesAsync(ct);
    return track;
}
```

### Rate Limiting

- Policy: `track-update`
- Default: 30 requests/minute per user

### Error Responses (RFC 7807)

```json
{
  "type": "https://novatune.dev/errors/track-deleted",
  "title": "Track is deleted",
  "status": 409,
  "detail": "Cannot update a deleted track. Restore the track first.",
  "instance": "/tracks/01HXK...",
  "extensions": {
    "trackId": "01HXK...",
    "deletedAt": "2025-01-08T12:00:00Z"
  }
}
```

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-track-id` | Malformed ULID |
| `400` | `validation-error` | Title or artist validation failed |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own track |
| `404` | `track-not-found` | Track does not exist |
| `409` | `track-deleted` | Track is soft-deleted |
| `409` | `concurrency-conflict` | Concurrent modification detected |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |

---

## 4. API Endpoint: `DELETE /tracks/{trackId}`

### Request

- **Method:** `DELETE`
- **Path:** `/tracks/{trackId}`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the track

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `trackId` | string | ULID identifier for the track |

### Response (Success: 204 No Content)

No response body.

### Soft-Delete Semantics (Req 6.3, NF-6.1)

Deletion is a two-phase process:

1. **Immediate (soft-delete):**
   - Set `Track.Status = Deleted`
   - Set `Track.DeletedAt = DateTimeOffset.UtcNow`
   - Set `Track.ScheduledDeletionAt = DeletedAt + GracePeriod` (default: 30 days)
   - Publish `TrackDeletedEvent` via outbox
   - Invalidate cached streaming URLs

2. **Deferred (physical deletion):**
   - Lifecycle worker picks up tracks where `ScheduledDeletionAt <= now`
   - Delete MinIO objects (audio + waveform)
   - Delete RavenDB document
   - Decrement user's `UsedStorageBytes`

### Validation Rules

| Check | Rule | Error |
|-------|------|-------|
| Track ID format | Must be valid ULID | `400 Bad Request` |
| Track exists | Track document must exist | `404 Not Found` |
| Ownership | `Track.UserId` must match authenticated user | `403 Forbidden` |
| Not already deleted | `Track.Status != Deleted` | `409 Conflict` |

### Rate Limiting

- Policy: `track-delete`
- Default: 10 requests/minute per user

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-track-id` | Malformed ULID |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own track |
| `404` | `track-not-found` | Track does not exist |
| `409` | `already-deleted` | Track is already soft-deleted |

---

## 5. API Endpoint: `POST /tracks/{trackId}/restore` (Optional)

### Request

- **Method:** `POST`
- **Path:** `/tracks/{trackId}/restore`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the track

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `trackId` | string | ULID identifier for the track |

### Response Schema (Success: 200 OK)

Returns the restored track (same schema as `GET /tracks/{trackId}`).

### Restoration Rules (NF-6.1)

| Check | Rule | Error |
|-------|------|-------|
| Track exists | Track document must exist | `404 Not Found` |
| Ownership | `Track.UserId` must match authenticated user | `403 Forbidden` |
| Is deleted | `Track.Status == Deleted` | `409 Conflict` |
| Within grace period | `ScheduledDeletionAt > DateTimeOffset.UtcNow` | `410 Gone` |

### Restoration Actions

1. Set `Track.Status` to previous status (stored in `Track.StatusBeforeDeletion`)
2. Clear `Track.DeletedAt` and `Track.ScheduledDeletionAt`
3. Clear `Track.StatusBeforeDeletion`
4. Set `Track.UpdatedAt = DateTimeOffset.UtcNow`

### Error Responses (RFC 7807)

```json
{
  "type": "https://novatune.dev/errors/restoration-expired",
  "title": "Restoration period expired",
  "status": 410,
  "detail": "The track cannot be restored because the 30-day grace period has expired.",
  "instance": "/tracks/01HXK.../restore",
  "extensions": {
    "trackId": "01HXK...",
    "deletedAt": "2024-12-01T12:00:00Z",
    "scheduledDeletionAt": "2024-12-31T12:00:00Z"
  }
}
```

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-track-id` | Malformed ULID |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own track |
| `404` | `track-not-found` | Track does not exist |
| `409` | `not-deleted` | Track is not in deleted state |
| `410` | `restoration-expired` | Grace period has passed |

---

## 6. Track Model Updates

### Extended Track Document

```csharp
public sealed class Track
{
    public string Id { get; init; } = string.Empty;

    [Required]
    [MaxLength(26)]
    public string TrackId { get; init; } = string.Empty;

    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Artist { get; set; }

    public TimeSpan Duration { get; set; }

    [Required]
    public string ObjectKey { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [MaxLength(64)]
    public string MimeType { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? Checksum { get; set; }

    public AudioMetadata? Metadata { get; set; }
    public TrackStatus Status { get; set; } = TrackStatus.Processing;

    [MaxLength(512)]
    public string? WaveformObjectKey { get; set; }

    [MaxLength(64)]
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    // Soft-delete fields (Stage 5)
    /// <summary>
    /// Timestamp when the track was soft-deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Timestamp when physical deletion will occur.
    /// </summary>
    public DateTimeOffset? ScheduledDeletionAt { get; set; }

    /// <summary>
    /// Status before deletion, used for restoration.
    /// </summary>
    public TrackStatus? StatusBeforeDeletion { get; set; }
}
```

---

## 7. Track Management Service

### Interface: `ITrackManagementService`

```csharp
namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for track CRUD operations.
/// </summary>
public interface ITrackManagementService
{
    /// <summary>
    /// Lists tracks for a user with pagination, filtering, and sorting.
    /// </summary>
    Task<PagedResult<TrackListItem>> ListTracksAsync(
        string userId,
        TrackListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Gets track details by ID.
    /// </summary>
    Task<TrackDetails> GetTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates track metadata (title, artist).
    /// </summary>
    Task<TrackDetails> UpdateTrackAsync(
        string trackId,
        string userId,
        UpdateTrackRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a track.
    /// </summary>
    Task DeleteTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Restores a soft-deleted track within the grace period.
    /// </summary>
    Task<TrackDetails> RestoreTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);
}
```

### Supporting Types

```csharp
public record TrackListQuery(
    string? Search = null,
    TrackStatus? Status = null,
    string SortBy = "createdAt",
    string SortOrder = "desc",
    string? Cursor = null,
    int Limit = 20,
    bool IncludeDeleted = false);

public record TrackListItem(
    string TrackId,
    string Title,
    string? Artist,
    TimeSpan Duration,
    TrackStatus Status,
    long FileSizeBytes,
    string MimeType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ProcessedAt);

public record TrackDetails(
    string TrackId,
    string Title,
    string? Artist,
    TimeSpan Duration,
    TrackStatus Status,
    long FileSizeBytes,
    string MimeType,
    AudioMetadata? Metadata,
    bool HasWaveform,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset? DeletedAt,
    DateTimeOffset? ScheduledDeletionAt);

public record UpdateTrackRequest(
    string? Title,
    string? Artist);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    int TotalCount,
    bool HasMore);
```

---

## 8. Event: `TrackDeletedEvent`

### Event Schema

**Note:** Migrate from `Guid` to ULID string per cross-cutting decision 3.1.

```csharp
namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

/// <summary>
/// Event published when a track is soft-deleted.
/// </summary>
public record TrackDeletedEvent
{
    public int SchemaVersion { get; init; } = 2;
    public required string TrackId { get; init; }       // ULID string
    public required string UserId { get; init; }        // ULID string
    public required string ObjectKey { get; init; }     // For lifecycle worker
    public string? WaveformObjectKey { get; init; }     // For lifecycle worker
    public required long FileSizeBytes { get; init; }   // For quota adjustment
    public required DateTimeOffset DeletedAt { get; init; }
    public required DateTimeOffset ScheduledDeletionAt { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
```

### Publishing Flow

```csharp
public async Task DeleteTrackAsync(
    string trackId,
    string userId,
    CancellationToken ct = default)
{
    var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);

    // Validation...

    var now = DateTimeOffset.UtcNow;
    var scheduledDeletion = now.Add(_options.Value.DeletionGracePeriod);

    // Soft-delete
    track.StatusBeforeDeletion = track.Status;
    track.Status = TrackStatus.Deleted;
    track.DeletedAt = now;
    track.ScheduledDeletionAt = scheduledDeletion;
    track.UpdatedAt = now;

    // Create outbox message
    var evt = new TrackDeletedEvent
    {
        TrackId = trackId,
        UserId = userId,
        ObjectKey = track.ObjectKey,
        WaveformObjectKey = track.WaveformObjectKey,
        FileSizeBytes = track.FileSizeBytes,
        DeletedAt = now,
        ScheduledDeletionAt = scheduledDeletion,
        CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
        Timestamp = now
    };

    var outbox = new OutboxMessage
    {
        Id = $"OutboxMessages/{Ulid.NewUlid()}",
        EventType = nameof(TrackDeletedEvent),
        Payload = JsonSerializer.Serialize(evt),
        PartitionKey = trackId,
        CreatedAt = now
    };

    await _session.StoreAsync(outbox, ct);
    await _session.SaveChangesAsync(ct);

    // Invalidate streaming cache immediately
    await _streamingService.InvalidateCacheAsync(trackId, userId, ct);

    _logger.LogInformation(
        "Track {TrackId} soft-deleted, scheduled for physical deletion at {ScheduledAt}",
        trackId, scheduledDeletion);
}
```

---

## 9. Lifecycle Worker

### Project: `NovaTuneApp.Workers.Lifecycle`

Separate deployment per NF-1.1. Handles both event-driven and scheduled cleanup.

### Event Handler: `TrackDeletedHandler`

Consumes `TrackDeletedEvent` for immediate actions:

```csharp
public class TrackDeletedHandler : IMessageHandler<TrackDeletedEvent>
{
    private readonly IStreamingService _streamingService;
    private readonly ILogger<TrackDeletedHandler> _logger;

    public async Task Handle(IMessageContext context, TrackDeletedEvent message)
    {
        // Cache invalidation (idempotent)
        await _streamingService.InvalidateCacheAsync(
            message.TrackId,
            message.UserId,
            context.ConsumerContext.WorkerStopped);

        _logger.LogDebug(
            "Processed TrackDeletedEvent for {TrackId}, scheduled deletion at {ScheduledAt}",
            message.TrackId,
            message.ScheduledDeletionAt);
    }
}
```

### Background Service: `PhysicalDeletionService`

Polls for tracks ready for physical deletion:

```csharp
public class PhysicalDeletionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<LifecycleOptions> _options;
    private readonly ILogger<PhysicalDeletionService> _logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessDeletionsAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing physical deletions");
            }

            await Task.Delay(_options.Value.PollingInterval, ct);
        }
    }

    private async Task ProcessDeletionsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IAsyncDocumentSession>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        var tracksToDelete = await session
            .Query<Track, Tracks_ByScheduledDeletion>()
            .Where(t => t.Status == TrackStatus.Deleted
                     && t.ScheduledDeletionAt <= DateTimeOffset.UtcNow)
            .Take(_options.Value.BatchSize)
            .ToListAsync(ct);

        foreach (var track in tracksToDelete)
        {
            try
            {
                // Delete MinIO objects
                await storageService.DeleteObjectAsync(track.ObjectKey, ct);

                if (track.WaveformObjectKey is not null)
                {
                    await storageService.DeleteObjectAsync(track.WaveformObjectKey, ct);
                }

                // Update user quota
                var user = await session.LoadAsync<User>($"Users/{track.UserId}", ct);
                if (user is not null)
                {
                    user.UsedStorageBytes -= track.FileSizeBytes;
                }

                // Delete document
                session.Delete(track);
                await session.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Physically deleted track {TrackId} for user {UserId}",
                    track.TrackId, track.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to physically delete track {TrackId}",
                    track.TrackId);
                // Continue with next track; will retry on next poll
            }
        }
    }
}
```

### RavenDB Index: `Tracks_ByScheduledDeletion`

```csharp
public class Tracks_ByScheduledDeletion : AbstractIndexCreationTask<Track>
{
    public Tracks_ByScheduledDeletion()
    {
        Map = tracks => from track in tracks
                        where track.Status == TrackStatus.Deleted
                           && track.ScheduledDeletionAt != null
                        select new
                        {
                            track.Status,
                            track.ScheduledDeletionAt
                        };
    }
}
```

---

## 10. Configuration

### `TrackManagementOptions`

```csharp
public class TrackManagementOptions
{
    public const string SectionName = "TrackManagement";

    /// <summary>
    /// Grace period before physical deletion.
    /// Default: 30 days.
    /// </summary>
    public TimeSpan DeletionGracePeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Maximum tracks returned per page.
    /// Default: 100.
    /// </summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// Default tracks returned per page.
    /// Default: 20.
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;
}
```

### `LifecycleOptions`

```csharp
public class LifecycleOptions
{
    public const string SectionName = "Lifecycle";

    /// <summary>
    /// Interval between physical deletion polling.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum tracks to process per polling cycle.
    /// Default: 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;
}
```

### `appsettings.json` Example

```json
{
  "TrackManagement": {
    "DeletionGracePeriod": "30.00:00:00",
    "MaxPageSize": 100,
    "DefaultPageSize": 20
  },
  "Lifecycle": {
    "PollingInterval": "00:05:00",
    "BatchSize": 50
  }
}
```

---

## 11. Endpoint Implementation

### `TrackEndpoints.cs`

```csharp
namespace NovaTuneApp.ApiService.Endpoints;

public static class TrackEndpoints
{
    public static void MapTrackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tracks")
            .RequireAuthorization(PolicyNames.ActiveUser)
            .WithTags("Tracks");

        group.MapGet("/", HandleListTracks)
            .WithName("ListTracks")
            .WithSummary("List user's tracks with search, filter, and pagination")
            .Produces<PagedResult<TrackListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("track-list");

        group.MapGet("/{trackId}", HandleGetTrack)
            .WithName("GetTrack")
            .WithSummary("Get track details")
            .Produces<TrackDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{trackId}", HandleUpdateTrack)
            .WithName("UpdateTrack")
            .WithSummary("Update track metadata")
            .Produces<TrackDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("track-update");

        group.MapDelete("/{trackId}", HandleDeleteTrack)
            .WithName("DeleteTrack")
            .WithSummary("Soft-delete a track")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("track-delete");

        group.MapPost("/{trackId}/restore", HandleRestoreTrack)
            .WithName("RestoreTrack")
            .WithSummary("Restore a soft-deleted track")
            .Produces<TrackDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
    }

    private static async Task<IResult> HandleListTracks(
        [AsParameters] TrackListQueryParams queryParams,
        [FromServices] ITrackManagementService trackService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var query = new TrackListQuery(
            queryParams.Search,
            queryParams.Status,
            queryParams.SortBy ?? "createdAt",
            queryParams.SortOrder ?? "desc",
            queryParams.Cursor,
            queryParams.Limit ?? 20,
            queryParams.IncludeDeleted ?? false);

        var result = await trackService.ListTracksAsync(userId, query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetTrack(
        [FromRoute] string trackId,
        [FromServices] ITrackManagementService trackService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(trackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            var track = await trackService.GetTrackAsync(trackId, userId, ct);
            return Results.Ok(track);
        }
        catch (TrackNotFoundException)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }
        catch (TrackAccessDeniedException)
        {
            return Results.Problem(
                title: "Access denied",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/forbidden");
        }
    }

    // Additional handlers for Update, Delete, Restore...
}

public record TrackListQueryParams(
    [FromQuery] string? Search,
    [FromQuery] TrackStatus? Status,
    [FromQuery] string? SortBy,
    [FromQuery] string? SortOrder,
    [FromQuery] string? Cursor,
    [FromQuery] int? Limit,
    [FromQuery] bool? IncludeDeleted);
```

---

## 12. Observability (NF-4.x)

### Logging

| Event | Level | Fields |
|-------|-------|--------|
| Track list requested | Debug | `UserId`, `Search`, `Status`, `Limit` |
| Track retrieved | Debug | `TrackId`, `UserId` |
| Track updated | Info | `TrackId`, `UserId`, `ChangedFields` |
| Track soft-deleted | Info | `TrackId`, `UserId`, `ScheduledDeletionAt` |
| Track restored | Info | `TrackId`, `UserId` |
| Physical deletion started | Info | `TrackId`, `UserId` |
| Physical deletion completed | Info | `TrackId`, `UserId`, `FreedBytes` |
| Physical deletion failed | Error | `TrackId`, `Error` |
| Access denied | Warning | `TrackId`, `UserId`, `OwnerId` |

**Redaction (NF-4.5):** Never log object keys in production.

### Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `track_list_requests_total` | Counter | `status` (success/error) |
| `track_list_request_duration_ms` | Histogram | — |
| `track_get_requests_total` | Counter | `status` |
| `track_update_requests_total` | Counter | `status` |
| `track_delete_requests_total` | Counter | `status` |
| `track_restore_requests_total` | Counter | `status` |
| `track_soft_deletions_total` | Counter | — |
| `track_physical_deletions_total` | Counter | `status` (success/failure) |
| `track_physical_deletion_duration_ms` | Histogram | — |
| `storage_freed_bytes_total` | Counter | — |

### Tracing

- Propagate `CorrelationId` across all track operations
- Span hierarchy for delete:
  - `track.delete` (parent)
    - `db.update_status` (child)
    - `outbox.write` (child)
    - `cache.invalidate` (child)

---

## 13. Resilience (NF-1.4)

### Timeouts

| Operation | Timeout | Retries |
|-----------|---------|---------|
| RavenDB read (track) | 2s | 1 |
| RavenDB write (update/delete) | 5s | 0 (use optimistic concurrency) |
| RavenDB query (list) | 5s | 1 |
| Cache invalidation | 500ms | 0 (cache is optional) |
| MinIO deletion (lifecycle) | 10s | 2 |

### Circuit Breaker

| Dependency | Failure Threshold | Half-Open After |
|------------|-------------------|-----------------|
| RavenDB | 5 consecutive | 30s |
| MinIO (lifecycle only) | 5 consecutive | 60s |

### Fail-Closed Behavior

All track management operations fail closed:
- If RavenDB unavailable → `503 Service Unavailable`

Lifecycle worker is tolerant:
- If MinIO unavailable → Log error, skip track, retry on next poll
- If RavenDB unavailable → Pause polling until healthy

---

## 14. Security Considerations

### Access Control

- All endpoints require authentication
- Users can only access their own tracks
- Deleted tracks remain accessible for restore during grace period
- `includeDeleted` query parameter only returns user's own deleted tracks

### Data Integrity

- Soft-delete preserves data for restoration and audit
- Physical deletion is deferred and logged
- Quota is only adjusted after physical deletion
- Concurrent updates handled via optimistic concurrency

### Audit Trail

Track state changes are logged with:
- User ID
- Track ID
- Operation type
- Timestamp
- Correlation ID

---

## 15. Test Strategy

### Unit Tests

- `TrackManagementService`: CRUD operations
- Pagination cursor encoding/decoding
- Query validation (sort fields, limit bounds)
- Merge policy for updates
- Soft-delete state transitions
- Restoration within/outside grace period
- Exception mapping

### Integration Tests

- End-to-end track CRUD flow
- Pagination with concurrent modifications
- Soft-delete → restore → delete again
- Physical deletion via lifecycle worker
- Cache invalidation on delete
- Quota adjustment after physical deletion
- Rate limiting enforcement

---

## 16. Implementation Tasks

### API Service

- [ ] Add `ITrackManagementService` interface and implementation
- [ ] Add `TrackEndpoints.cs` with all CRUD endpoints
- [ ] Add `TrackListQuery` and `TrackListQueryParams` types
- [ ] Add `Tracks_ByUserForSearch` RavenDB index
- [ ] Add `Tracks_ByScheduledDeletion` RavenDB index
- [ ] Add `TrackManagementOptions` configuration class
- [ ] Add rate limiting policies: `track-list`, `track-update`, `track-delete`
- [ ] Add track management metrics to `NovaTuneMetrics`
- [ ] Add track-specific exceptions (`TrackNotFoundException`, etc.)
- [ ] Extend `Track` model with soft-delete fields
- [ ] Migrate `TrackDeletedEvent` from Guid to ULID strings
- [ ] Update `IStorageService` with `DeleteObjectAsync` method

### Lifecycle Worker

- [ ] Create `NovaTuneApp.Workers.Lifecycle` project
- [ ] Add Kafka consumer for `{env}-track-deletions`
- [ ] Implement `TrackDeletedHandler` for immediate cache invalidation
- [ ] Implement `PhysicalDeletionService` background service
- [ ] Add `LifecycleOptions` configuration class
- [ ] Add health checks (Redpanda, RavenDB, MinIO)

### Infrastructure

- [ ] Add `{env}-track-deletions` topic to Redpanda setup
- [ ] Configure lifecycle worker in AppHost

### Testing

- [ ] Unit tests for `TrackManagementService`
- [ ] Unit tests for pagination cursor logic
- [ ] Integration tests for track CRUD endpoints
- [ ] Integration tests for lifecycle worker

---

## Requirements Covered

- `Req 4.4` — Schedule physical deletion after grace period
- `Req 6.1` — List tracks with search, filter, sort, and pagination
- `Req 6.2` — Update track metadata with merge policy
- `Req 6.3` — Soft-delete tracks
- `Req 6.4` — Get track details
- `NF-2.5` — Rate limiting on track endpoints
- `NF-6.1` — Repeatable deletion jobs; no operations on deleted tracks
- `NF-6.2` — Optimistic concurrency; monotonic state transitions
- `NF-6.3` — Configurable retention/grace periods

---

## Open Items

- [ ] Determine exact full-text search configuration (analyzer, stemming)
- [ ] Decide if `totalCount` should be exact or approximate
- [ ] Finalize rate limit values for track endpoints
- [ ] Consider bulk delete endpoint for batch operations
- [ ] Determine if waveform should have separate presigned URL endpoint
- [ ] Consider adding track download endpoint (separate from streaming)
