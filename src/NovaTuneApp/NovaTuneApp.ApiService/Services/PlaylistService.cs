using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Playlists;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for playlist CRUD operations.
/// </summary>
public class PlaylistService : IPlaylistService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IOptions<PlaylistOptions> _options;
    private readonly ILogger<PlaylistService> _logger;

    private static readonly HashSet<string> ValidSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt", "updatedAt", "name", "trackCount"
    };

    public PlaylistService(
        IAsyncDocumentSession session,
        IOptions<PlaylistOptions> options,
        ILogger<PlaylistService> logger)
    {
        _session = session;
        _options = options;
        _logger = logger;
    }

    public async Task<PagedResult<PlaylistListItem>> ListPlaylistsAsync(
        string userId,
        PlaylistListQuery query,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Listing playlists for user {UserId}: Search={Search}, SortBy={SortBy}, Limit={Limit}",
            userId, query.Search, query.SortBy, query.Limit);

        var limit = Math.Clamp(query.Limit, 1, _options.Value.MaxPageSize);
        var sortBy = ValidSortFields.Contains(query.SortBy) ? query.SortBy : "updatedAt";
        var sortDescending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        var ravenQuery = _session
            .Query<Playlist, Playlists_ByUserForSearch>()
            .Where(p => p.UserId == userId);

        // Full-text search
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            ravenQuery = ravenQuery.Search(p => p.Name, $"*{query.Search}*");
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
        var playlists = await ravenQuery.Take(limit + 1).ToListAsync(ct);

        var hasMore = playlists.Count > limit;
        var items = playlists.Take(limit).Select(MapToListItem).ToList();

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            var lastPlaylist = playlists[limit - 1];
            nextCursor = EncodeCursor(lastPlaylist, sortBy);
        }

        // Get approximate total count
        var totalCount = await GetApproximateCountAsync(userId, query.Search, ct);

        return new PagedResult<PlaylistListItem>(items, nextCursor, totalCount, hasMore);
    }

    public async Task<PlaylistDetails> CreatePlaylistAsync(
        string userId,
        CreatePlaylistRequest request,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Creating playlist for user {UserId}: Name={Name}",
            userId, request.Name);

        // Check quota
        var currentCount = await _session
            .Query<Playlist, Playlists_ByUserForSearch>()
            .Where(p => p.UserId == userId)
            .CountAsync(ct);

        if (currentCount >= _options.Value.MaxPlaylistsPerUser)
        {
            _logger.LogWarning(
                "User {UserId} exceeded playlist quota: {CurrentCount}/{MaxCount}",
                userId, currentCount, _options.Value.MaxPlaylistsPerUser);
            throw new PlaylistQuotaExceededException(userId, currentCount, _options.Value.MaxPlaylistsPerUser);
        }

        var now = DateTimeOffset.UtcNow;
        var playlistId = Ulid.NewUlid().ToString();

        var playlist = new Playlist
        {
            Id = $"Playlists/{playlistId}",
            PlaylistId = playlistId,
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            Tracks = [],
            TrackCount = 0,
            TotalDuration = TimeSpan.Zero,
            Visibility = PlaylistVisibility.Private,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _session.StoreAsync(playlist, ct);
        await _session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created playlist {PlaylistId} for user {UserId}",
            playlistId, userId);

        return MapToDetails(playlist, null);
    }

    public async Task<PlaylistDetails> GetPlaylistAsync(
        string playlistId,
        string userId,
        PlaylistDetailQuery query,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting playlist {PlaylistId} for user {UserId}",
            playlistId, userId);

        var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

        if (playlist is null)
        {
            throw new PlaylistNotFoundException(playlistId);
        }

        if (playlist.UserId != userId)
        {
            _logger.LogWarning(
                "Access denied: user {UserId} attempted to access playlist {PlaylistId} owned by {OwnerId}",
                userId, playlistId, playlist.UserId);
            throw new PlaylistAccessDeniedException(playlistId);
        }

        PagedResult<PlaylistTrackItem>? trackResult = null;
        if (query.IncludeTracks && playlist.Tracks.Count > 0)
        {
            trackResult = await GetPlaylistTracksAsync(playlist, query.TrackCursor, query.TrackLimit, ct);
        }
        else if (query.IncludeTracks)
        {
            trackResult = new PagedResult<PlaylistTrackItem>([], null, 0, false);
        }

        return MapToDetails(playlist, trackResult);
    }

    public async Task<PlaylistDetails> UpdatePlaylistAsync(
        string playlistId,
        string userId,
        UpdatePlaylistRequest request,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Updating playlist {PlaylistId} for user {UserId}",
            playlistId, userId);

        var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

        if (playlist is null)
        {
            throw new PlaylistNotFoundException(playlistId);
        }

        if (playlist.UserId != userId)
        {
            _logger.LogWarning(
                "Access denied: user {UserId} attempted to update playlist {PlaylistId} owned by {OwnerId}",
                userId, playlistId, playlist.UserId);
            throw new PlaylistAccessDeniedException(playlistId);
        }

        var changedFields = new List<string>();

        if (request.Name is not null)
        {
            playlist.Name = request.Name;
            changedFields.Add("name");
        }

        if (request.HasDescription)
        {
            playlist.Description = request.Description;
            changedFields.Add("description");
        }

        playlist.UpdatedAt = DateTimeOffset.UtcNow;

        await _session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated playlist {PlaylistId} for user {UserId}, changed fields: {ChangedFields}",
            playlistId, userId, string.Join(", ", changedFields));

        return MapToDetails(playlist, null);
    }

    public async Task DeletePlaylistAsync(
        string playlistId,
        string userId,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Deleting playlist {PlaylistId} for user {UserId}",
            playlistId, userId);

        var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

        if (playlist is null)
        {
            throw new PlaylistNotFoundException(playlistId);
        }

        if (playlist.UserId != userId)
        {
            _logger.LogWarning(
                "Access denied: user {UserId} attempted to delete playlist {PlaylistId} owned by {OwnerId}",
                userId, playlistId, playlist.UserId);
            throw new PlaylistAccessDeniedException(playlistId);
        }

        _session.Delete(playlist);
        await _session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deleted playlist {PlaylistId} for user {UserId}",
            playlistId, userId);
    }

    public async Task<PlaylistDetails> AddTracksAsync(
        string playlistId,
        string userId,
        AddTracksRequest request,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Adding {TrackCount} tracks to playlist {PlaylistId} for user {UserId}",
            request.TrackIds.Count, playlistId, userId);

        var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

        if (playlist is null)
        {
            throw new PlaylistNotFoundException(playlistId);
        }

        if (playlist.UserId != userId)
        {
            throw new PlaylistAccessDeniedException(playlistId);
        }

        // Check track limit
        if (playlist.TrackCount + request.TrackIds.Count > _options.Value.MaxTracksPerPlaylist)
        {
            throw new PlaylistTrackLimitExceededException(
                playlistId,
                playlist.TrackCount,
                request.TrackIds.Count,
                _options.Value.MaxTracksPerPlaylist);
        }

        // Load and validate all tracks
        var trackDocIds = request.TrackIds.Select(id => $"Tracks/{id}").ToList();
        var trackDocs = await _session.LoadAsync<Track>(trackDocIds, ct);

        foreach (var trackId in request.TrackIds)
        {
            var docId = $"Tracks/{trackId}";
            if (!trackDocs.TryGetValue(docId, out var track) || track is null)
            {
                throw new TrackNotFoundException(trackId);
            }

            if (track.UserId != userId)
            {
                throw new TrackAccessDeniedException(trackId);
            }

            if (track.Status == TrackStatus.Deleted)
            {
                throw new TrackDeletedException(trackId, track.DeletedAt);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var insertPosition = request.Position ?? playlist.Tracks.Count;

        // Validate insert position
        if (insertPosition < 0 || insertPosition > playlist.Tracks.Count)
        {
            throw new InvalidPositionException(insertPosition, playlist.Tracks.Count + 1);
        }

        // Shift existing tracks at and after insert position
        foreach (var entry in playlist.Tracks.Where(t => t.Position >= insertPosition))
        {
            entry.Position += request.TrackIds.Count;
        }

        // Add new track entries
        var newEntries = request.TrackIds.Select((id, i) => new PlaylistTrackEntry
        {
            Position = insertPosition + i,
            TrackId = id,
            AddedAt = now
        }).ToList();

        playlist.Tracks.AddRange(newEntries);

        // Sort by position to maintain order
        playlist.Tracks = playlist.Tracks.OrderBy(t => t.Position).ToList();

        // Update denormalized fields
        playlist.TrackCount = playlist.Tracks.Count;
        playlist.TotalDuration = CalculateTotalDuration(playlist.Tracks, trackDocs);
        playlist.UpdatedAt = now;

        await _session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Added {TrackCount} tracks to playlist {PlaylistId} at position {Position} for user {UserId}",
            request.TrackIds.Count, playlistId, insertPosition, userId);

        return MapToDetails(playlist, null);
    }

    public async Task RemoveTrackAsync(
        string playlistId,
        string userId,
        int position,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Removing track at position {Position} from playlist {PlaylistId} for user {UserId}",
            position, playlistId, userId);

        var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

        if (playlist is null)
        {
            throw new PlaylistNotFoundException(playlistId);
        }

        if (playlist.UserId != userId)
        {
            throw new PlaylistAccessDeniedException(playlistId);
        }

        // Find track at position
        var trackToRemove = playlist.Tracks.FirstOrDefault(t => t.Position == position);
        if (trackToRemove is null)
        {
            throw new PlaylistTrackNotFoundException(playlistId, position);
        }

        // Remove the track
        playlist.Tracks.Remove(trackToRemove);

        // Reindex positions for tracks after the removed one
        foreach (var entry in playlist.Tracks.Where(t => t.Position > position))
        {
            entry.Position--;
        }

        // Update denormalized fields
        playlist.TrackCount = playlist.Tracks.Count;
        playlist.UpdatedAt = DateTimeOffset.UtcNow;

        // Recalculate total duration
        await RecalculateTotalDurationAsync(playlist, ct);

        await _session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Removed track at position {Position} from playlist {PlaylistId} for user {UserId}",
            position, playlistId, userId);
    }

    public async Task<PlaylistDetails> ReorderTracksAsync(
        string playlistId,
        string userId,
        ReorderRequest request,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Reordering {MoveCount} tracks in playlist {PlaylistId} for user {UserId}",
            request.Moves.Count, playlistId, userId);

        var playlist = await _session.LoadAsync<Playlist>($"Playlists/{playlistId}", ct);

        if (playlist is null)
        {
            throw new PlaylistNotFoundException(playlistId);
        }

        if (playlist.UserId != userId)
        {
            throw new PlaylistAccessDeniedException(playlistId);
        }

        if (playlist.Tracks.Count == 0)
        {
            throw new InvalidOperationException("Cannot reorder empty playlist");
        }

        // Validate all positions before applying any moves
        foreach (var move in request.Moves)
        {
            if (move.From < 0 || move.From >= playlist.Tracks.Count)
            {
                throw new InvalidPositionException(move.From, playlist.Tracks.Count);
            }

            if (move.To < 0 || move.To >= playlist.Tracks.Count)
            {
                throw new InvalidPositionException(move.To, playlist.Tracks.Count);
            }
        }

        // Sort tracks by position to work with a proper list
        var tracks = playlist.Tracks.OrderBy(t => t.Position).ToList();

        // Apply moves sequentially
        foreach (var move in request.Moves)
        {
            if (move.From == move.To)
                continue; // No-op move

            var track = tracks[move.From];
            tracks.RemoveAt(move.From);
            tracks.Insert(move.To, track);
        }

        // Reassign positions to maintain contiguous 0-based indices
        for (var i = 0; i < tracks.Count; i++)
        {
            tracks[i].Position = i;
        }

        playlist.Tracks = tracks;
        playlist.UpdatedAt = DateTimeOffset.UtcNow;

        await _session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Reordered {MoveCount} tracks in playlist {PlaylistId} for user {UserId}",
            request.Moves.Count, playlistId, userId);

        return MapToDetails(playlist, null);
    }

    public async Task RemoveDeletedTrackReferencesAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Removing deleted track {TrackId} references from playlists for user {UserId}",
            trackId, userId);

        // Find all playlists containing this track
        var affectedPlaylistIds = await _session
            .Query<Playlists_ByTrackReference.Result, Playlists_ByTrackReference>()
            .Where(r => r.UserId == userId && r.TrackId == trackId)
            .Select(r => r.PlaylistId)
            .Distinct()
            .ToListAsync(ct);

        if (affectedPlaylistIds.Count == 0)
        {
            _logger.LogDebug(
                "No playlists found containing track {TrackId} for user {UserId}",
                trackId, userId);
            return;
        }

        // Load and update each affected playlist
        var playlistDocIds = affectedPlaylistIds.Select(id => $"Playlists/{id}").ToList();
        var playlists = await _session.LoadAsync<Playlist>(playlistDocIds, ct);

        foreach (var (_, playlist) in playlists)
        {
            if (playlist is null)
                continue;

            // Remove all entries for this track
            var entriesRemoved = playlist.Tracks.RemoveAll(t => t.TrackId == trackId);

            if (entriesRemoved == 0)
                continue;

            // Reindex positions
            var orderedTracks = playlist.Tracks.OrderBy(t => t.Position).ToList();
            for (var i = 0; i < orderedTracks.Count; i++)
            {
                orderedTracks[i].Position = i;
            }
            playlist.Tracks = orderedTracks;

            // Update denormalized fields
            playlist.TrackCount = playlist.Tracks.Count;
            playlist.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Removed {EntriesRemoved} reference(s) to track {TrackId} from playlist {PlaylistId}",
                entriesRemoved, trackId, playlist.PlaylistId);
        }

        await _session.SaveChangesAsync(ct);
    }

    #region Private Helpers

    private async Task<PagedResult<PlaylistTrackItem>> GetPlaylistTracksAsync(
        Playlist playlist,
        string? trackCursor,
        int limit,
        CancellationToken ct)
    {
        var orderedTracks = playlist.Tracks.OrderBy(t => t.Position).ToList();
        var startPosition = 0;

        // Apply cursor (position-based for tracks within playlist)
        if (!string.IsNullOrEmpty(trackCursor) && int.TryParse(trackCursor, out var cursorPosition))
        {
            startPosition = cursorPosition + 1;
        }

        var pagedEntries = orderedTracks
            .Where(t => t.Position >= startPosition)
            .Take(limit + 1)
            .ToList();

        var hasMore = pagedEntries.Count > limit;
        var entries = pagedEntries.Take(limit).ToList();

        // Load track details
        var trackIds = entries.Select(e => $"Tracks/{e.TrackId}").ToList();
        var tracks = await _session.LoadAsync<Track>(trackIds, ct);

        var items = entries.Select(entry =>
        {
            var track = tracks.GetValueOrDefault($"Tracks/{entry.TrackId}");
            return new PlaylistTrackItem(
                entry.Position,
                entry.TrackId,
                track?.Title ?? "Unknown",
                track?.Artist,
                track?.Duration ?? TimeSpan.Zero,
                track?.Status ?? TrackStatus.Unknown,
                entry.AddedAt);
        }).ToList();

        string? nextCursor = null;
        if (hasMore && entries.Count > 0)
        {
            nextCursor = entries[^1].Position.ToString();
        }

        return new PagedResult<PlaylistTrackItem>(items, nextCursor, playlist.TrackCount, hasMore);
    }

    private static PlaylistListItem MapToListItem(Playlist playlist) =>
        new(playlist.PlaylistId, playlist.Name, playlist.Description,
            playlist.TrackCount, playlist.TotalDuration, playlist.Visibility,
            playlist.CreatedAt, playlist.UpdatedAt);

    private static PlaylistDetails MapToDetails(Playlist playlist, PagedResult<PlaylistTrackItem>? tracks) =>
        new(playlist.PlaylistId, playlist.Name, playlist.Description,
            playlist.TrackCount, playlist.TotalDuration, playlist.Visibility,
            playlist.CreatedAt, playlist.UpdatedAt, tracks);

    private static TimeSpan CalculateTotalDuration(
        List<PlaylistTrackEntry> entries,
        IReadOnlyDictionary<string, Track> trackDocs)
    {
        var total = TimeSpan.Zero;
        foreach (var entry in entries)
        {
            if (trackDocs.TryGetValue($"Tracks/{entry.TrackId}", out var track))
            {
                total += track.Duration;
            }
        }
        return total;
    }

    private async Task RecalculateTotalDurationAsync(Playlist playlist, CancellationToken ct)
    {
        if (playlist.Tracks.Count == 0)
        {
            playlist.TotalDuration = TimeSpan.Zero;
            return;
        }

        var trackIds = playlist.Tracks.Select(t => $"Tracks/{t.TrackId}").ToList();
        var tracks = await _session.LoadAsync<Track>(trackIds, ct);

        playlist.TotalDuration = CalculateTotalDuration(playlist.Tracks, tracks);
    }

    private static IRavenQueryable<Playlist> ApplySorting(
        IRavenQueryable<Playlist> query,
        string sortBy,
        bool descending)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "name" => descending
                ? query.OrderByDescending(p => p.Name).ThenByDescending(p => p.PlaylistId)
                : query.OrderBy(p => p.Name).ThenBy(p => p.PlaylistId),
            "trackcount" => descending
                ? query.OrderByDescending(p => p.TrackCount).ThenByDescending(p => p.PlaylistId)
                : query.OrderBy(p => p.TrackCount).ThenBy(p => p.PlaylistId),
            "createdat" => descending
                ? query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.PlaylistId)
                : query.OrderBy(p => p.CreatedAt).ThenBy(p => p.PlaylistId),
            _ => descending // Default: updatedAt
                ? query.OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.PlaylistId)
                : query.OrderBy(p => p.UpdatedAt).ThenBy(p => p.PlaylistId)
        };
    }

    private static IRavenQueryable<Playlist> ApplyCursor(
        IRavenQueryable<Playlist> query,
        PlaylistListCursor cursor,
        string sortBy,
        bool descending)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "name" => descending
                ? query.Where(p => p.Name.CompareTo(cursor.SortValue) < 0 ||
                    (p.Name == cursor.SortValue && p.PlaylistId.CompareTo(cursor.PlaylistId) < 0))
                : query.Where(p => p.Name.CompareTo(cursor.SortValue) > 0 ||
                    (p.Name == cursor.SortValue && p.PlaylistId.CompareTo(cursor.PlaylistId) > 0)),
            "trackcount" => ApplyTrackCountCursor(query, cursor, descending),
            "createdat" => ApplyDateTimeCursor(query, cursor, descending, p => p.CreatedAt),
            _ => ApplyDateTimeCursor(query, cursor, descending, p => p.UpdatedAt)
        };
    }

    private static IRavenQueryable<Playlist> ApplyTrackCountCursor(
        IRavenQueryable<Playlist> query,
        PlaylistListCursor cursor,
        bool descending)
    {
        if (!int.TryParse(cursor.SortValue, out var cursorCount))
            return query;

        return descending
            ? query.Where(p => p.TrackCount < cursorCount ||
                (p.TrackCount == cursorCount && p.PlaylistId.CompareTo(cursor.PlaylistId) < 0))
            : query.Where(p => p.TrackCount > cursorCount ||
                (p.TrackCount == cursorCount && p.PlaylistId.CompareTo(cursor.PlaylistId) > 0));
    }

    private static IRavenQueryable<Playlist> ApplyDateTimeCursor(
        IRavenQueryable<Playlist> query,
        PlaylistListCursor cursor,
        bool descending,
        System.Linq.Expressions.Expression<Func<Playlist, DateTimeOffset>> selector)
    {
        if (!DateTimeOffset.TryParse(cursor.SortValue, out var cursorDate))
            return query;

        var compiled = selector.Compile();

        return descending
            ? query.Where(p => compiled(p) < cursorDate ||
                (compiled(p) == cursorDate && p.PlaylistId.CompareTo(cursor.PlaylistId) < 0))
            : query.Where(p => compiled(p) > cursorDate ||
                (compiled(p) == cursorDate && p.PlaylistId.CompareTo(cursor.PlaylistId) > 0));
    }

    private static string EncodeCursor(Playlist playlist, string sortBy)
    {
        var sortValue = sortBy.ToLowerInvariant() switch
        {
            "name" => playlist.Name,
            "trackcount" => playlist.TrackCount.ToString(),
            "createdat" => playlist.CreatedAt.ToString("O"),
            _ => playlist.UpdatedAt.ToString("O")
        };

        var cursor = new PlaylistListCursor(sortValue, playlist.PlaylistId, DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(cursor);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static PlaylistListCursor DecodeCursor(string encoded)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return JsonSerializer.Deserialize<PlaylistListCursor>(json)
                ?? throw new ArgumentException("Invalid cursor format");
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ArgumentException("Invalid cursor format", ex);
        }
    }

    private async Task<int> GetApproximateCountAsync(
        string userId,
        string? search,
        CancellationToken ct)
    {
        var countQuery = _session
            .Query<Playlist, Playlists_ByUserForSearch>()
            .Where(p => p.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            countQuery = countQuery.Search(p => p.Name, $"*{search}*");
        }

        return await countQuery.CountAsync(ct);
    }

    #endregion
}

/// <summary>
/// Cursor for playlist list pagination.
/// </summary>
internal record PlaylistListCursor(string SortValue, string PlaylistId, DateTimeOffset CreatedAt);
