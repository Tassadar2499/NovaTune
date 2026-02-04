using System.Text;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;
using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services.Admin;

/// <summary>
/// Admin track moderation service implementation (Req 11.2).
/// </summary>
public class AdminTrackService : IAdminTrackService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IAuditLogService _auditLogService;
    private readonly ITrackManagementService _trackManagementService;
    private readonly IOptions<AdminOptions> _options;
    private readonly IOptions<TrackManagementOptions> _trackOptions;
    private readonly ILogger<AdminTrackService> _logger;

    public AdminTrackService(
        IAsyncDocumentSession session,
        IAuditLogService auditLogService,
        ITrackManagementService trackManagementService,
        IOptions<AdminOptions> options,
        IOptions<TrackManagementOptions> trackOptions,
        ILogger<AdminTrackService> logger)
    {
        _session = session;
        _auditLogService = auditLogService;
        _trackManagementService = trackManagementService;
        _options = options;
        _trackOptions = trackOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AdminTrackListItem>> ListTracksAsync(
        AdminTrackListQuery query,
        CancellationToken ct = default)
    {
        var limit = Math.Min(query.Limit, _options.Value.MaxTrackPageSize);
        if (limit <= 0) limit = _options.Value.DefaultTrackPageSize;

        var dbQuery = _session.Query<Track, Tracks_ForAdminSearch>();

        // Apply search filter using full-text search on title and artist
        // Chained Search calls default to OR behavior
        if (!string.IsNullOrEmpty(query.Search))
        {
            var searchTerm = $"*{query.Search}*";
            dbQuery = dbQuery
                .Search(t => t.Title, searchTerm)
                .Search(t => t.Artist, searchTerm);
        }

        // Apply status filter
        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.Status == query.Status.Value);
        }

        // Apply moderation status filter
        if (query.ModerationStatus.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.ModerationStatus == query.ModerationStatus.Value);
        }

        // Apply user ID filter
        if (!string.IsNullOrEmpty(query.UserId))
        {
            dbQuery = dbQuery.Where(t => t.UserId == query.UserId);
        }

        // Apply cursor-based pagination
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            var cursorData = DecodeCursor(query.Cursor);
            if (cursorData.HasValue)
            {
                dbQuery = query.SortOrder.ToLowerInvariant() == "desc"
                    ? dbQuery.Where(t => t.CreatedAt < cursorData.Value.Timestamp ||
                        (t.CreatedAt == cursorData.Value.Timestamp && string.Compare(t.TrackId, cursorData.Value.TrackId) < 0))
                    : dbQuery.Where(t => t.CreatedAt > cursorData.Value.Timestamp ||
                        (t.CreatedAt == cursorData.Value.Timestamp && string.Compare(t.TrackId, cursorData.Value.TrackId) > 0));
            }
        }

        // Apply sorting
        dbQuery = query.SortBy.ToLowerInvariant() switch
        {
            "title" => query.SortOrder.ToLowerInvariant() == "desc"
                ? dbQuery.OrderByDescending(t => t.Title)
                : dbQuery.OrderBy(t => t.Title),
            "filesizebytes" => query.SortOrder.ToLowerInvariant() == "desc"
                ? dbQuery.OrderByDescending(t => t.FileSizeBytes)
                : dbQuery.OrderBy(t => t.FileSizeBytes),
            "moderatedat" => query.SortOrder.ToLowerInvariant() == "desc"
                ? dbQuery.OrderByDescending(t => t.ModeratedAt)
                : dbQuery.OrderBy(t => t.ModeratedAt),
            _ => query.SortOrder.ToLowerInvariant() == "desc"
                ? dbQuery.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.TrackId)
                : dbQuery.OrderBy(t => t.CreatedAt).ThenBy(t => t.TrackId)
        };

        var tracks = await dbQuery
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = tracks.Count > limit;
        if (hasMore)
            tracks = tracks.Take(limit).ToList();

        // Get user emails for each track - load directly by document ID
        var userIds = tracks.Select(t => t.UserId).Distinct().ToList();
        var userDocIds = userIds.Select(id => $"ApplicationUsers/{id}").ToList();
        var userDict = await _session.LoadAsync<ApplicationUser>(userDocIds, ct);
        var users = userDict.Values
            .Where(u => u != null)
            .ToDictionary(u => u!.UserId, u => u!.Email);

        var items = tracks.Select(t => new AdminTrackListItem(
            t.TrackId,
            t.UserId,
            users.GetValueOrDefault(t.UserId, "unknown"),
            t.Title,
            t.Artist,
            t.Status,
            t.ModerationStatus,
            t.FileSizeBytes,
            t.CreatedAt,
            t.ModeratedAt)).ToList();

        var nextCursor = hasMore && tracks.Count > 0
            ? EncodeCursor(tracks.Last().CreatedAt, tracks.Last().TrackId)
            : null;

        // TotalCount is -1 for cursor pagination (no COUNT query performed)
        return new PagedResult<AdminTrackListItem>(items, nextCursor, -1, hasMore);
    }

    /// <inheritdoc />
    public async Task<AdminTrackDetails?> GetTrackAsync(
        string trackId,
        CancellationToken ct = default)
    {
        var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);
        if (track is null)
            return null;

        var user = await _session
            .Query<ApplicationUser>()
            .Where(u => u.UserId == track.UserId)
            .FirstOrDefaultAsync(ct);

        // TODO: Get total plays from Stage 7 analytics aggregates
        var totalPlays = 0;

        return new AdminTrackDetails(
            track.TrackId,
            track.UserId,
            user?.Email ?? "unknown",
            track.Title,
            track.Artist,
            track.Duration,
            track.Status,
            track.ModerationStatus,
            track.ModerationReasonCode,
            track.FileSizeBytes,
            track.MimeType,
            track.Metadata,
            track.CreatedAt,
            track.ModeratedAt,
            track.ModeratedByUserId,
            totalPlays);
    }

    /// <inheritdoc />
    public async Task<AdminTrackDetails> ModerateTrackAsync(
        string trackId,
        ModerateTrackRequest request,
        string adminUserId,
        CancellationToken ct = default)
    {
        // Validate reason code
        if (!ModerationReasonCodes.IsValid(request.ReasonCode))
        {
            throw new InvalidReasonCodeException(request.ReasonCode);
        }

        var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);
        if (track is null)
        {
            throw new TrackNotFoundException(trackId);
        }

        var previousModerationStatus = track.ModerationStatus;
        var now = DateTimeOffset.UtcNow;

        track.ModerationStatus = request.ModerationStatus;
        track.ModerationReasonCode = request.ReasonCode;
        track.ModeratedAt = now;
        track.ModeratedByUserId = adminUserId;
        track.UpdatedAt = now;

        // Get admin info for audit
        var admin = await _session
            .Query<ApplicationUser>()
            .Where(u => u.UserId == adminUserId)
            .FirstOrDefaultAsync(ct);

        // Create audit log entry
        await _auditLogService.LogAsync(new AuditLogRequest(
            ActorUserId: adminUserId,
            ActorEmail: admin?.Email ?? "unknown",
            Action: AuditActions.TrackModerated,
            TargetType: AuditTargetTypes.Track,
            TargetId: trackId,
            ReasonCode: request.ReasonCode,
            ReasonText: request.ReasonText,
            PreviousState: new { ModerationStatus = previousModerationStatus.ToString() },
            NewState: new { ModerationStatus = request.ModerationStatus.ToString() },
            CorrelationId: null,
            IpAddress: null,
            UserAgent: null), ct);

        // If marked as Removed, trigger soft-delete
        if (request.ModerationStatus == ModerationStatus.Removed)
        {
            track.Status = TrackStatus.Deleted;
            track.DeletedAt = now;
            track.ScheduledDeletionAt = now.Add(_trackOptions.Value.DeletionGracePeriod);
            track.StatusBeforeDeletion = TrackStatus.Ready;

            _logger.LogWarning(
                "Track {TrackId} marked as Removed by admin {AdminUserId}, scheduled for deletion at {DeletionAt}",
                trackId, adminUserId, track.ScheduledDeletionAt);
        }

        await _session.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Track moderated: {TrackId} from {OldStatus} to {NewStatus} by admin {AdminUserId}, reason: {ReasonCode}",
            trackId, previousModerationStatus, request.ModerationStatus, adminUserId, request.ReasonCode);

        return (await GetTrackAsync(trackId, ct))!;
    }

    /// <inheritdoc />
    public async Task DeleteTrackAsync(
        string trackId,
        AdminDeleteTrackRequest request,
        string adminUserId,
        CancellationToken ct = default)
    {
        // Validate reason code
        if (!ModerationReasonCodes.IsValid(request.ReasonCode))
        {
            throw new InvalidReasonCodeException(request.ReasonCode);
        }

        var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);
        if (track is null)
        {
            throw new TrackNotFoundException(trackId);
        }

        var now = DateTimeOffset.UtcNow;

        // Get admin info for audit
        var admin = await _session
            .Query<ApplicationUser>()
            .Where(u => u.UserId == adminUserId)
            .FirstOrDefaultAsync(ct);

        // Create audit log entry
        await _auditLogService.LogAsync(new AuditLogRequest(
            ActorUserId: adminUserId,
            ActorEmail: admin?.Email ?? "unknown",
            Action: AuditActions.TrackDeleted,
            TargetType: AuditTargetTypes.Track,
            TargetId: trackId,
            ReasonCode: request.ReasonCode,
            ReasonText: request.ReasonText,
            PreviousState: new { Status = track.Status.ToString(), ModerationStatus = track.ModerationStatus.ToString() },
            NewState: new { Status = TrackStatus.Deleted.ToString(), ModerationStatus = ModerationStatus.Removed.ToString() },
            CorrelationId: null,
            IpAddress: null,
            UserAgent: null), ct);

        // Perform soft-delete
        track.Status = TrackStatus.Deleted;
        track.ModerationStatus = ModerationStatus.Removed;
        track.ModerationReasonCode = request.ReasonCode;
        track.ModeratedAt = now;
        track.ModeratedByUserId = adminUserId;
        track.DeletedAt = now;
        track.ScheduledDeletionAt = now.Add(_trackOptions.Value.DeletionGracePeriod);
        track.StatusBeforeDeletion = track.Status;
        track.UpdatedAt = now;

        await _session.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Track deleted by admin: {TrackId} by admin {AdminUserId}, reason: {ReasonCode}, scheduled for physical deletion at {DeletionAt}",
            trackId, adminUserId, request.ReasonCode, track.ScheduledDeletionAt);
    }

    private static string EncodeCursor(DateTimeOffset timestamp, string trackId)
    {
        var data = $"{timestamp:O}|{trackId}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
    }

    private static (DateTimeOffset Timestamp, string TrackId)? DecodeCursor(string cursor)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = decoded.Split('|', 2);
            if (parts.Length == 2)
            {
                return (DateTimeOffset.Parse(parts[0]), parts[1]);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
