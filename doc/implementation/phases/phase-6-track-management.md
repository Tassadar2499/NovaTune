# Phase 6: Track Management (FR 6.x)

> **Status:** ⏳ Pending
> **Dependencies:** Phases 3, 4, 5 (complete audio pipeline)
> **Milestone:** M4 - Management

## Objective

Provide comprehensive track browsing, editing, search, and deletion capabilities with optimized RavenDB queries.

---

## FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 6.1 | Track Browsing | P1 | Test |
| FR 6.2 | Metadata Editing | P2 | Test |
| FR 6.3 | Track Deletion | P1 | Test |
| FR 6.4 | Search | P2 | Test |
| FR 6.5 | Sorting | P2 | Test |

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-1.4 | Metadata Query Latency | <300ms p95 |
| NF-6.2 | RavenDB Integrity | Static indexes, migrations |
| NF-6.3 | Event Stream Governance | `track-metadata-updated` events |

---

## Tasks

### Task 6.1: Track Browsing API

**Priority:** P1 (Must-have)

Implement paginated track listing with cursor-based pagination.

#### Subtasks

- [ ] **6.1.1** Create `GET /api/v1/tracks`:
  ```csharp
  app.MapGet("/api/v1/tracks", async (
      [FromQuery] string? cursor,
      [FromQuery] int limit = 20,
      [FromQuery] string? sortBy = "createdAt",
      [FromQuery] string? sortOrder = "desc",
      [FromQuery] TrackStatus? status = null,
      ITrackService trackService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var userId = user.GetUserId();

      // Validate limit
      limit = Math.Clamp(limit, 10, 100);

      var result = await trackService.GetTracksAsync(
          userId,
          new TrackQueryOptions
          {
              Cursor = cursor,
              Limit = limit,
              SortBy = sortBy,
              SortOrder = sortOrder,
              Status = status
          },
          ct);

      return Results.Ok(new PagedResponse<TrackDto>
      {
          Items = result.Items.Select(t => t.ToDto()),
          NextCursor = result.NextCursor,
          HasMore = result.HasMore,
          TotalCount = result.TotalCount
      });
  })
  .RequireAuthorization()
  .WithName("ListTracks")
  .WithOpenApi();
  ```

- [ ] **6.1.2** Implement cursor-based pagination:
  ```csharp
  public sealed class CursorPaginator
  {
      public string EncodeCursor(Track track, string sortBy)
      {
          var value = sortBy switch
          {
              "createdAt" => track.CreatedAt.ToUnixTimeMilliseconds().ToString(),
              "title" => track.Title,
              "artist" => track.Artist ?? "",
              "duration" => track.Duration.TotalMilliseconds.ToString(),
              _ => track.CreatedAt.ToUnixTimeMilliseconds().ToString()
          };

          var cursor = $"{track.Id}|{value}";
          return Convert.ToBase64String(Encoding.UTF8.GetBytes(cursor));
      }

      public (string Id, string Value)? DecodeCursor(string? cursor)
      {
          if (string.IsNullOrEmpty(cursor))
              return null;

          try
          {
              var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
              var parts = decoded.Split('|', 2);
              return (parts[0], parts[1]);
          }
          catch
          {
              return null;
          }
      }
  }
  ```

- [ ] **6.1.3** Create `GET /api/v1/tracks/{id}`:
  ```csharp
  app.MapGet("/api/v1/tracks/{id}", async (
      string id,
      ITrackService trackService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var userId = user.GetUserId();
      var track = await trackService.GetTrackAsync(id, userId, ct);

      return track is not null
          ? Results.Ok(track.ToDto())
          : Results.NotFound();
  })
  .RequireAuthorization()
  .AddEndpointFilter<TrackOwnershipFilter>()
  .WithName("GetTrack");
  ```

- [ ] **6.1.4** Create TrackDto:
  ```csharp
  public record TrackDto(
      string Id,
      string Title,
      string? Artist,
      TimeSpan Duration,
      AudioMetadataDto? Metadata,
      string Status,
      DateTimeOffset CreatedAt,
      DateTimeOffset UpdatedAt,
      string? WaveformUrl);

  public record AudioMetadataDto(
      string Format,
      int Bitrate,
      int SampleRate,
      int Channels,
      long FileSizeBytes);
  ```

- [ ] **6.1.5** Write tests for pagination

#### Acceptance Criteria
- Pagination works correctly with cursor
- Sort options function properly
- Status filtering works
- Response time <300ms for 10K tracks

---

### Task 6.2: RavenDB Static Indexes

**Priority:** P1 (Must-have)

Create optimized indexes for track queries.

#### Subtasks

- [ ] **6.2.1** Create browse index:
  ```csharp
  public class Tracks_ByUserId_SortByUploadDate
      : AbstractIndexCreationTask<Track>
  {
      public class Result
      {
          public string UserId { get; set; } = string.Empty;
          public DateTimeOffset CreatedAt { get; set; }
          public string Title { get; set; } = string.Empty;
          public string? Artist { get; set; }
          public TimeSpan Duration { get; set; }
          public TrackStatus Status { get; set; }
      }

      public Tracks_ByUserId_SortByUploadDate()
      {
          Map = tracks => from track in tracks
                          select new Result
                          {
                              UserId = track.UserId,
                              CreatedAt = track.CreatedAt,
                              Title = track.Title,
                              Artist = track.Artist,
                              Duration = track.Duration,
                              Status = track.Status
                          };

          StoreAllFields(FieldStorage.Yes);
      }
  }
  ```

- [ ] **6.2.2** Create full-text search index:
  ```csharp
  public class Tracks_ByUserId_Search
      : AbstractIndexCreationTask<Track>
  {
      public class Result
      {
          public string UserId { get; set; } = string.Empty;
          public string Query { get; set; } = string.Empty;
          public string Title { get; set; } = string.Empty;
          public string? Artist { get; set; }
          public string[]? Tags { get; set; }
      }

      public Tracks_ByUserId_Search()
      {
          Map = tracks => from track in tracks
                          select new Result
                          {
                              UserId = track.UserId,
                              Query = string.Join(" ",
                                  track.Title,
                                  track.Artist ?? "",
                                  string.Join(" ", track.Tags ?? Array.Empty<string>())),
                              Title = track.Title,
                              Artist = track.Artist,
                              Tags = track.Tags
                          };

          Index(x => x.Query, FieldIndexing.Search);
          Analyze(x => x.Query, "StandardAnalyzer");

          Suggestion(x => x.Title);
      }
  }
  ```

- [ ] **6.2.3** Create statistics index:
  ```csharp
  public class Tracks_ByUserId_Stats
      : AbstractIndexCreationTask<Track, Tracks_ByUserId_Stats.Result>
  {
      public class Result
      {
          public string UserId { get; set; } = string.Empty;
          public int TotalTracks { get; set; }
          public long TotalDurationMs { get; set; }
          public long TotalSizeBytes { get; set; }
      }

      public Tracks_ByUserId_Stats()
      {
          Map = tracks => from track in tracks
                          select new Result
                          {
                              UserId = track.UserId,
                              TotalTracks = 1,
                              TotalDurationMs = (long)track.Duration.TotalMilliseconds,
                              TotalSizeBytes = track.Metadata != null
                                  ? track.Metadata.FileSizeBytes : 0
                          };

          Reduce = results => from result in results
                              group result by result.UserId into g
                              select new Result
                              {
                                  UserId = g.Key,
                                  TotalTracks = g.Sum(x => x.TotalTracks),
                                  TotalDurationMs = g.Sum(x => x.TotalDurationMs),
                                  TotalSizeBytes = g.Sum(x => x.TotalSizeBytes)
                              };
      }
  }
  ```

- [ ] **6.2.4** Deploy indexes on application startup

- [ ] **6.2.5** Monitor index performance

- [ ] **6.2.6** Write index tests

#### Acceptance Criteria
- Indexes created on startup
- Queries use indexes (no auto-index)
- Query performance <300ms p95

---

### Task 6.3: Search Implementation

**Priority:** P2 (Should-have)

Implement full-text search with relevance ranking.

#### Subtasks

- [ ] **6.3.1** Create `GET /api/v1/tracks/search`:
  ```csharp
  app.MapGet("/api/v1/tracks/search", async (
      [FromQuery] string q,
      [FromQuery] string? cursor,
      [FromQuery] int limit = 20,
      ITrackService trackService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      if (string.IsNullOrWhiteSpace(q))
          return Results.BadRequest("Search query required");

      var userId = user.GetUserId();

      var result = await trackService.SearchAsync(
          userId,
          q,
          new SearchOptions
          {
              Cursor = cursor,
              Limit = Math.Clamp(limit, 10, 100)
          },
          ct);

      return Results.Ok(new SearchResponse<TrackDto>
      {
          Items = result.Items.Select(t => t.ToDto()),
          NextCursor = result.NextCursor,
          HasMore = result.HasMore,
          Query = q,
          Suggestions = result.Suggestions
      });
  })
  .RequireAuthorization()
  .WithName("SearchTracks");
  ```

- [ ] **6.3.2** Implement search service:
  ```csharp
  public async Task<SearchResult<Track>> SearchAsync(
      string userId,
      string query,
      SearchOptions options,
      CancellationToken ct)
  {
      using var session = _store.OpenAsyncSession();

      var ravenQuery = session
          .Query<Tracks_ByUserId_Search.Result, Tracks_ByUserId_Search>()
          .Where(x => x.UserId == userId)
          .Search(x => x.Query, query)
          .OrderByScore();

      // Handle quoted phrases
      if (query.Contains('"'))
      {
          var phrases = ExtractPhrases(query);
          foreach (var phrase in phrases)
          {
              ravenQuery = ravenQuery.Search(x => x.Query, phrase, @operator: SearchOperator.And);
          }
      }

      // Get suggestions if few results
      var suggestions = await session
          .Query<Tracks_ByUserId_Search.Result, Tracks_ByUserId_Search>()
          .SuggestUsing(b => b.ByField(x => x.Title, query))
          .ExecuteAsync(ct);

      var tracks = await ravenQuery
          .Skip(options.Offset)
          .Take(options.Limit + 1)
          .OfType<Track>()
          .ToListAsync(ct);

      return new SearchResult<Track>
      {
          Items = tracks.Take(options.Limit).ToList(),
          HasMore = tracks.Count > options.Limit,
          Suggestions = suggestions.FirstOrDefault()?.Suggestions ?? Array.Empty<string>()
      };
  }
  ```

- [ ] **6.3.3** Implement tag autocomplete:
  ```csharp
  app.MapGet("/api/v1/tracks/tags/autocomplete", async (
      [FromQuery] string prefix,
      ITrackService trackService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var userId = user.GetUserId();
      var tags = await trackService.GetTagSuggestionsAsync(userId, prefix, ct);
      return Results.Ok(tags);
  }).RequireAuthorization();
  ```

- [ ] **6.3.4** Add search highlighting

- [ ] **6.3.5** Write search tests

#### Acceptance Criteria
- Full-text search works with stemming
- Quoted phrases work correctly
- Suggestions provided for typos
- Tag autocomplete works

---

### Task 6.4: Metadata Editing

**Priority:** P2 (Should-have)

Allow users to update track metadata.

#### Subtasks

- [ ] **6.4.1** Create `PATCH /api/v1/tracks/{id}`:
  ```csharp
  app.MapPatch("/api/v1/tracks/{id}", async (
      string id,
      UpdateTrackRequest request,
      [FromHeader(Name = "If-Match")] string? etag,
      ITrackService trackService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var userId = user.GetUserId();

      var result = await trackService.UpdateTrackAsync(
          id,
          userId,
          request,
          etag,
          ct);

      return result.Match(
          success => Results.Ok(success.ToDto()),
          error => error switch
          {
              NotFoundError => Results.NotFound(),
              ConcurrencyError => Results.Conflict(new { message = "Track was modified" }),
              _ => Results.Problem()
          });
  })
  .RequireAuthorization()
  .AddEndpointFilter<TrackOwnershipFilter>()
  .WithName("UpdateTrack");
  ```

- [ ] **6.4.2** Define update request:
  ```csharp
  public record UpdateTrackRequest(
      string? Title,
      string? Artist,
      string? Album,
      string[]? Tags,
      string? Description);

  public class UpdateTrackRequestValidator : AbstractValidator<UpdateTrackRequest>
  {
      public UpdateTrackRequestValidator()
      {
          RuleFor(x => x.Title)
              .MaximumLength(200)
              .When(x => x.Title is not null);

          RuleFor(x => x.Artist)
              .MaximumLength(200)
              .When(x => x.Artist is not null);

          RuleFor(x => x.Tags)
              .Must(tags => tags!.Length <= 20)
              .When(x => x.Tags is not null)
              .WithMessage("Maximum 20 tags allowed");
      }
  }
  ```

- [ ] **6.4.3** Implement optimistic concurrency:
  ```csharp
  public async Task<Result<Track, UpdateError>> UpdateTrackAsync(
      string trackId,
      string userId,
      UpdateTrackRequest request,
      string? etag,
      CancellationToken ct)
  {
      using var session = _store.OpenAsyncSession();

      var track = await session.LoadAsync<Track>(trackId, ct);
      if (track is null || track.UserId != userId)
          return new NotFoundError();

      // Check ETag
      var currentEtag = session.Advanced.GetChangeVectorFor(track);
      if (etag is not null && etag != currentEtag)
          return new ConcurrencyError();

      // Store previous version for history
      var previousVersion = new TrackVersion
      {
          Title = track.Title,
          Artist = track.Artist,
          Tags = track.Tags,
          ModifiedAt = track.UpdatedAt
      };

      track.EditHistory ??= new List<TrackVersion>();
      track.EditHistory.Insert(0, previousVersion);
      if (track.EditHistory.Count > 10)
          track.EditHistory.RemoveAt(10);

      // Apply updates
      if (request.Title is not null) track.Title = request.Title;
      if (request.Artist is not null) track.Artist = request.Artist;
      if (request.Tags is not null) track.Tags = request.Tags;
      if (request.Description is not null) track.Description = request.Description;

      track.UpdatedAt = _timeProvider.GetUtcNow();

      await session.SaveChangesAsync(ct);

      // Publish event
      await _eventPublisher.PublishAsync(new TrackMetadataUpdated(
          track.Id,
          userId,
          request,
          _timeProvider.GetUtcNow()));

      return track;
  }
  ```

- [ ] **6.4.4** Return ETag in response headers

- [ ] **6.4.5** Write concurrency tests

#### Acceptance Criteria
- Metadata updates work correctly
- Optimistic concurrency prevents conflicts
- Edit history maintained (last 10)
- Kafka event published

---

### Task 6.5: Track Deletion

**Priority:** P1 (Must-have)

Implement soft-delete with cascade and restoration.

#### Subtasks

- [ ] **6.5.1** Create `DELETE /api/v1/tracks/{id}`:
  ```csharp
  app.MapDelete("/api/v1/tracks/{id}", async (
      string id,
      ITrackService trackService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var userId = user.GetUserId();

      var result = await trackService.DeleteTrackAsync(id, userId, ct);

      return result.Match(
          success => Results.NoContent(),
          error => error switch
          {
              NotFoundError => Results.NotFound(),
              ForbiddenError => Results.Forbid(),
              _ => Results.Problem()
          });
  })
  .RequireAuthorization()
  .AddEndpointFilter<TrackOwnershipFilter>()
  .WithName("DeleteTrack");
  ```

- [ ] **6.5.2** Implement soft-delete:
  ```csharp
  public async Task<Result<Unit, DeleteError>> DeleteTrackAsync(
      string trackId,
      string userId,
      CancellationToken ct)
  {
      using var session = _store.OpenAsyncSession();

      var track = await session.LoadAsync<Track>(trackId, ct);
      if (track is null || track.UserId != userId)
          return new NotFoundError();

      // Soft delete
      track.Status = TrackStatus.Deleted;
      track.DeletedAt = _timeProvider.GetUtcNow();
      track.UpdatedAt = _timeProvider.GetUtcNow();

      await session.SaveChangesAsync(ct);

      // Cascade operations
      await CascadeDeleteAsync(track, ct);

      // Publish tombstone
      await _eventPublisher.PublishAsync(new TrackDeleted(
          track.Id,
          track.UserId,
          track.ObjectKey,
          track.DeletedAt!.Value));

      // Invalidate cache
      await _cache.RemoveAsync($"presigned:*:{trackId}", ct);

      return Unit.Default;
  }

  private async Task CascadeDeleteAsync(Track track, CancellationToken ct)
  {
      // Remove from playlists
      await _playlistService.RemoveTrackFromAllAsync(track.Id, track.UserId, ct);

      // Invalidate shares
      await _shareService.InvalidateTrackSharesAsync(track.Id, ct);
  }
  ```

- [ ] **6.5.3** Create `POST /api/v1/tracks/{id}/restore`:
  ```csharp
  app.MapPost("/api/v1/tracks/{id}/restore", async (
      string id,
      ITrackService trackService,
      ClaimsPrincipal user,
      CancellationToken ct) =>
  {
      var userId = user.GetUserId();

      var result = await trackService.RestoreTrackAsync(id, userId, ct);

      return result.Match(
          success => Results.Ok(success.ToDto()),
          error => error switch
          {
              NotFoundError => Results.NotFound(),
              ExpiredError => Results.BadRequest(new { message = "Recovery window expired" }),
              _ => Results.Problem()
          });
  })
  .RequireAuthorization()
  .WithName("RestoreTrack");
  ```

- [ ] **6.5.4** Implement 30-day recovery window:
  ```csharp
  public async Task<Result<Track, RestoreError>> RestoreTrackAsync(
      string trackId,
      string userId,
      CancellationToken ct)
  {
      using var session = _store.OpenAsyncSession();

      var track = await session.LoadAsync<Track>(trackId, ct);
      if (track is null || track.UserId != userId)
          return new NotFoundError();

      if (track.Status != TrackStatus.Deleted)
          return new InvalidStateError();

      // Check recovery window
      var deletedAge = _timeProvider.GetUtcNow() - track.DeletedAt!.Value;
      if (deletedAge > TimeSpan.FromDays(30))
          return new ExpiredError();

      track.Status = TrackStatus.Ready;
      track.DeletedAt = null;
      track.UpdatedAt = _timeProvider.GetUtcNow();

      await session.SaveChangesAsync(ct);

      return track;
  }
  ```

- [ ] **6.5.5** Write deletion and restoration tests

#### Acceptance Criteria
- Soft delete sets proper status
- Cascade removes from playlists/shares
- Kafka tombstone published
- Restoration works within 30 days
- Cache invalidated

---

### Task 6.6: Sorting and Filtering

**Priority:** P2 (Should-have)

Implement comprehensive sort and filter options.

#### Subtasks

- [ ] **6.6.1** Support sort fields:
  ```csharp
  public enum TrackSortField
  {
      CreatedAt,
      UpdatedAt,
      Title,
      Artist,
      Duration,
      PlayCount
  }

  public static IQueryable<Track> ApplySort(
      this IQueryable<Track> query,
      TrackSortField field,
      bool descending)
  {
      return (field, descending) switch
      {
          (TrackSortField.CreatedAt, true) => query.OrderByDescending(t => t.CreatedAt),
          (TrackSortField.CreatedAt, false) => query.OrderBy(t => t.CreatedAt),
          (TrackSortField.Title, true) => query.OrderByDescending(t => t.Title),
          (TrackSortField.Title, false) => query.OrderBy(t => t.Title),
          // ... other fields
          _ => query.OrderByDescending(t => t.CreatedAt)
      };
  }
  ```

- [ ] **6.6.2** Support filter options:
  ```csharp
  public record TrackFilters(
      string? TitleContains,
      string? ArtistContains,
      string[]? Tags,
      DateTimeOffset? CreatedAfter,
      DateTimeOffset? CreatedBefore,
      TimeSpan? MinDuration,
      TimeSpan? MaxDuration,
      TrackStatus? Status);
  ```

- [ ] **6.6.3** Implement filter application:
  ```csharp
  public static IQueryable<Track> ApplyFilters(
      this IQueryable<Track> query,
      TrackFilters filters)
  {
      if (filters.TitleContains is not null)
          query = query.Where(t => t.Title.Contains(filters.TitleContains));

      if (filters.ArtistContains is not null)
          query = query.Where(t => t.Artist != null &&
              t.Artist.Contains(filters.ArtistContains));

      if (filters.Tags is { Length: > 0 })
          query = query.Where(t => t.Tags != null &&
              filters.Tags.All(tag => t.Tags.Contains(tag)));

      if (filters.CreatedAfter is not null)
          query = query.Where(t => t.CreatedAt >= filters.CreatedAfter);

      if (filters.CreatedBefore is not null)
          query = query.Where(t => t.CreatedAt <= filters.CreatedBefore);

      if (filters.MinDuration is not null)
          query = query.Where(t => t.Duration >= filters.MinDuration);

      if (filters.MaxDuration is not null)
          query = query.Where(t => t.Duration <= filters.MaxDuration);

      if (filters.Status is not null)
          query = query.Where(t => t.Status == filters.Status);
      else
          query = query.Where(t => t.Status != TrackStatus.Deleted);

      return query;
  }
  ```

- [ ] **6.6.4** Write filter combination tests

#### Acceptance Criteria
- All sort fields work
- All filter combinations work
- Compound queries use indexes

---

## Infrastructure Setup

- [ ] RavenDB indexes deployed
- [ ] Kafka topic: `track-events`
- [ ] Index performance baseline established
- [ ] Query caching configured

---

## Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Pagination logic | Cursor encoding/decoding |
| Unit | Search query building | All filter combinations |
| Integration | Browse endpoints | All sort/filter options |
| Integration | Edit concurrency | Conflict resolution |
| Integration | Deletion cascade | All cleanup steps |
| Performance | Query latency | <300ms p95 with 10K tracks |

---

## Exit Criteria

- [ ] Browse returns paginated results correctly
- [ ] All sort and filter options work
- [ ] Search returns relevant results
- [ ] Metadata edits persist with versioning
- [ ] Deletion cascades to all related data
- [ ] Query latency <300ms p95 for 10K tracks/user
- [ ] Kafka events published for all changes

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Index rebuild time | Medium | Test migrations in staging |
| Search relevance tuning | Low | Iterative improvement |
| Concurrent edit conflicts | Medium | Clear error messaging |

---

## Navigation

← [Phase 5: Audio Streaming](phase-5-audio-streaming.md) | [Overview](../overview.md) | [Phase 7: Optional Features →](phase-7-optional-features.md)
