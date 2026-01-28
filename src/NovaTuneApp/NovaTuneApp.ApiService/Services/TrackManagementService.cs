using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Outbox;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for track CRUD operations with soft-delete support.
/// </summary>
public class TrackManagementService : ITrackManagementService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IOptions<TrackManagementOptions> _trackOptions;
    private readonly IOptions<NovaTuneOptions> _novaTuneOptions;
    private readonly IStreamingService _streamingService;
    private readonly ILogger<TrackManagementService> _logger;

    private static readonly HashSet<string> ValidSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt", "updatedAt", "title", "artist", "duration"
    };

    public TrackManagementService(
        IAsyncDocumentSession session,
        IOptions<TrackManagementOptions> trackOptions,
        IOptions<NovaTuneOptions> novaTuneOptions,
        IStreamingService streamingService,
        ILogger<TrackManagementService> logger)
    {
        _session = session;
        _trackOptions = trackOptions;
        _novaTuneOptions = novaTuneOptions;
        _streamingService = streamingService;
        _logger = logger;
    }

    public async Task<PagedResult<TrackListItem>> ListTracksAsync(
        string userId,
        TrackListQuery query,
        CancellationToken ct = default)
    {
        using var activity = NovaTuneActivitySource.StartTrackList(userId, query.Search);
        var sw = Stopwatch.StartNew();

        _logger.LogDebug(
            "Listing tracks for user {UserId}: Search={Search}, Status={Status}, Limit={Limit}",
            userId, query.Search, query.Status, query.Limit);

        try
        {
            var limit = Math.Clamp(query.Limit, 1, _trackOptions.Value.MaxPageSize);
            var sortBy = ValidSortFields.Contains(query.SortBy) ? query.SortBy : "createdAt";
            var sortDescending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

            var ravenQuery = _session
                .Query<Track, Tracks_ByUserForSearch>()
                .Where(t => t.UserId == userId);

            // Filter by status
            if (query.Status.HasValue)
            {
                ravenQuery = ravenQuery.Where(t => t.Status == query.Status.Value);
            }
            else if (!query.IncludeDeleted)
            {
                ravenQuery = ravenQuery.Where(t => t.Status != TrackStatus.Deleted);
            }

            // Full-text search (chained Search calls use OR by default)
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                ravenQuery = ravenQuery
                    .Search(t => t.Title, $"*{query.Search}*")
                    .Search(t => t.Artist, $"*{query.Search}*");
            }

            // Apply sorting
            ravenQuery = ApplySorting(ravenQuery, sortBy, sortDescending);

            // Apply cursor
            if (!string.IsNullOrEmpty(query.Cursor))
            {
                var cursor = DecodeCursor(query.Cursor);
                ravenQuery = ApplyCursor(ravenQuery, cursor, sortBy, sortDescending);
            }

            // Fetch one extra to determine HasMore
            var tracks = await ravenQuery.Take(limit + 1).ToListAsync(ct);

            var hasMore = tracks.Count > limit;
            var items = tracks.Take(limit).Select(MapToListItem).ToList();

            string? nextCursor = null;
            if (hasMore && items.Count > 0)
            {
                var lastTrack = tracks[limit - 1];
                nextCursor = EncodeCursor(lastTrack, sortBy);
            }

            // Get approximate total count
            var totalCount = await GetApproximateCountAsync(userId, query, ct);

            activity?.SetStatus(ActivityStatusCode.Ok);
            NovaTuneMetrics.RecordTrackListRequest("success", sw.Elapsed.TotalMilliseconds);

            return new PagedResult<TrackListItem>(items, nextCursor, totalCount, hasMore);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NovaTuneMetrics.RecordTrackListRequest("error", sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    public async Task<TrackDetails> GetTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        using var activity = NovaTuneActivitySource.StartTrackGet(trackId, userId);

        _logger.LogDebug(
            "Getting track {TrackId} for user {UserId}",
            trackId, userId);

        try
        {
            var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);

            if (track is null)
            {
                NovaTuneMetrics.RecordTrackGetRequest("not_found");
                throw new TrackNotFoundException(trackId);
            }

            if (track.UserId != userId)
            {
                _logger.LogWarning(
                    "Access denied: user {UserId} attempted to access track {TrackId} owned by {OwnerId}",
                    userId, trackId, track.UserId);
                NovaTuneMetrics.RecordTrackGetRequest("access_denied");
                throw new TrackAccessDeniedException(trackId);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            NovaTuneMetrics.RecordTrackGetRequest("success");

            return MapToDetails(track);
        }
        catch (Exception ex) when (ex is not TrackNotFoundException and not TrackAccessDeniedException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NovaTuneMetrics.RecordTrackGetRequest("error");
            throw;
        }
    }

    public async Task<TrackDetails> UpdateTrackAsync(
        string trackId,
        string userId,
        UpdateTrackRequest request,
        CancellationToken ct = default)
    {
        using var activity = NovaTuneActivitySource.StartTrackUpdate(trackId, userId);

        try
        {
            Track? track;
            using (NovaTuneActivitySource.StartDbLoadTrack(trackId))
            {
                track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);
            }

            if (track is null)
            {
                NovaTuneMetrics.RecordTrackUpdateRequest("not_found");
                throw new TrackNotFoundException(trackId);
            }

            if (track.UserId != userId)
            {
                _logger.LogWarning(
                    "Access denied: user {UserId} attempted to update track {TrackId} owned by {OwnerId}",
                    userId, trackId, track.UserId);
                NovaTuneMetrics.RecordTrackUpdateRequest("access_denied");
                throw new TrackAccessDeniedException(trackId);
            }

            if (track.Status == TrackStatus.Deleted)
            {
                NovaTuneMetrics.RecordTrackUpdateRequest("deleted");
                throw new TrackDeletedException(trackId, track.DeletedAt);
            }

            // Build list of changed fields for logging
            var changedFields = new List<string>();

            // Merge policy: only update provided fields
            using (NovaTuneActivitySource.StartDbUpdateStatus(trackId))
            {
                if (request.Title is not null)
                {
                    track.Title = request.Title;
                    changedFields.Add("title");
                }

                if (request.Artist is not null)
                {
                    track.Artist = request.Artist == "" ? null : request.Artist;
                    changedFields.Add("artist");
                }

                track.UpdatedAt = DateTimeOffset.UtcNow;

                await _session.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "Track {TrackId} updated by user {UserId}, changed fields: {ChangedFields}",
                trackId, userId, string.Join(", ", changedFields));

            activity?.SetStatus(ActivityStatusCode.Ok);
            NovaTuneMetrics.RecordTrackUpdateRequest("success");

            return MapToDetails(track);
        }
        catch (Exception ex) when (ex is not TrackNotFoundException
            and not TrackAccessDeniedException
            and not TrackDeletedException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NovaTuneMetrics.RecordTrackUpdateRequest("error");
            throw;
        }
    }

    public async Task DeleteTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        using var activity = NovaTuneActivitySource.StartTrackSoftDelete(trackId, userId);

        try
        {
            // Load track
            Track? track;
            using (NovaTuneActivitySource.StartDbLoadTrack(trackId))
            {
                track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);
            }

            if (track is null)
            {
                NovaTuneMetrics.RecordTrackDeleteRequest("not_found");
                throw new TrackNotFoundException(trackId);
            }

            if (track.UserId != userId)
            {
                _logger.LogWarning(
                    "Access denied: user {UserId} attempted to delete track {TrackId} owned by {OwnerId}",
                    userId, trackId, track.UserId);
                NovaTuneMetrics.RecordTrackDeleteRequest("access_denied");
                throw new TrackAccessDeniedException(trackId);
            }

            if (track.Status == TrackStatus.Deleted)
            {
                NovaTuneMetrics.RecordTrackDeleteRequest("already_deleted");
                throw new TrackAlreadyDeletedException(trackId);
            }

            var now = DateTimeOffset.UtcNow;
            var scheduledDeletion = now.Add(_trackOptions.Value.DeletionGracePeriod);
            var correlationId = Activity.Current?.Id ?? Ulid.NewUlid().ToString();

            // Update track status
            using (NovaTuneActivitySource.StartDbUpdateStatus(trackId))
            {
                track.StatusBeforeDeletion = track.Status;
                track.Status = TrackStatus.Deleted;
                track.DeletedAt = now;
                track.ScheduledDeletionAt = scheduledDeletion;
                track.UpdatedAt = now;
            }

            // Create TrackDeletedEvent for lifecycle worker
            var evt = new TrackDeletedEvent
            {
                TrackId = trackId,
                UserId = userId,
                ObjectKey = track.ObjectKey,
                WaveformObjectKey = track.WaveformObjectKey,
                FileSizeBytes = track.FileSizeBytes,
                DeletedAt = now,
                ScheduledDeletionAt = scheduledDeletion,
                CorrelationId = correlationId,
                Timestamp = now
            };

            // Write outbox message (transactional with track update)
            using (NovaTuneActivitySource.StartOutboxWrite(nameof(TrackDeletedEvent)))
            {
                var topicPrefix = _novaTuneOptions.Value.TopicPrefix;
                var outbox = new OutboxMessage
                {
                    Id = $"OutboxMessages/{Ulid.NewUlid()}",
                    MessageType = nameof(TrackDeletedEvent),
                    Topic = $"{topicPrefix}-track-deletions",
                    PartitionKey = trackId,
                    Payload = JsonSerializer.Serialize(evt),
                    CorrelationId = correlationId,
                    CreatedAt = now
                };

                await _session.StoreAsync(outbox, ct);
                await _session.SaveChangesAsync(ct);
            }

            // Invalidate streaming cache immediately (best effort, non-transactional)
            using (NovaTuneActivitySource.StartCacheInvalidate(trackId))
            {
                try
                {
                    await _streamingService.InvalidateCacheAsync(trackId, userId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to invalidate streaming cache for track {TrackId}, will expire naturally",
                        trackId);
                }
            }

            // Log with correlation ID
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
            {
                _logger.LogInformation(
                    "Track {TrackId} soft-deleted by user {UserId}, scheduled for physical deletion at {ScheduledDeletionAt}",
                    trackId, userId, scheduledDeletion);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            NovaTuneMetrics.RecordTrackDeleteRequest("success");
            NovaTuneMetrics.RecordTrackSoftDeletion();
        }
        catch (Exception ex) when (ex is not TrackNotFoundException
            and not TrackAccessDeniedException
            and not TrackAlreadyDeletedException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NovaTuneMetrics.RecordTrackDeleteRequest("error");
            throw;
        }
    }

    public async Task<TrackDetails> RestoreTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        using var activity = NovaTuneActivitySource.StartTrackRestore(trackId, userId);

        try
        {
            Track? track;
            using (NovaTuneActivitySource.StartDbLoadTrack(trackId))
            {
                track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);
            }

            if (track is null)
            {
                NovaTuneMetrics.RecordTrackRestoreRequest("not_found");
                throw new TrackNotFoundException(trackId);
            }

            if (track.UserId != userId)
            {
                _logger.LogWarning(
                    "Access denied: user {UserId} attempted to restore track {TrackId} owned by {OwnerId}",
                    userId, trackId, track.UserId);
                NovaTuneMetrics.RecordTrackRestoreRequest("access_denied");
                throw new TrackAccessDeniedException(trackId);
            }

            if (track.Status != TrackStatus.Deleted)
            {
                NovaTuneMetrics.RecordTrackRestoreRequest("not_deleted");
                throw new TrackNotDeletedException(trackId);
            }

            if (track.ScheduledDeletionAt <= DateTimeOffset.UtcNow)
            {
                NovaTuneMetrics.RecordTrackRestoreRequest("expired");
                throw new TrackRestorationExpiredException(
                    trackId,
                    track.DeletedAt!.Value,
                    track.ScheduledDeletionAt!.Value);
            }

            // Restore track status
            using (NovaTuneActivitySource.StartDbUpdateStatus(trackId))
            {
                track.Status = track.StatusBeforeDeletion ?? TrackStatus.Ready;
                track.DeletedAt = null;
                track.ScheduledDeletionAt = null;
                track.StatusBeforeDeletion = null;
                track.UpdatedAt = DateTimeOffset.UtcNow;

                await _session.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "Track {TrackId} restored by user {UserId}",
                trackId, userId);

            activity?.SetStatus(ActivityStatusCode.Ok);
            NovaTuneMetrics.RecordTrackRestoreRequest("success");
            NovaTuneMetrics.RecordTrackRestoration();

            return MapToDetails(track);
        }
        catch (Exception ex) when (ex is not TrackNotFoundException
            and not TrackAccessDeniedException
            and not TrackNotDeletedException
            and not TrackRestorationExpiredException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            NovaTuneMetrics.RecordTrackRestoreRequest("error");
            throw;
        }
    }

    #region Private Helpers

    private static TrackListItem MapToListItem(Track track) =>
        new(track.TrackId, track.Title, track.Artist, track.Duration,
            track.Status, track.FileSizeBytes, track.MimeType,
            track.CreatedAt, track.UpdatedAt, track.ProcessedAt);

    private static TrackDetails MapToDetails(Track track) =>
        new(track.TrackId, track.Title, track.Artist, track.Duration,
            track.Status, track.FileSizeBytes, track.MimeType,
            track.Metadata, track.WaveformObjectKey is not null,
            track.CreatedAt, track.UpdatedAt, track.ProcessedAt,
            track.DeletedAt, track.ScheduledDeletionAt);

    private static IRavenQueryable<Track> ApplySorting(
        IRavenQueryable<Track> query,
        string sortBy,
        bool descending)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "title" => descending
                ? query.OrderByDescending(t => t.Title).ThenByDescending(t => t.TrackId)
                : query.OrderBy(t => t.Title).ThenBy(t => t.TrackId),
            "artist" => descending
                ? query.OrderByDescending(t => t.Artist).ThenByDescending(t => t.TrackId)
                : query.OrderBy(t => t.Artist).ThenBy(t => t.TrackId),
            "duration" => descending
                ? query.OrderByDescending(t => t.Duration).ThenByDescending(t => t.TrackId)
                : query.OrderBy(t => t.Duration).ThenBy(t => t.TrackId),
            "updatedat" => descending
                ? query.OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.TrackId)
                : query.OrderBy(t => t.UpdatedAt).ThenBy(t => t.TrackId),
            _ => descending // Default: createdAt
                ? query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.TrackId)
                : query.OrderBy(t => t.CreatedAt).ThenBy(t => t.TrackId)
        };
    }

    private static IRavenQueryable<Track> ApplyCursor(
        IRavenQueryable<Track> query,
        TrackListCursor cursor,
        string sortBy,
        bool descending)
    {
        // For cursor-based pagination, we filter records that come after the cursor position
        // The cursor contains the sort value and track ID for tie-breaking
        return sortBy.ToLowerInvariant() switch
        {
            "title" => descending
                ? query.Where(t => t.Title.CompareTo(cursor.SortValue) < 0 ||
                    (t.Title == cursor.SortValue && t.TrackId.CompareTo(cursor.TrackId) < 0))
                : query.Where(t => t.Title.CompareTo(cursor.SortValue) > 0 ||
                    (t.Title == cursor.SortValue && t.TrackId.CompareTo(cursor.TrackId) > 0)),
            "artist" => descending
                ? query.Where(t => (t.Artist ?? "").CompareTo(cursor.SortValue) < 0 ||
                    ((t.Artist ?? "") == cursor.SortValue && t.TrackId.CompareTo(cursor.TrackId) < 0))
                : query.Where(t => (t.Artist ?? "").CompareTo(cursor.SortValue) > 0 ||
                    ((t.Artist ?? "") == cursor.SortValue && t.TrackId.CompareTo(cursor.TrackId) > 0)),
            "duration" => ApplyDurationCursor(query, cursor, descending),
            "updatedat" => ApplyDateTimeCursor(query, cursor, descending, t => t.UpdatedAt),
            _ => ApplyDateTimeCursor(query, cursor, descending, t => t.CreatedAt) // Default: createdAt
        };
    }

    private static IRavenQueryable<Track> ApplyDurationCursor(
        IRavenQueryable<Track> query,
        TrackListCursor cursor,
        bool descending)
    {
        if (!TimeSpan.TryParse(cursor.SortValue, out var cursorDuration))
            return query;

        return descending
            ? query.Where(t => t.Duration < cursorDuration ||
                (t.Duration == cursorDuration && t.TrackId.CompareTo(cursor.TrackId) < 0))
            : query.Where(t => t.Duration > cursorDuration ||
                (t.Duration == cursorDuration && t.TrackId.CompareTo(cursor.TrackId) > 0));
    }

    private static IRavenQueryable<Track> ApplyDateTimeCursor(
        IRavenQueryable<Track> query,
        TrackListCursor cursor,
        bool descending,
        System.Linq.Expressions.Expression<Func<Track, DateTimeOffset>> selector)
    {
        if (!DateTimeOffset.TryParse(cursor.SortValue, out var cursorDate))
            return query;

        // Compile the expression to get the field value
        var compiled = selector.Compile();

        return descending
            ? query.Where(t => compiled(t) < cursorDate ||
                (compiled(t) == cursorDate && t.TrackId.CompareTo(cursor.TrackId) < 0))
            : query.Where(t => compiled(t) > cursorDate ||
                (compiled(t) == cursorDate && t.TrackId.CompareTo(cursor.TrackId) > 0));
    }

    private static string EncodeCursor(Track track, string sortBy)
    {
        var sortValue = sortBy.ToLowerInvariant() switch
        {
            "title" => track.Title,
            "artist" => track.Artist ?? "",
            "duration" => track.Duration.ToString(),
            "updatedat" => track.UpdatedAt.ToString("O"),
            _ => track.CreatedAt.ToString("O") // Default: createdAt
        };

        var cursor = new TrackListCursor(sortValue, track.TrackId, DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(cursor);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static TrackListCursor DecodeCursor(string encoded)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return JsonSerializer.Deserialize<TrackListCursor>(json)
                ?? throw new ArgumentException("Invalid cursor format");
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ArgumentException("Invalid cursor format", ex);
        }
    }

    private async Task<int> GetApproximateCountAsync(
        string userId,
        TrackListQuery query,
        CancellationToken ct)
    {
        var countQuery = _session
            .Query<Track, Tracks_ByUserForSearch>()
            .Where(t => t.UserId == userId);

        // Apply same filters
        if (query.Status.HasValue)
        {
            countQuery = countQuery.Where(t => t.Status == query.Status.Value);
        }
        else if (!query.IncludeDeleted)
        {
            countQuery = countQuery.Where(t => t.Status != TrackStatus.Deleted);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            countQuery = countQuery
                .Search(t => t.Title, $"*{query.Search}*")
                .Search(t => t.Artist, $"*{query.Search}*");
        }

        return await countQuery.CountAsync(ct);
    }

    #endregion
}
