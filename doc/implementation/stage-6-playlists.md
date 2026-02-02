# Stage 6 — Playlists

**Goal:** Enable playlist CRUD with stable ordering and track reference integrity.

## Overview

```
┌─────────┐  GET /playlists (list)            ┌─────────────┐
│ Client  │ ───────────────────────────────► │ API Service │
└────┬────┘ ◄─────────────────────────────── └──────┬──────┘
     │       Paginated playlist list                │
     │                                              │ Query RavenDB
     │  POST /playlists (create)                    ▼
     │ ───────────────────────────────────► ┌─────────────────┐
     │ ◄─────────────────────────────────── │    RavenDB      │
     │       201 Created + playlist         │                 │
     │                                      │  ┌───────────┐  │
     │  GET /playlists/{id}                 │  │ Playlists │  │
     │ ───────────────────────────────────► │  └───────────┘  │
     │ ◄─────────────────────────────────── │        │        │
     │       Playlist with tracks           │        ▼ ref    │
     │                                      │  ┌───────────┐  │
     │  PATCH /playlists/{id}               │  │  Tracks   │  │
     │ ───────────────────────────────────► │  └───────────┘  │
     │ ◄─────────────────────────────────── │                 │
     │       Updated playlist               └─────────────────┘
     │
     │  DELETE /playlists/{id}
     │ ───────────────────────────────────►
     │ ◄───────────────────────────────────
     │       204 No Content
     │
     │  POST /playlists/{id}/tracks
     │ ───────────────────────────────────►  Add tracks to playlist
     │ ◄───────────────────────────────────
     │       200 OK + updated playlist
     │
     │  DELETE /playlists/{id}/tracks/{position}
     │ ───────────────────────────────────►  Remove track at position
     │ ◄───────────────────────────────────
     │       204 No Content
     │
     │  POST /playlists/{id}/reorder
     │ ───────────────────────────────────►  Reorder tracks
     │ ◄───────────────────────────────────
     │       200 OK + updated playlist
```

---

## 1. Data Model

### Playlist Document

```csharp
namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Represents a user-owned playlist with ordered track references.
/// </summary>
public sealed class Playlist
{
    /// <summary>
    /// RavenDB document ID (internal).
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Public ULID identifier.
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string PlaylistId { get; init; } = string.Empty;

    /// <summary>
    /// Owner user ID (ULID).
    /// </summary>
    [Required]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Playlist display name.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Ordered list of track entries.
    /// </summary>
    public List<PlaylistTrackEntry> Tracks { get; set; } = [];

    /// <summary>
    /// Total number of tracks (denormalized for list queries).
    /// </summary>
    public int TrackCount { get; set; }

    /// <summary>
    /// Total duration of all tracks (denormalized).
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Playlist visibility for future sharing support.
    /// </summary>
    public PlaylistVisibility Visibility { get; set; } = PlaylistVisibility.Private;

    /// <summary>
    /// Timestamp when the playlist was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Timestamp of the last modification.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Represents a track entry within a playlist.
/// </summary>
public sealed class PlaylistTrackEntry
{
    /// <summary>
    /// Position in the playlist (0-based, stable ordering).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Reference to the track ID (ULID).
    /// </summary>
    [Required]
    public string TrackId { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when this track was added.
    /// </summary>
    public DateTimeOffset AddedAt { get; init; }
}

/// <summary>
/// Playlist visibility settings (for future sharing support).
/// </summary>
public enum PlaylistVisibility
{
    /// <summary>
    /// Only the owner can access.
    /// </summary>
    Private = 0,

    /// <summary>
    /// Anyone with the link can view (future).
    /// </summary>
    Unlisted = 1,

    /// <summary>
    /// Publicly discoverable (future).
    /// </summary>
    Public = 2
}
```

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Track entries embedded in playlist** | Playlists typically have <1000 tracks; embedded list simplifies queries and atomic updates |
| **Position field for ordering** | Explicit positions allow stable ordering and efficient reordering |
| **Duplicates allowed** | Per Req 7 clarifications; same track can appear multiple times |
| **TrackCount/TotalDuration denormalized** | Avoids computing aggregates on every list query |
| **Visibility enum** | Anticipates future sharing per Req 7 clarifications |

---

## 2. API Endpoint: `GET /playlists`

### Request

- **Method:** `GET`
- **Path:** `/playlists`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; returns only user's own playlists

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `search` | string | — | Search by playlist name |
| `sortBy` | string | `updatedAt` | Sort field: `createdAt`, `updatedAt`, `name`, `trackCount` |
| `sortOrder` | string | `desc` | Sort direction: `asc`, `desc` |
| `cursor` | string | — | Cursor for pagination (base64-encoded) |
| `limit` | int | 20 | Page size (1-50) |

### Response Schema (Success: 200 OK)

```json
{
  "items": [
    {
      "playlistId": "01HXK...",
      "name": "My Favorites",
      "description": "Best tracks of 2025",
      "trackCount": 42,
      "totalDuration": "PT2H15M30S",
      "visibility": "Private",
      "createdAt": "2025-01-08T10:00:00Z",
      "updatedAt": "2025-01-10T14:30:00Z"
    }
  ],
  "nextCursor": "eyJza...",
  "totalCount": 15,
  "hasMore": true
}
```

### Pagination Strategy (Cursor-Based)

```csharp
internal record PlaylistListCursor(
    string SortValue,      // Value of the sort field at cursor position
    string PlaylistId,     // Tie-breaker for stable ordering
    DateTimeOffset Timestamp);
```

### Rate Limiting (NF-2.5)

- Policy: `playlist-list`
- Default: 60 requests/minute per user

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-query-parameter` | Invalid sort field, cursor, or limit |
| `401` | `unauthorized` | Missing or invalid authentication |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |

---

## 3. API Endpoint: `POST /playlists`

### Request

- **Method:** `POST`
- **Path:** `/playlists`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role

### Request Schema

```json
{
  "name": "My New Playlist",
  "description": "Optional description"
}
```

### Response Schema (Success: 201 Created)

```json
{
  "playlistId": "01HXK...",
  "name": "My New Playlist",
  "description": "Optional description",
  "trackCount": 0,
  "totalDuration": "PT0S",
  "visibility": "Private",
  "createdAt": "2025-01-08T10:00:00Z",
  "updatedAt": "2025-01-08T10:00:00Z"
}
```

### Validation Rules (Req 7.1, NF-2.4)

| Field | Rule | Error Code |
|-------|------|------------|
| `name` | 1-100 characters, non-empty | `INVALID_NAME` |
| `description` | 0-500 characters | `INVALID_DESCRIPTION` |
| Playlist quota | Max 200 playlists per user | `PLAYLIST_QUOTA_EXCEEDED` |

### Rate Limiting

- Policy: `playlist-create`
- Default: 20 requests/minute per user

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `validation-error` | Name or description validation failed |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `playlist-quota-exceeded` | User has reached 200 playlists |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |

---

## 4. API Endpoint: `GET /playlists/{playlistId}`

### Request

- **Method:** `GET`
- **Path:** `/playlists/{playlistId}`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the playlist

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `playlistId` | string | ULID identifier for the playlist |

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `includeTracks` | bool | `true` | Include track details in response |
| `trackCursor` | string | — | Cursor for paginated track list |
| `trackLimit` | int | 50 | Number of tracks to return (1-100) |

### Response Schema (Success: 200 OK)

```json
{
  "playlistId": "01HXK...",
  "name": "My Favorites",
  "description": "Best tracks of 2025",
  "trackCount": 42,
  "totalDuration": "PT2H15M30S",
  "visibility": "Private",
  "createdAt": "2025-01-08T10:00:00Z",
  "updatedAt": "2025-01-10T14:30:00Z",
  "tracks": {
    "items": [
      {
        "position": 0,
        "trackId": "01HXK...",
        "title": "Track Title",
        "artist": "Artist Name",
        "duration": "PT3M42S",
        "status": "Ready",
        "addedAt": "2025-01-08T10:05:00Z"
      }
    ],
    "nextCursor": "eyJza...",
    "hasMore": true
  }
}
```

### Track Reference Resolution

When `includeTracks=true`, track metadata is loaded via RavenDB `Include`:

```csharp
var playlist = await session
    .Include<Playlist>(p => p.Tracks.Select(t => $"Tracks/{t.TrackId}"))
    .LoadAsync<Playlist>($"Playlists/{playlistId}", ct);
```

**Handling deleted tracks:**
- Tracks with `Status == Deleted` are included with a `"status": "Deleted"` marker
- UI can display these as unavailable or filter them client-side
- Physical deletion of tracks removes them from playlists via lifecycle worker

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-playlist-id` | Malformed ULID |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own playlist |
| `404` | `playlist-not-found` | Playlist does not exist |

---

## 5. API Endpoint: `PATCH /playlists/{playlistId}`

### Request

- **Method:** `PATCH`
- **Path:** `/playlists/{playlistId}`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the playlist

### Request Schema

```json
{
  "name": "Updated Name",
  "description": "Updated description"
}
```

All fields are optional; only provided fields are updated.

### Response Schema (Success: 200 OK)

Returns the full updated playlist (same schema as `GET /playlists/{playlistId}` without tracks).

### Validation Rules (Req 7.1)

| Field | Rule | Error Code |
|-------|------|------------|
| `name` | 1-100 characters, non-empty if provided | `INVALID_NAME` |
| `description` | 0-500 characters; `null` clears | `INVALID_DESCRIPTION` |

### Concurrency Handling

Use optimistic concurrency with RavenDB `@etag`:

```csharp
public async Task<Playlist> UpdatePlaylistAsync(
    string playlistId,
    string userId,
    UpdatePlaylistRequest request,
    CancellationToken ct = default)
{
    var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

    if (playlist is null)
        throw new PlaylistNotFoundException(playlistId);

    if (playlist.UserId != userId)
        throw new PlaylistAccessDeniedException(playlistId);

    if (request.Name is not null)
        playlist.Name = request.Name;

    if (request.HasDescription)
        playlist.Description = request.Description;

    playlist.UpdatedAt = DateTimeOffset.UtcNow;

    await _session.SaveChangesAsync(ct);
    return playlist;
}
```

### Rate Limiting

- Policy: `playlist-update`
- Default: 30 requests/minute per user

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-playlist-id` | Malformed ULID |
| `400` | `validation-error` | Name or description validation failed |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own playlist |
| `404` | `playlist-not-found` | Playlist does not exist |
| `409` | `concurrency-conflict` | Concurrent modification detected |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |

---

## 6. API Endpoint: `DELETE /playlists/{playlistId}`

### Request

- **Method:** `DELETE`
- **Path:** `/playlists/{playlistId}`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the playlist

### Response (Success: 204 No Content)

No response body.

### Deletion Semantics

Playlists are **hard-deleted immediately** (unlike tracks):
- Playlists don't contain actual data, only references
- No grace period or restoration needed
- Tracks are not affected by playlist deletion

```csharp
public async Task DeletePlaylistAsync(
    string playlistId,
    string userId,
    CancellationToken ct = default)
{
    var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

    if (playlist is null)
        throw new PlaylistNotFoundException(playlistId);

    if (playlist.UserId != userId)
        throw new PlaylistAccessDeniedException(playlistId);

    _session.Delete(playlist);
    await _session.SaveChangesAsync(ct);

    _logger.LogInformation(
        "Playlist {PlaylistId} deleted by user {UserId}",
        playlistId, userId);
}
```

### Rate Limiting

- Policy: `playlist-delete`
- Default: 20 requests/minute per user

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-playlist-id` | Malformed ULID |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own playlist |
| `404` | `playlist-not-found` | Playlist does not exist |

---

## 7. API Endpoint: `POST /playlists/{playlistId}/tracks`

### Request

- **Method:** `POST`
- **Path:** `/playlists/{playlistId}/tracks`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the playlist

### Request Schema

```json
{
  "trackIds": ["01HXK...", "01HXL..."],
  "position": null
}
```

| Field | Type | Description |
|-------|------|-------------|
| `trackIds` | string[] | Track IDs to add (1-100 per request) |
| `position` | int? | Insert position (null = append to end) |

### Response Schema (Success: 200 OK)

Returns the updated playlist with track summary.

### Validation Rules (Req 7.2, NF-2.4)

| Rule | Error Code |
|------|------------|
| Track IDs must be valid ULIDs | `INVALID_TRACK_ID` |
| Tracks must exist and be owned by user | `TRACK_NOT_FOUND` |
| Tracks must have `Status != Deleted` | `TRACK_DELETED` |
| Max 10,000 tracks per playlist | `PLAYLIST_TRACK_LIMIT_EXCEEDED` |
| Max 100 tracks per request | `BATCH_SIZE_EXCEEDED` |
| Position must be valid (0 to current track count) | `INVALID_POSITION` |

### Position Handling

```csharp
public async Task<Playlist> AddTracksToPlaylistAsync(
    string playlistId,
    string userId,
    AddTracksRequest request,
    CancellationToken ct = default)
{
    var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);
    // ... validation ...

    // Verify all tracks exist and are accessible
    var trackDocs = await _session.LoadAsync<Track>(
        request.TrackIds.Select(id => $"Tracks/{id}"),
        ct);

    foreach (var (trackId, track) in trackDocs)
    {
        if (track is null)
            throw new TrackNotFoundException(trackId);
        if (track.UserId != userId)
            throw new TrackAccessDeniedException(trackId);
        if (track.Status == TrackStatus.Deleted)
            throw new TrackDeletedException(trackId);
    }

    // Check quota
    if (playlist.TrackCount + request.TrackIds.Count > 10_000)
        throw new PlaylistTrackLimitExceededException(playlistId);

    var now = DateTimeOffset.UtcNow;
    var insertPosition = request.Position ?? playlist.Tracks.Count;

    // Shift existing tracks if inserting
    foreach (var entry in playlist.Tracks.Where(t => t.Position >= insertPosition))
    {
        entry.Position += request.TrackIds.Count;
    }

    // Add new tracks
    var newEntries = request.TrackIds.Select((id, i) => new PlaylistTrackEntry
    {
        Position = insertPosition + i,
        TrackId = id,
        AddedAt = now
    });

    playlist.Tracks.AddRange(newEntries);
    playlist.Tracks = playlist.Tracks.OrderBy(t => t.Position).ToList();

    // Update denormalized fields
    var totalDuration = TimeSpan.Zero;
    foreach (var entry in playlist.Tracks)
    {
        if (trackDocs.TryGetValue($"Tracks/{entry.TrackId}", out var track) && track is not null)
        {
            totalDuration += track.Duration;
        }
    }

    playlist.TrackCount = playlist.Tracks.Count;
    playlist.TotalDuration = totalDuration;
    playlist.UpdatedAt = now;

    await _session.SaveChangesAsync(ct);
    return playlist;
}
```

### Rate Limiting

- Policy: `playlist-tracks-add`
- Default: 30 requests/minute per user

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `validation-error` | Invalid track IDs, position, or batch size |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own playlist or track |
| `403` | `playlist-track-limit-exceeded` | Would exceed 10,000 tracks |
| `404` | `playlist-not-found` | Playlist does not exist |
| `404` | `track-not-found` | One or more tracks not found |
| `409` | `track-deleted` | One or more tracks are deleted |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |

---

## 8. API Endpoint: `DELETE /playlists/{playlistId}/tracks/{position}`

### Request

- **Method:** `DELETE`
- **Path:** `/playlists/{playlistId}/tracks/{position}`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the playlist

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `playlistId` | string | ULID identifier for the playlist |
| `position` | int | Position of track to remove (0-based) |

### Response (Success: 204 No Content)

No response body.

### Implementation

```csharp
public async Task RemoveTrackFromPlaylistAsync(
    string playlistId,
    string userId,
    int position,
    CancellationToken ct = default)
{
    var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

    if (playlist is null)
        throw new PlaylistNotFoundException(playlistId);

    if (playlist.UserId != userId)
        throw new PlaylistAccessDeniedException(playlistId);

    var trackToRemove = playlist.Tracks.FirstOrDefault(t => t.Position == position);
    if (trackToRemove is null)
        throw new PlaylistTrackNotFoundException(playlistId, position);

    playlist.Tracks.Remove(trackToRemove);

    // Reindex positions
    foreach (var entry in playlist.Tracks.Where(t => t.Position > position))
    {
        entry.Position--;
    }

    // Update denormalized fields (recalculate duration)
    await UpdatePlaylistAggregatesAsync(playlist, ct);

    playlist.UpdatedAt = DateTimeOffset.UtcNow;
    await _session.SaveChangesAsync(ct);
}
```

### Rate Limiting

- Policy: `playlist-tracks-remove`
- Default: 60 requests/minute per user

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-position` | Position out of range |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own playlist |
| `404` | `playlist-not-found` | Playlist does not exist |
| `404` | `track-not-in-playlist` | No track at specified position |

---

## 9. API Endpoint: `POST /playlists/{playlistId}/reorder`

### Request

- **Method:** `POST`
- **Path:** `/playlists/{playlistId}/reorder`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the playlist

### Request Schema

```json
{
  "moves": [
    { "from": 5, "to": 0 },
    { "from": 10, "to": 3 }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `moves` | object[] | Array of move operations (1-50 per request) |
| `moves[].from` | int | Current position of track |
| `moves[].to` | int | Target position |

### Response Schema (Success: 200 OK)

Returns the updated playlist with reordered tracks.

### Reorder Algorithm

Moves are applied sequentially:

```csharp
public async Task<Playlist> ReorderPlaylistTracksAsync(
    string playlistId,
    string userId,
    ReorderRequest request,
    CancellationToken ct = default)
{
    var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

    if (playlist is null)
        throw new PlaylistNotFoundException(playlistId);

    if (playlist.UserId != userId)
        throw new PlaylistAccessDeniedException(playlistId);

    // Validate all positions before applying
    foreach (var move in request.Moves)
    {
        if (move.From < 0 || move.From >= playlist.Tracks.Count)
            throw new InvalidPositionException(move.From);
        if (move.To < 0 || move.To >= playlist.Tracks.Count)
            throw new InvalidPositionException(move.To);
    }

    // Apply moves sequentially
    var tracks = playlist.Tracks.OrderBy(t => t.Position).ToList();

    foreach (var move in request.Moves)
    {
        var track = tracks[move.From];
        tracks.RemoveAt(move.From);
        tracks.Insert(move.To, track);
    }

    // Reassign positions
    for (var i = 0; i < tracks.Count; i++)
    {
        tracks[i].Position = i;
    }

    playlist.Tracks = tracks;
    playlist.UpdatedAt = DateTimeOffset.UtcNow;

    await _session.SaveChangesAsync(ct);
    return playlist;
}
```

### Rate Limiting

- Policy: `playlist-reorder`
- Default: 30 requests/minute per user

### Error Responses (RFC 7807)

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `validation-error` | Invalid move operations or batch size |
| `400` | `invalid-position` | Position out of range |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own playlist |
| `404` | `playlist-not-found` | Playlist does not exist |
| `409` | `concurrency-conflict` | Playlist modified concurrently |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |

---

## 10. Playlist Service Interface

```csharp
namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for playlist CRUD operations.
/// </summary>
public interface IPlaylistService
{
    /// <summary>
    /// Lists playlists for a user with pagination and sorting.
    /// </summary>
    Task<PagedResult<PlaylistListItem>> ListPlaylistsAsync(
        string userId,
        PlaylistListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new playlist.
    /// </summary>
    Task<PlaylistDetails> CreatePlaylistAsync(
        string userId,
        CreatePlaylistRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets playlist details by ID.
    /// </summary>
    Task<PlaylistDetails> GetPlaylistAsync(
        string playlistId,
        string userId,
        PlaylistDetailQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Updates playlist metadata.
    /// </summary>
    Task<PlaylistDetails> UpdatePlaylistAsync(
        string playlistId,
        string userId,
        UpdatePlaylistRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a playlist.
    /// </summary>
    Task DeletePlaylistAsync(
        string playlistId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Adds tracks to a playlist.
    /// </summary>
    Task<PlaylistDetails> AddTracksAsync(
        string playlistId,
        string userId,
        AddTracksRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a track from a playlist by position.
    /// </summary>
    Task RemoveTrackAsync(
        string playlistId,
        string userId,
        int position,
        CancellationToken ct = default);

    /// <summary>
    /// Reorders tracks within a playlist.
    /// </summary>
    Task<PlaylistDetails> ReorderTracksAsync(
        string playlistId,
        string userId,
        ReorderRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Removes deleted track references from all user playlists.
    /// Called by lifecycle worker after track physical deletion.
    /// </summary>
    Task RemoveDeletedTrackReferencesAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);
}
```

### Supporting Types

```csharp
public record PlaylistListQuery(
    string? Search = null,
    string SortBy = "updatedAt",
    string SortOrder = "desc",
    string? Cursor = null,
    int Limit = 20);

public record PlaylistDetailQuery(
    bool IncludeTracks = true,
    string? TrackCursor = null,
    int TrackLimit = 50);

public record CreatePlaylistRequest(
    string Name,
    string? Description = null);

public record UpdatePlaylistRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool HasDescription { get; init; } // Distinguishes null from omitted
}

public record AddTracksRequest(
    IReadOnlyList<string> TrackIds,
    int? Position = null);

public record ReorderRequest(
    IReadOnlyList<MoveOperation> Moves);

public record MoveOperation(int From, int To);

public record PlaylistListItem(
    string PlaylistId,
    string Name,
    string? Description,
    int TrackCount,
    TimeSpan TotalDuration,
    PlaylistVisibility Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record PlaylistDetails(
    string PlaylistId,
    string Name,
    string? Description,
    int TrackCount,
    TimeSpan TotalDuration,
    PlaylistVisibility Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PagedResult<PlaylistTrackItem>? Tracks);

public record PlaylistTrackItem(
    int Position,
    string TrackId,
    string Title,
    string? Artist,
    TimeSpan Duration,
    TrackStatus Status,
    DateTimeOffset AddedAt);
```

---

## 11. RavenDB Indexes

### Index: `Playlists_ByUserForSearch`

```csharp
public class Playlists_ByUserForSearch : AbstractIndexCreationTask<Playlist>
{
    public Playlists_ByUserForSearch()
    {
        Map = playlists => from playlist in playlists
                           select new
                           {
                               playlist.UserId,
                               playlist.Name,
                               playlist.TrackCount,
                               playlist.CreatedAt,
                               playlist.UpdatedAt,
                               SearchText = playlist.Name
                           };

        Index("SearchText", FieldIndexing.Search);
        Analyze("SearchText", "StandardAnalyzer");
    }
}
```

### Index: `Playlists_ByTrackReference`

For efficient removal of deleted tracks from playlists:

```csharp
public class Playlists_ByTrackReference : AbstractIndexCreationTask<Playlist>
{
    public class Result
    {
        public string UserId { get; set; } = string.Empty;
        public string PlaylistId { get; set; } = string.Empty;
        public string TrackId { get; set; } = string.Empty;
    }

    public Playlists_ByTrackReference()
    {
        Map = playlists => from playlist in playlists
                           from track in playlist.Tracks
                           select new Result
                           {
                               UserId = playlist.UserId,
                               PlaylistId = playlist.PlaylistId,
                               TrackId = track.TrackId
                           };
    }
}
```

---

## 12. Track Deletion Integration

When a track is physically deleted (via lifecycle worker), references must be removed from playlists.

### Lifecycle Worker Extension

```csharp
// In PhysicalDeletionService.ProcessDeletionsAsync

foreach (var track in tracksToDelete)
{
    try
    {
        // ... existing MinIO deletion ...

        // Remove from playlists
        await _playlistService.RemoveDeletedTrackReferencesAsync(
            track.TrackId,
            track.UserId,
            ct);

        // ... existing quota and document deletion ...
    }
    catch (Exception ex)
    {
        // ...
    }
}
```

### Implementation

```csharp
public async Task RemoveDeletedTrackReferencesAsync(
    string trackId,
    string userId,
    CancellationToken ct = default)
{
    // Find all playlists containing this track
    var affectedPlaylists = await _session
        .Query<Playlists_ByTrackReference.Result, Playlists_ByTrackReference>()
        .Where(r => r.UserId == userId && r.TrackId == trackId)
        .Select(r => r.PlaylistId)
        .Distinct()
        .ToListAsync(ct);

    foreach (var playlistId in affectedPlaylists)
    {
        var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);
        if (playlist is null) continue;

        // Remove all entries for this track
        playlist.Tracks.RemoveAll(t => t.TrackId == trackId);

        // Reindex positions
        for (var i = 0; i < playlist.Tracks.Count; i++)
        {
            playlist.Tracks[i].Position = i;
        }

        // Update denormalized fields
        playlist.TrackCount = playlist.Tracks.Count;
        // TotalDuration will need recalculation if track duration was cached
        playlist.UpdatedAt = DateTimeOffset.UtcNow;
    }

    await _session.SaveChangesAsync(ct);

    _logger.LogInformation(
        "Removed track {TrackId} from {Count} playlists for user {UserId}",
        trackId, affectedPlaylists.Count, userId);
}
```

---

## 13. Configuration

### `PlaylistOptions`

```csharp
public class PlaylistOptions
{
    public const string SectionName = "Playlists";

    /// <summary>
    /// Maximum playlists per user.
    /// Default: 200 (per NF-2.4).
    /// </summary>
    public int MaxPlaylistsPerUser { get; set; } = 200;

    /// <summary>
    /// Maximum tracks per playlist.
    /// Default: 10,000 (per NF-2.4).
    /// </summary>
    public int MaxTracksPerPlaylist { get; set; } = 10_000;

    /// <summary>
    /// Maximum tracks to add in a single request.
    /// Default: 100.
    /// </summary>
    public int MaxTracksPerAddRequest { get; set; } = 100;

    /// <summary>
    /// Maximum move operations per reorder request.
    /// Default: 50.
    /// </summary>
    public int MaxMovesPerReorderRequest { get; set; } = 50;

    /// <summary>
    /// Maximum playlist name length.
    /// Default: 100.
    /// </summary>
    public int MaxNameLength { get; set; } = 100;

    /// <summary>
    /// Maximum playlist description length.
    /// Default: 500.
    /// </summary>
    public int MaxDescriptionLength { get; set; } = 500;

    /// <summary>
    /// Default page size for playlist list.
    /// Default: 20.
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Maximum page size for playlist list.
    /// Default: 50.
    /// </summary>
    public int MaxPageSize { get; set; } = 50;
}
```

### `appsettings.json` Example

```json
{
  "Playlists": {
    "MaxPlaylistsPerUser": 200,
    "MaxTracksPerPlaylist": 10000,
    "MaxTracksPerAddRequest": 100,
    "MaxMovesPerReorderRequest": 50,
    "MaxNameLength": 100,
    "MaxDescriptionLength": 500,
    "DefaultPageSize": 20,
    "MaxPageSize": 50
  }
}
```

---

## 14. Endpoint Implementation

### `PlaylistEndpoints.cs`

```csharp
namespace NovaTuneApp.ApiService.Endpoints;

public static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/playlists")
            .RequireAuthorization(PolicyNames.ActiveUser)
            .WithTags("Playlists");

        group.MapGet("/", HandleListPlaylists)
            .WithName("ListPlaylists")
            .WithSummary("List user's playlists with search and pagination")
            .Produces<PagedResult<PlaylistListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("playlist-list");

        group.MapPost("/", HandleCreatePlaylist)
            .WithName("CreatePlaylist")
            .WithSummary("Create a new playlist")
            .Produces<PlaylistDetails>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireRateLimiting("playlist-create");

        group.MapGet("/{playlistId}", HandleGetPlaylist)
            .WithName("GetPlaylist")
            .WithSummary("Get playlist details with tracks")
            .Produces<PlaylistDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{playlistId}", HandleUpdatePlaylist)
            .WithName("UpdatePlaylist")
            .WithSummary("Update playlist metadata")
            .Produces<PlaylistDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("playlist-update");

        group.MapDelete("/{playlistId}", HandleDeletePlaylist)
            .WithName("DeletePlaylist")
            .WithSummary("Delete a playlist")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("playlist-delete");

        group.MapPost("/{playlistId}/tracks", HandleAddTracks)
            .WithName("AddTracksToPlaylist")
            .WithSummary("Add tracks to a playlist")
            .Produces<PlaylistDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireRateLimiting("playlist-tracks-add");

        group.MapDelete("/{playlistId}/tracks/{position:int}", HandleRemoveTrack)
            .WithName("RemoveTrackFromPlaylist")
            .WithSummary("Remove a track from a playlist by position")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("playlist-tracks-remove");

        group.MapPost("/{playlistId}/reorder", HandleReorderTracks)
            .WithName("ReorderPlaylistTracks")
            .WithSummary("Reorder tracks within a playlist")
            .Produces<PlaylistDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("playlist-reorder");
    }

    // Handler implementations...
}
```

---

## 15. Observability (NF-4.x)

### Logging

| Event | Level | Fields |
|-------|-------|--------|
| Playlist list requested | Debug | `UserId`, `Search`, `Limit` |
| Playlist created | Info | `PlaylistId`, `UserId`, `Name` |
| Playlist retrieved | Debug | `PlaylistId`, `UserId` |
| Playlist updated | Info | `PlaylistId`, `UserId`, `ChangedFields` |
| Playlist deleted | Info | `PlaylistId`, `UserId` |
| Tracks added to playlist | Info | `PlaylistId`, `UserId`, `TrackCount`, `Position` |
| Track removed from playlist | Info | `PlaylistId`, `UserId`, `Position` |
| Playlist reordered | Info | `PlaylistId`, `UserId`, `MoveCount` |
| Track references cleaned | Info | `TrackId`, `UserId`, `AffectedPlaylists` |
| Access denied | Warning | `PlaylistId`, `UserId`, `OwnerId` |
| Quota exceeded | Warning | `UserId`, `CurrentCount`, `Limit` |

### Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `playlist_list_requests_total` | Counter | `status` (success/error) |
| `playlist_create_requests_total` | Counter | `status` |
| `playlist_get_requests_total` | Counter | `status` |
| `playlist_update_requests_total` | Counter | `status` |
| `playlist_delete_requests_total` | Counter | `status` |
| `playlist_tracks_add_requests_total` | Counter | `status` |
| `playlist_tracks_remove_requests_total` | Counter | `status` |
| `playlist_reorder_requests_total` | Counter | `status` |
| `playlist_track_count` | Histogram | — |
| `playlist_quota_usage` | Gauge | `user_id` (sampled) |

### Tracing

- Propagate `CorrelationId` across all playlist operations
- Span hierarchy for add tracks:
  - `playlist.add_tracks` (parent)
    - `db.load_playlist` (child)
    - `db.load_tracks` (child)
    - `db.save_changes` (child)

---

## 16. Resilience (NF-1.4)

### Timeouts

| Operation | Timeout | Retries |
|-----------|---------|---------|
| RavenDB read (playlist) | 2s | 1 |
| RavenDB read (tracks for validation) | 3s | 1 |
| RavenDB write (update/delete) | 5s | 0 (use optimistic concurrency) |
| RavenDB query (list) | 5s | 1 |

### Circuit Breaker

| Dependency | Failure Threshold | Half-Open After |
|------------|-------------------|-----------------|
| RavenDB | 5 consecutive | 30s |

### Fail-Closed Behavior

All playlist operations fail closed:
- If RavenDB unavailable → `503 Service Unavailable`

---

## 17. Security Considerations

### Access Control

- All endpoints require authentication
- Users can only access their own playlists
- Users can only add their own tracks to playlists
- Visibility field is enforced (Private by default)

### Data Integrity

- Playlist operations use optimistic concurrency
- Track references validated before adding
- Position ordering maintained atomically
- Quota enforcement prevents resource exhaustion

### Future Sharing Considerations

The data model anticipates sharing with:
- `Visibility` enum for access levels
- Separate authorization checks for view vs. edit
- Potential `SharedWith` collection for explicit grants

---

## 18. Test Strategy

### Unit Tests

- `PlaylistService`: CRUD operations
- Pagination cursor encoding/decoding
- Query validation (sort fields, limit bounds)
- Position reindexing after add/remove/reorder
- Quota enforcement
- Track validation (exists, owned, not deleted)
- Exception mapping

### Integration Tests

- End-to-end playlist CRUD flow
- Add tracks with various positions
- Remove tracks and verify reindexing
- Reorder operations
- Playlist quota enforcement
- Track deletion cascade to playlists
- Concurrent modification handling
- Rate limiting enforcement

---

## 19. Implementation Tasks

### Models & Service

- [ ] Add `Playlist` and `PlaylistTrackEntry` models
- [ ] Add `PlaylistVisibility` enum
- [ ] Add `IPlaylistService` interface and implementation
- [ ] Add `PlaylistOptions` configuration class
- [ ] Add playlist-specific exceptions (`PlaylistNotFoundException`, etc.)

### Endpoints

- [ ] Add `PlaylistEndpoints.cs` with all endpoints
- [ ] Add query parameter types for list/detail endpoints
- [ ] Add request/response DTOs

### RavenDB

- [ ] Add `Playlists_ByUserForSearch` index
- [ ] Add `Playlists_ByTrackReference` index

### Infrastructure

- [ ] Add rate limiting policies: `playlist-list`, `playlist-create`, `playlist-update`, `playlist-delete`, `playlist-tracks-add`, `playlist-tracks-remove`, `playlist-reorder`
- [ ] Add playlist metrics to `NovaTuneMetrics`

### Lifecycle Integration

- [ ] Extend lifecycle worker to call `RemoveDeletedTrackReferencesAsync`
- [ ] Add `Playlists_ByTrackReference` index usage

### Testing

- [ ] Unit tests for `PlaylistService`
- [ ] Unit tests for position management
- [ ] Integration tests for playlist CRUD endpoints
- [ ] Integration tests for track deletion cascade

---

## Requirements Covered

- `Req 7.1` — Create, rename, and delete playlists
- `Req 7.2` — Add/remove/reorder tracks within playlists
- `Req 7.3` — Persist playlists in RavenDB with ownership and authorization
- `NF-2.4` — Quota enforcement (200 playlists, 10,000 tracks per playlist)
- `NF-2.5` — Rate limiting on playlist endpoints
- `NF-6.2` — Optimistic concurrency for concurrent updates

---

## Open Items

- [ ] Determine exact search configuration (analyzer, stemming) for playlist names
- [ ] Finalize rate limit values for playlist endpoints
- [ ] Consider bulk operations (add multiple playlists, delete multiple tracks)
- [ ] Consider playlist duplication/copy endpoint
- [ ] Design playlist sharing API when needed (future milestone)
- [ ] Consider playlist cover image support (future)
- [ ] Evaluate whether to cache playlist track counts

---

## Claude Skills

The following Claude Code skills are available to assist with implementing Stage 6:

### Planning

| Skill | Description |
|-------|-------------|
| `implement-playlists` | Comprehensive implementation plan with phases, files, and validation checklist |

### Core Patterns

| Skill | Use For | Stage 6 Components |
|-------|---------|-------------------|
| `add-api-endpoint` | Minimal API endpoint structure | All playlist CRUD endpoints |
| `add-cursor-pagination` | Cursor-based pagination for list endpoints | GET /playlists with stable pagination |
| `add-ravendb-index` | RavenDB index creation | `Playlists_ByUserForSearch`, `Playlists_ByTrackReference` |
| `add-rate-limiting` | Rate limiting policies | All playlist rate limit policies |
| `add-observability` | Metrics, logging, tracing | Playlist operation metrics and spans |

### Playlist-Specific

| Skill | Use For | Stage 6 Components |
|-------|---------|-------------------|
| `add-playlist-tracks` | Add/remove tracks with position management | POST/DELETE track endpoints |
| `add-playlist-reordering` | Track reordering with move operations | POST /playlists/{id}/reorder |

### Usage

Invoke skills using the Skill tool:
```
Skill: implement-playlists       # For planning overview
Skill: add-api-endpoint          # When implementing playlist endpoints
Skill: add-cursor-pagination     # When implementing GET /playlists
Skill: add-playlist-tracks       # When implementing track add/remove
Skill: add-playlist-reordering   # When implementing reorder endpoint
Skill: add-ravendb-index         # When creating playlist indexes
```

---

## Claude Agents

The following Claude Code agents are available for autonomous task execution:

### Implementation Agents

| Agent | Description | Tools |
|-------|-------------|-------|
| `playlist-api-implementer` | Implement playlist service, endpoints, and models | Read, Write, Edit, Bash, IDE diagnostics, Context7 |

### Testing Agent

| Agent | Description | Tools |
|-------|-------------|-------|
| `playlist-tester` | Write unit and integration tests for playlists | Read, Write, Edit, Bash, IDE diagnostics |

### Workflow Example

Use agents for structured implementation:

```
# Phase 1: Planning (single agent)
Task(subagent_type="playlist-api-implementer", prompt="Analyze PlaylistService requirements and create implementation plan")

# Phase 2: Implementation (focused agent)
Task(subagent_type="playlist-api-implementer", prompt="Implement Playlist model, PlaylistService, and PlaylistEndpoints")

# Phase 3: Testing (after implementation)
Task(subagent_type="playlist-tester", prompt="Write unit tests for PlaylistService")
Task(subagent_type="playlist-tester", prompt="Write integration tests for playlist endpoints")
```

### Agent Locations

All agents are defined in `.claude/agents/`:
- `playlist-api-implementer.md`
- `playlist-tester.md`
