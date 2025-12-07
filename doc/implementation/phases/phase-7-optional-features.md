# Phase 7: Optional Features (FR 7.x, FR 8.x)

> **Status:** ⏳ Pending
> **Dependencies:** Phase 6 (Track Management)
> **Milestone:** M5 - Extended

## Objective

Extend platform capabilities with playlist management (Tier 1) and content sharing (Tier 2).

---

## Tier 1: Playlists (FR 7.x)

### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 7.1 | Playlist Creation | P2 | Test |
| FR 7.2 | Playlist Editing | P2 | Test |
| FR 7.3 | Playlist Reordering | P2 | E2E |
| FR 7.4 | Continuous Playback | P2 | E2E |
| FR 7.5 | Playlist Deletion | P2 | Test |

---

## Tasks - Tier 1: Playlists

### Task 7.1: Playlist Entity & Repository

**Priority:** P2 (Should-have)

Define playlist data model and storage.

#### Subtasks

- [ ] **7.1.1** Create `Playlist` entity:
  ```csharp
  public sealed class Playlist
  {
      public string Id { get; init; } = $"Playlists/{Guid.NewGuid()}";
      public string UserId { get; init; } = string.Empty;
      public string Name { get; set; } = string.Empty;
      public string? Description { get; set; }
      public List<PlaylistTrack> Tracks { get; set; } = new();
      public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
      public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
  }

  public sealed class PlaylistTrack
  {
      public string TrackId { get; init; } = string.Empty;
      public int Position { get; set; }
      public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;
  }
  ```

- [ ] **7.1.2** Create `IPlaylistRepository`:
  ```csharp
  public interface IPlaylistRepository
  {
      Task<Playlist?> GetByIdAsync(string id, CancellationToken ct);
      Task<IReadOnlyList<Playlist>> GetByUserIdAsync(string userId, CancellationToken ct);
      Task<Playlist> CreateAsync(Playlist playlist, CancellationToken ct);
      Task<Playlist> UpdateAsync(Playlist playlist, CancellationToken ct);
      Task DeleteAsync(string id, CancellationToken ct);
      Task<int> CountByUserIdAsync(string userId, CancellationToken ct);
  }
  ```

- [ ] **7.1.3** Create RavenDB index:
  ```csharp
  public class Playlists_ByUserId : AbstractIndexCreationTask<Playlist>
  {
      public Playlists_ByUserId()
      {
          Map = playlists => from playlist in playlists
                             select new
                             {
                                 playlist.UserId,
                                 playlist.Name,
                                 playlist.CreatedAt,
                                 TrackCount = playlist.Tracks.Count
                             };
      }
  }
  ```

- [ ] **7.1.4** Add validation constraints:
  - Name: 1-100 characters
  - Max playlists per user: 100
  - Max tracks per playlist: 500
  - Unique name per user

- [ ] **7.1.5** Write repository tests

#### Acceptance Criteria
- Entity supports all required fields
- Constraints enforced
- Index created for efficient queries

---

### Task 7.2: Playlist CRUD API

**Priority:** P2 (Should-have)

Implement playlist management endpoints.

#### Subtasks

- [ ] **7.2.1** Create `POST /api/v1/playlists`:
  ```csharp
  app.MapPost("/api/v1/playlists", async (
      CreatePlaylistRequest request,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var userId = user.GetUserId();

      // Check playlist limit
      var count = await playlistService.CountByUserAsync(userId, ct);
      if (count >= 100)
          return Results.BadRequest(new { message = "Playlist limit reached" });

      var result = await playlistService.CreateAsync(userId, request, ct);

      return result.Match(
          playlist => Results.Created(
              $"/api/v1/playlists/{playlist.Id}",
              playlist.ToDto()),
          error => Results.BadRequest(new { message = error.Message }));
  })
  .RequireAuthorization()
  .WithName("CreatePlaylist");

  public record CreatePlaylistRequest(
      string Name,
      string? Description,
      string[]? TrackIds);
  ```

- [ ] **7.2.2** Create `GET /api/v1/playlists`:
  ```csharp
  app.MapGet("/api/v1/playlists", async (
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var userId = user.GetUserId();
      var playlists = await playlistService.GetAllByUserAsync(userId, ct);
      return Results.Ok(playlists.Select(p => p.ToSummaryDto()));
  })
  .RequireAuthorization()
  .WithName("ListPlaylists");
  ```

- [ ] **7.2.3** Create `GET /api/v1/playlists/{id}`:
  ```csharp
  app.MapGet("/api/v1/playlists/{id}", async (
      string id,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var playlist = await playlistService.GetByIdAsync(id, user.GetUserId(), ct);
      return playlist is not null
          ? Results.Ok(playlist.ToDetailDto())
          : Results.NotFound();
  })
  .RequireAuthorization()
  .AddEndpointFilter<PlaylistOwnershipFilter>()
  .WithName("GetPlaylist");
  ```

- [ ] **7.2.4** Create `PATCH /api/v1/playlists/{id}`:
  ```csharp
  app.MapPatch("/api/v1/playlists/{id}", async (
      string id,
      UpdatePlaylistRequest request,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var result = await playlistService.UpdateAsync(
          id, user.GetUserId(), request, ct);
      return result.Match(
          playlist => Results.Ok(playlist.ToDto()),
          error => Results.NotFound());
  })
  .RequireAuthorization()
  .AddEndpointFilter<PlaylistOwnershipFilter>()
  .WithName("UpdatePlaylist");
  ```

- [ ] **7.2.5** Create `DELETE /api/v1/playlists/{id}`:
  ```csharp
  app.MapDelete("/api/v1/playlists/{id}", async (
      string id,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      await playlistService.DeleteAsync(id, user.GetUserId(), ct);
      return Results.NoContent();
  })
  .RequireAuthorization()
  .AddEndpointFilter<PlaylistOwnershipFilter>()
  .WithName("DeletePlaylist");
  ```

- [ ] **7.2.6** Write API integration tests

#### Acceptance Criteria
- All CRUD operations work
- Ownership verified
- Playlist limit enforced

---

### Task 7.3: Playlist Track Management

**Priority:** P2 (Should-have)

Implement track add/remove/reorder operations.

#### Subtasks

- [ ] **7.3.1** Create `POST /api/v1/playlists/{id}/tracks`:
  ```csharp
  app.MapPost("/api/v1/playlists/{id}/tracks", async (
      string id,
      AddTracksRequest request,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var result = await playlistService.AddTracksAsync(
          id, user.GetUserId(), request.TrackIds, ct);

      return result.Match(
          playlist => Results.Ok(playlist.ToDetailDto()),
          error => error switch
          {
              PlaylistFullError => Results.BadRequest(new { message = "Playlist is full" }),
              TrackNotFoundError e => Results.BadRequest(new { message = $"Track not found: {e.TrackId}" }),
              _ => Results.Problem()
          });
  })
  .RequireAuthorization()
  .AddEndpointFilter<PlaylistOwnershipFilter>()
  .WithName("AddTracksToPlaylist");

  public record AddTracksRequest(string[] TrackIds);
  ```

- [ ] **7.3.2** Implement track addition:
  ```csharp
  public async Task<Result<Playlist, PlaylistError>> AddTracksAsync(
      string playlistId,
      string userId,
      string[] trackIds,
      CancellationToken ct)
  {
      using var session = _store.OpenAsyncSession();

      var playlist = await session.LoadAsync<Playlist>(playlistId, ct);
      if (playlist is null || playlist.UserId != userId)
          return new NotFoundError();

      // Check capacity
      if (playlist.Tracks.Count + trackIds.Length > 500)
          return new PlaylistFullError();

      // Verify all tracks exist and belong to user
      var tracks = await session.LoadAsync<Track>(trackIds, ct);
      foreach (var (trackId, track) in tracks)
      {
          if (track is null || track.UserId != userId)
              return new TrackNotFoundError(trackId);
      }

      // Add tracks at end
      var currentMax = playlist.Tracks.Any()
          ? playlist.Tracks.Max(t => t.Position)
          : -1;

      foreach (var (trackId, index) in trackIds.Select((id, i) => (id, i)))
      {
          playlist.Tracks.Add(new PlaylistTrack
          {
              TrackId = trackId,
              Position = currentMax + index + 1
          });
      }

      playlist.UpdatedAt = _timeProvider.GetUtcNow();
      await session.SaveChangesAsync(ct);

      return playlist;
  }
  ```

- [ ] **7.3.3** Create `DELETE /api/v1/playlists/{id}/tracks/{trackId}`:
  ```csharp
  app.MapDelete("/api/v1/playlists/{id}/tracks/{trackId}", async (
      string id,
      string trackId,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var result = await playlistService.RemoveTrackAsync(
          id, user.GetUserId(), trackId, ct);
      return result.Match(
          _ => Results.NoContent(),
          _ => Results.NotFound());
  })
  .RequireAuthorization()
  .AddEndpointFilter<PlaylistOwnershipFilter>()
  .WithName("RemoveTrackFromPlaylist");
  ```

- [ ] **7.3.4** Create `PUT /api/v1/playlists/{id}/order`:
  ```csharp
  app.MapPut("/api/v1/playlists/{id}/order", async (
      string id,
      ReorderRequest request,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var result = await playlistService.ReorderAsync(
          id, user.GetUserId(), request.TrackIds, ct);
      return result.Match(
          playlist => Results.Ok(playlist.ToDetailDto()),
          _ => Results.NotFound());
  })
  .RequireAuthorization()
  .AddEndpointFilter<PlaylistOwnershipFilter>()
  .WithName("ReorderPlaylist");

  public record ReorderRequest(string[] TrackIds);
  ```

- [ ] **7.3.5** Implement reordering:
  ```csharp
  public async Task<Result<Playlist, PlaylistError>> ReorderAsync(
      string playlistId,
      string userId,
      string[] orderedTrackIds,
      CancellationToken ct)
  {
      using var session = _store.OpenAsyncSession();

      var playlist = await session.LoadAsync<Playlist>(playlistId, ct);
      if (playlist is null || playlist.UserId != userId)
          return new NotFoundError();

      // Verify all tracks are present
      var existingIds = playlist.Tracks.Select(t => t.TrackId).ToHashSet();
      if (!orderedTrackIds.ToHashSet().SetEquals(existingIds))
          return new InvalidOrderError();

      // Reorder
      var trackMap = playlist.Tracks.ToDictionary(t => t.TrackId);
      for (var i = 0; i < orderedTrackIds.Length; i++)
      {
          trackMap[orderedTrackIds[i]].Position = i;
      }

      playlist.Tracks = playlist.Tracks.OrderBy(t => t.Position).ToList();
      playlist.UpdatedAt = _timeProvider.GetUtcNow();

      await session.SaveChangesAsync(ct);

      return playlist;
  }
  ```

- [ ] **7.3.6** Support batch add/remove

- [ ] **7.3.7** Write E2E drag-drop reorder tests

#### Acceptance Criteria
- Add/remove tracks works
- Reorder preserves all tracks
- Duplicate tracks allowed
- Batch operations work

---

### Task 7.4: Continuous Playback Support

**Priority:** P2 (Should-have)

Implement API support for continuous playlist playback.

#### Subtasks

- [ ] **7.4.1** Create `GET /api/v1/playlists/{id}/queue`:
  ```csharp
  app.MapGet("/api/v1/playlists/{id}/queue", async (
      string id,
      [FromQuery] int startPosition = 0,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var queue = await playlistService.GetQueueAsync(
          id, user.GetUserId(), startPosition, ct);

      return queue is not null
          ? Results.Ok(queue)
          : Results.NotFound();
  })
  .RequireAuthorization()
  .WithName("GetPlaybackQueue");
  ```

- [ ] **7.4.2** Return queue with prefetch URLs:
  ```csharp
  public record PlaybackQueue(
      PlaylistTrackDto Current,
      PlaylistTrackDto? Next,
      string? NextStreamUrl,    // Prefetched presigned URL
      int TotalTracks,
      bool HasMore);

  public async Task<PlaybackQueue?> GetQueueAsync(
      string playlistId,
      string userId,
      int currentPosition,
      CancellationToken ct)
  {
      var playlist = await _repository.GetByIdAsync(playlistId, ct);
      if (playlist is null || playlist.UserId != userId)
          return null;

      var orderedTracks = playlist.Tracks.OrderBy(t => t.Position).ToList();

      var currentTrack = orderedTracks.ElementAtOrDefault(currentPosition);
      if (currentTrack is null)
          return null;

      var nextTrack = orderedTracks.ElementAtOrDefault(currentPosition + 1);

      // Prefetch next track URL
      string? nextUrl = null;
      if (nextTrack is not null)
      {
          var presigned = await _presignedUrls.GenerateAsync(
              nextTrack.TrackId, userId, ct: ct);
          nextUrl = presigned.Url;
      }

      return new PlaybackQueue(
          Current: await EnrichTrackDto(currentTrack, ct),
          Next: nextTrack is not null ? await EnrichTrackDto(nextTrack, ct) : null,
          NextStreamUrl: nextUrl,
          TotalTracks: orderedTracks.Count,
          HasMore: currentPosition + 1 < orderedTracks.Count);
  }
  ```

- [ ] **7.4.3** Support shuffle mode:
  ```csharp
  app.MapGet("/api/v1/playlists/{id}/shuffle", async (
      string id,
      [FromQuery] int? seed,
      IPlaylistService playlistService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var shuffled = await playlistService.GetShuffledAsync(
          id, user.GetUserId(), seed, ct);
      return shuffled is not null
          ? Results.Ok(shuffled)
          : Results.NotFound();
  })
  .RequireAuthorization()
  .WithName("GetShuffledPlaylist");
  ```

- [ ] **7.4.4** Support loop modes:
  - None: Stop after last track
  - Playlist: Restart from beginning
  - Track: Repeat current track

- [ ] **7.4.5** Write E2E playback tests

#### Acceptance Criteria
- Queue returns current and next track
- Next URL prefetched for gapless playback
- Shuffle generates consistent order with seed
- Loop modes work correctly

---

## Tier 2: Sharing (FR 8.x)

### FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 8.1 | Share Links | P3 | Test |
| FR 8.2 | Visibility Controls | P3 | Test |
| FR 8.3 | Secure Streaming | P2 | Test |

---

## Tasks - Tier 2: Sharing

### Task 7.5: Share Entity & Repository

**Priority:** P3 (Nice-to-have)

Define share data model and storage.

#### Subtasks

- [ ] **7.5.1** Create `Share` entity:
  ```csharp
  public sealed class Share
  {
      public string Id { get; init; } = GenerateShareId();
      public string OwnerId { get; init; } = string.Empty;
      public ShareResourceType ResourceType { get; init; }
      public string ResourceId { get; init; } = string.Empty;
      public ShareVisibility Visibility { get; set; } = ShareVisibility.Link;
      public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
      public DateTimeOffset? ExpiresAt { get; set; }
      public int AccessCount { get; set; }
      public DateTimeOffset? LastAccessedAt { get; set; }
      public bool IsRevoked { get; set; }

      private static string GenerateShareId()
      {
          // 12-character alphanumeric
          var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
          var id = new char[12];
          for (var i = 0; i < 12; i++)
              id[i] = chars[Random.Shared.Next(chars.Length)];
          return new string(id);
      }
  }

  public enum ShareResourceType { Track, Playlist }
  public enum ShareVisibility { Private, Link, Public }
  ```

- [ ] **7.5.2** Create `IShareRepository`

- [ ] **7.5.3** Create RavenDB indexes

- [ ] **7.5.4** Add constraints:
  - Max active shares per user: 100
  - Default expiry: 30 days

- [ ] **7.5.5** Write repository tests

#### Acceptance Criteria
- Share IDs are 12-char alphanumeric
- Constraints enforced
- Expiry tracked

---

### Task 7.6: Share API

**Priority:** P3 (Nice-to-have)

Implement share management endpoints.

#### Subtasks

- [ ] **7.6.1** Create `POST /api/v1/shares`:
  ```csharp
  app.MapPost("/api/v1/shares", async (
      CreateShareRequest request,
      IShareService shareService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var result = await shareService.CreateAsync(
          user.GetUserId(), request, ct);

      return result.Match(
          share => Results.Created(
              $"/share/{share.Id}",
              new
              {
                  shareId = share.Id,
                  url = $"https://novatune.com/share/{share.Id}",
                  expiresAt = share.ExpiresAt
              }),
          error => Results.BadRequest(new { message = error.Message }));
  })
  .RequireAuthorization()
  .WithName("CreateShare");

  public record CreateShareRequest(
      ShareResourceType ResourceType,
      string ResourceId,
      ShareVisibility? Visibility,
      int? ExpiryDays);
  ```

- [ ] **7.6.2** Create `PATCH /api/v1/shares/{id}`:
  ```csharp
  app.MapPatch("/api/v1/shares/{id}", async (
      string id,
      UpdateShareRequest request,
      IShareService shareService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var result = await shareService.UpdateAsync(
          id, user.GetUserId(), request, ct);
      return result.Match(
          share => Results.Ok(share),
          _ => Results.NotFound());
  })
  .RequireAuthorization()
  .WithName("UpdateShare");
  ```

- [ ] **7.6.3** Create `DELETE /api/v1/shares/{id}`:
  ```csharp
  app.MapDelete("/api/v1/shares/{id}", async (
      string id,
      IShareService shareService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      await shareService.RevokeAsync(id, user.GetUserId(), ct);
      return Results.NoContent();
  })
  .RequireAuthorization()
  .WithName("RevokeShare");
  ```

- [ ] **7.6.4** Create `GET /api/v1/shares`:
  ```csharp
  app.MapGet("/api/v1/shares", async (
      IShareService shareService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var shares = await shareService.GetByUserAsync(user.GetUserId(), ct);
      return Results.Ok(shares);
  })
  .RequireAuthorization()
  .WithName("ListShares");
  ```

- [ ] **7.6.5** Write API tests

#### Acceptance Criteria
- Share CRUD works
- Share URLs generated correctly
- Revocation works

---

### Task 7.7: Public Share Access

**Priority:** P2 (Should-have)

Implement anonymous access via share links.

#### Subtasks

- [ ] **7.7.1** Create `GET /share/{shareId}`:
  ```csharp
  app.MapGet("/share/{shareId}", async (
      string shareId,
      IShareService shareService,
      CancellationToken ct) =>
  {
      var result = await shareService.GetPublicAsync(shareId, ct);

      return result.Match(
          content => Results.Ok(content),
          error => error switch
          {
              ShareNotFoundError => Results.NotFound(),
              ShareExpiredError => Results.Gone(),
              ShareRevokedError => Results.Gone(),
              _ => Results.Problem()
          });
  })
  .AllowAnonymous()
  .WithName("GetSharedContent");
  ```

- [ ] **7.7.2** Create `GET /share/{shareId}/stream`:
  ```csharp
  app.MapGet("/share/{shareId}/stream", async (
      string shareId,
      HttpContext context,
      IShareService shareService,
      IStreamingService streamingService,
      CancellationToken ct) =>
  {
      // Validate share
      var share = await shareService.ValidateAsync(shareId, ct);
      if (share is null)
          return Results.NotFound();

      // Check rate limit
      var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
      if (!await shareService.CheckRateLimitAsync(shareId, clientIp, ct))
          return Results.StatusCode(429); // Too Many Requests

      // Get stream
      var stream = await streamingService.GetStreamForShareAsync(
          share.ResourceId, ct);

      // Record access
      await shareService.RecordAccessAsync(shareId, clientIp, ct);

      return Results.Stream(stream.Content, stream.ContentType,
          enableRangeProcessing: true);
  })
  .AllowAnonymous()
  .WithName("StreamSharedContent");
  ```

- [ ] **7.7.3** Implement rate limiting:
  ```csharp
  public async Task<bool> CheckRateLimitAsync(
      string shareId,
      string clientIp,
      CancellationToken ct)
  {
      var key = $"share-rate:{shareId}:{clientIp}";
      var count = await _cache.IncrementAsync(key, ct);

      if (count == 1)
          await _cache.SetExpiryAsync(key, TimeSpan.FromHours(1), ct);

      return count <= 50; // 50 streams per hour per IP per share
  }
  ```

- [ ] **7.7.4** Track access analytics

- [ ] **7.7.5** Write anonymous access tests

#### Acceptance Criteria
- Anonymous streaming works via share link
- Rate limiting prevents abuse (50/hour)
- Access count tracked
- Expired/revoked shares return 410

---

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-6.2 | RavenDB Integrity | Playlist/share schemas |
| NF-7.1 | Responsiveness | Drag-drop reordering |
| NF-7.2 | Feedback | Playback transitions |

---

## Infrastructure Setup

- [ ] RavenDB indexes for playlists and shares
- [ ] Rate limiting for share endpoints
- [ ] Share analytics tracking

---

## Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Playlist operations | All CRUD |
| Unit | Share ID generation | Uniqueness |
| Integration | Playlist track management | Add/remove/reorder |
| Integration | Share access | Valid/expired/revoked |
| E2E | Continuous playback | Auto-advance, prefetch |
| E2E | Share streaming | Anonymous access |

---

## Exit Criteria

**Tier 1 (Playlists):**
- [ ] Playlist CRUD operations work
- [ ] Track add/remove/reorder functional
- [ ] Continuous playback with prefetch
- [ ] Loop and shuffle modes work
- [ ] Playlist limit (100) enforced
- [ ] Track limit (500) enforced

**Tier 2 (Sharing):**
- [ ] Share links generated correctly
- [ ] Visibility controls enforced
- [ ] Anonymous streaming via share link works
- [ ] Rate limiting prevents abuse (50/hour)
- [ ] Share expiry enforced

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Playlist size impacts query performance | Medium | Lazy loading, pagination |
| Share link abuse | High | Rate limiting, analytics |
| Orphaned shares after track deletion | Low | Cascade invalidation |

---

## Navigation

← [Phase 6: Track Management](phase-6-track-management.md) | [Overview](../overview.md) | [Phase 8: Observability & Admin →](phase-8-observability-admin.md)
