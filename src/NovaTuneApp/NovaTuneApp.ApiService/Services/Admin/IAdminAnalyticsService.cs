using NovaTuneApp.ApiService.Models.Admin;

namespace NovaTuneApp.ApiService.Services.Admin;

/// <summary>
/// Service for admin analytics dashboard (Req 11.3).
/// </summary>
public interface IAdminAnalyticsService
{
    /// <summary>
    /// Gets overall analytics overview for admin dashboard.
    /// </summary>
    Task<AnalyticsOverview> GetOverviewAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Gets top tracks by play count for a given period.
    /// </summary>
    Task<TopTracksResponse> GetTopTracksAsync(
        int count,
        AnalyticsPeriod period,
        CancellationToken ct = default);

    /// <summary>
    /// Gets most active users for a given period.
    /// </summary>
    Task<ActiveUsersResponse> GetActiveUsersAsync(
        int count,
        AnalyticsPeriod period,
        CancellationToken ct = default);
}
