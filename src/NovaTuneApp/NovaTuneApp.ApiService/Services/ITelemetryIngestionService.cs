using NovaTuneApp.ApiService.Models.Telemetry;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for telemetry ingestion (Req 5.4).
/// </summary>
public interface ITelemetryIngestionService
{
    /// <summary>
    /// Ingests a single playback event.
    /// </summary>
    /// <param name="request">The playback event request.</param>
    /// <param name="userId">The authenticated user ID.</param>
    /// <param name="correlationId">Correlation ID for tracing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ingestion result.</returns>
    Task<TelemetryIngestionResult> IngestAsync(
        PlaybackEventRequest request,
        string userId,
        string correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Ingests a batch of playback events.
    /// </summary>
    /// <param name="events">The list of playback events.</param>
    /// <param name="userId">The authenticated user ID.</param>
    /// <param name="correlationId">Correlation ID for tracing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The batch ingestion result.</returns>
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
    /// <param name="trackId">The track ID.</param>
    /// <param name="startDate">Start date of the range.</param>
    /// <param name="endDate">End date of the range.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Track analytics summary.</returns>
    Task<TrackAnalytics> GetTrackAnalyticsAsync(
        string trackId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    /// <summary>
    /// Gets top tracks by play count for admin dashboard.
    /// </summary>
    /// <param name="count">Maximum number of tracks to return.</param>
    /// <param name="since">Optional start date filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of top tracks.</returns>
    Task<IReadOnlyList<TrackPlaySummary>> GetTopTracksAsync(
        int count,
        DateOnly? since,
        CancellationToken ct = default);

    /// <summary>
    /// Gets user activity summary for admin dashboard.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="startDate">Start date of the range.</param>
    /// <param name="endDate">End date of the range.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User activity summary.</returns>
    Task<UserActivitySummary> GetUserActivityAsync(
        string userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    /// <summary>
    /// Gets recent activity feed for admin dashboard.
    /// </summary>
    /// <param name="count">Maximum number of items to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of recent activity items.</returns>
    Task<IReadOnlyList<RecentActivityItem>> GetRecentActivityAsync(
        int count,
        CancellationToken ct = default);
}
