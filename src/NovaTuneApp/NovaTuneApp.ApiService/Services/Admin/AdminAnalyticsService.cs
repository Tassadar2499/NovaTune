using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;
using NovaTuneApp.ApiService.Models.Analytics;
using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services.Admin;

/// <summary>
/// Admin analytics service implementation using Stage 7 aggregates (Req 11.3).
/// </summary>
public class AdminAnalyticsService : IAdminAnalyticsService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IOptions<AdminOptions> _options;
    private readonly ILogger<AdminAnalyticsService> _logger;

    public AdminAnalyticsService(
        IAsyncDocumentSession session,
        IOptions<AdminOptions> options,
        ILogger<AdminAnalyticsService> logger)
    {
        _session = session;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AnalyticsOverview> GetOverviewAsync(
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var yesterday = now.AddDays(-1);
        var todayDate = DateOnly.FromDateTime(now.DateTime);

        // Get total user count
        var totalUsers = await _session
            .Query<ApplicationUser>()
            .CountAsync(ct);

        // Get active users in last 24h from user activity aggregates
        var activeUsersLast24h = await _session
            .Query<UserActivityAggregate>()
            .Where(a => a.DateBucket == todayDate)
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(ct);

        // Get total tracks count
        var totalTracks = await _session
            .Query<Track>()
            .Where(t => t.Status != TrackStatus.Deleted)
            .CountAsync(ct);

        // Get tracks uploaded in last 24h
        var tracksUploadedLast24h = await _session
            .Query<Track>()
            .Where(t => t.CreatedAt >= yesterday && t.Status != TrackStatus.Deleted)
            .CountAsync(ct);

        // Get play stats from today's daily aggregates
        var todaysAggregates = await _session
            .Query<TrackDailyAggregate>()
            .Where(a => a.DateBucket == todayDate)
            .ToListAsync(ct);

        var totalPlaysLast24h = todaysAggregates.Sum(a => a.TotalPlays);
        var totalSecondsLast24h = todaysAggregates.Sum(a => a.TotalSecondsPlayed);

        // Get total storage used
        var allUsers = await _session
            .Query<ApplicationUser>()
            .Select(u => u.UsedStorageBytes)
            .ToListAsync(ct);
        var totalStorageUsed = allUsers.Sum();

        return new AnalyticsOverview(
            TotalUsers: totalUsers,
            ActiveUsersLast24h: activeUsersLast24h,
            TotalTracks: totalTracks,
            TracksUploadedLast24h: tracksUploadedLast24h,
            TotalPlaysLast24h: totalPlaysLast24h,
            TotalListenTimeLast24h: TimeSpan.FromSeconds(totalSecondsLast24h),
            TotalStorageUsedBytes: totalStorageUsed);
    }

    /// <inheritdoc />
    public async Task<TopTracksResponse> GetTopTracksAsync(
        int count,
        AnalyticsPeriod period,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 100);
        var (startDate, periodString) = GetDateRange(period);

        // Query daily aggregates and group by track
        var aggregates = await _session
            .Query<TrackDailyAggregate>()
            .Where(a => a.DateBucket >= startDate)
            .ToListAsync(ct);

        var trackStats = aggregates
            .GroupBy(a => a.TrackId)
            .Select(g => new
            {
                TrackId = g.Key,
                UserId = g.First().UserId,
                PlayCount = g.Sum(a => a.TotalPlays),
                TotalSecondsPlayed = g.Sum(a => a.TotalSecondsPlayed)
            })
            .OrderByDescending(t => t.PlayCount)
            .Take(count)
            .ToList();

        // Get track details
        var trackIds = trackStats.Select(t => $"Tracks/{t.TrackId}").ToList();
        var tracks = await _session.LoadAsync<Track>(trackIds, ct);

        // Get user emails - load directly by document ID for efficiency
        var userIds = trackStats.Select(t => t.UserId).Distinct().ToList();
        var userDocIds = userIds.Select(id => $"ApplicationUsers/{id}").ToList();
        var userDict = await _session.LoadAsync<ApplicationUser>(userDocIds, ct);
        var users = userDict.Values
            .Where(u => u != null)
            .ToDictionary(u => u!.UserId, u => u!.Email);

        var items = trackStats.Select(s =>
        {
            var track = tracks.GetValueOrDefault($"Tracks/{s.TrackId}");
            return new TopTrackItem(
                s.TrackId,
                track?.Title ?? "Unknown",
                track?.Artist,
                s.UserId,
                users.GetValueOrDefault(s.UserId, "unknown"),
                s.PlayCount,
                TimeSpan.FromSeconds(s.TotalSecondsPlayed));
        }).ToList();

        return new TopTracksResponse(periodString, items);
    }

    /// <inheritdoc />
    public async Task<ActiveUsersResponse> GetActiveUsersAsync(
        int count,
        AnalyticsPeriod period,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 100);
        var (startDate, periodString) = GetDateRange(period);

        // Query user activity aggregates
        var aggregates = await _session
            .Query<UserActivityAggregate>()
            .Where(a => a.DateBucket >= startDate)
            .ToListAsync(ct);

        var userStats = aggregates
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPlays = g.Sum(a => a.TotalPlays),
                TotalSecondsPlayed = g.Sum(a => a.TotalSecondsPlayed),
                LastActivityAt = g.Max(a => a.LastActivityAt)
            })
            .OrderByDescending(u => u.TotalPlays)
            .Take(count)
            .ToList();

        // Get user details - load directly by document ID for efficiency
        var userIds = userStats.Select(u => u.UserId).ToList();
        var documentIds = userIds.Select(id => $"ApplicationUsers/{id}").ToList();
        var userDict = await _session.LoadAsync<ApplicationUser>(documentIds, ct);
        var users = userDict.Values
            .Where(u => u != null)
            .ToDictionary(u => u!.UserId);

        var items = userStats.Select(s =>
        {
            var user = users.GetValueOrDefault(s.UserId);
            return new UserActivityItem(
                s.UserId,
                user?.Email ?? "unknown",
                user?.DisplayName,
                user?.TrackCount ?? 0,
                s.TotalPlays,
                TimeSpan.FromSeconds(s.TotalSecondsPlayed),
                s.LastActivityAt);
        }).ToList();

        return new ActiveUsersResponse(periodString, items);
    }

    private static (DateOnly StartDate, string PeriodString) GetDateRange(AnalyticsPeriod period)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return period switch
        {
            AnalyticsPeriod.Last24Hours => (today, "24h"),
            AnalyticsPeriod.Last7Days => (today.AddDays(-7), "7d"),
            AnalyticsPeriod.Last30Days => (today.AddDays(-30), "30d"),
            AnalyticsPeriod.AllTime => (DateOnly.MinValue, "all"),
            _ => (today.AddDays(-7), "7d")
        };
    }
}
