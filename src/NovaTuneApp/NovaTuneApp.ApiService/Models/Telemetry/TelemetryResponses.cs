namespace NovaTuneApp.ApiService.Models.Telemetry;

/// <summary>
/// Response for successful telemetry event ingestion.
/// </summary>
/// <param name="Accepted">Whether the event was accepted.</param>
/// <param name="CorrelationId">Correlation ID for tracing.</param>
public record TelemetryAcceptedResponse(bool Accepted, string CorrelationId);

/// <summary>
/// Response for batch telemetry event ingestion.
/// </summary>
/// <param name="Accepted">Number of events accepted.</param>
/// <param name="Rejected">Number of events rejected.</param>
/// <param name="CorrelationId">Correlation ID for tracing.</param>
public record TelemetryBatchResponse(int Accepted, int Rejected, string CorrelationId);

/// <summary>
/// Result of a single telemetry ingestion operation.
/// </summary>
/// <param name="Accepted">Whether the event was accepted.</param>
/// <param name="CorrelationId">Correlation ID for tracing (if accepted).</param>
/// <param name="RejectionReason">Reason for rejection (if rejected).</param>
public record TelemetryIngestionResult(
    bool Accepted,
    string? CorrelationId = null,
    string? RejectionReason = null)
{
    /// <summary>
    /// Creates an accepted result.
    /// </summary>
    public static TelemetryIngestionResult Success(string correlationId) =>
        new(true, correlationId);

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    public static TelemetryIngestionResult Rejected(string reason) =>
        new(false, RejectionReason: reason);

    /// <summary>
    /// Creates an access denied result.
    /// </summary>
    public static TelemetryIngestionResult AccessDenied() =>
        new(false, RejectionReason: "access_denied");

    /// <summary>
    /// Creates a sampled (dropped) result.
    /// </summary>
    public static TelemetryIngestionResult Sampled() =>
        new(true, RejectionReason: "sampled");
}

/// <summary>
/// Result of a batch telemetry ingestion operation.
/// </summary>
/// <param name="AcceptedCount">Number of events accepted.</param>
/// <param name="RejectedCount">Number of events rejected.</param>
/// <param name="CorrelationId">Correlation ID for tracing.</param>
public record TelemetryBatchResult(
    int AcceptedCount,
    int RejectedCount,
    string CorrelationId);

/// <summary>
/// Track analytics summary for a date range.
/// </summary>
/// <param name="TrackId">Track identifier.</param>
/// <param name="StartDate">Start of date range.</param>
/// <param name="EndDate">End of date range.</param>
/// <param name="TotalPlays">Total play starts.</param>
/// <param name="CompletedPlays">Total play completions.</param>
/// <param name="TotalListenTime">Total listen time.</param>
/// <param name="UniqueListeners">Estimated unique listeners.</param>
/// <param name="DailyBreakdown">Daily play counts.</param>
public record TrackAnalytics(
    string TrackId,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalPlays,
    int CompletedPlays,
    TimeSpan TotalListenTime,
    int UniqueListeners,
    IReadOnlyList<DailyPlayCount> DailyBreakdown);

/// <summary>
/// Daily play count for a track.
/// </summary>
/// <param name="Date">Date.</param>
/// <param name="Plays">Number of play starts.</param>
/// <param name="Completed">Number of play completions.</param>
public record DailyPlayCount(DateOnly Date, int Plays, int Completed);

/// <summary>
/// Track play summary for top tracks list.
/// </summary>
/// <param name="TrackId">Track identifier.</param>
/// <param name="Title">Track title.</param>
/// <param name="Artist">Track artist.</param>
/// <param name="UserId">Track owner user ID.</param>
/// <param name="PlayCount">Total play count.</param>
/// <param name="TotalListenTime">Total listen time.</param>
public record TrackPlaySummary(
    string TrackId,
    string Title,
    string? Artist,
    string UserId,
    int PlayCount,
    TimeSpan TotalListenTime);

/// <summary>
/// User activity summary for admin dashboards.
/// </summary>
/// <param name="UserId">User identifier.</param>
/// <param name="TracksPlayed">Number of unique tracks played.</param>
/// <param name="TotalPlays">Total play events.</param>
/// <param name="TotalListenTime">Total listen time.</param>
/// <param name="LastActivityAt">Timestamp of last activity.</param>
public record UserActivitySummary(
    string UserId,
    int TracksPlayed,
    int TotalPlays,
    TimeSpan TotalListenTime,
    DateTimeOffset LastActivityAt);

/// <summary>
/// Recent activity item for activity feed.
/// </summary>
/// <param name="UserId">User who triggered the event.</param>
/// <param name="TrackId">Track that was played.</param>
/// <param name="EventType">Type of event.</param>
/// <param name="Timestamp">When the event occurred.</param>
public record RecentActivityItem(
    string UserId,
    string TrackId,
    string EventType,
    DateTimeOffset Timestamp);
