using System.Text.Json.Serialization;

namespace NovaTuneApp.ApiService.Models.Analytics;

/// <summary>
/// Hourly play count aggregate per track (Req 9.2).
/// </summary>
public sealed class TrackHourlyAggregate
{
    /// <summary>
    /// RavenDB document ID: "TrackHourlyAggregates/{trackId}/{hourBucket}".
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Track ID (ULID).
    /// </summary>
    public string TrackId { get; init; } = string.Empty;

    /// <summary>
    /// Track owner user ID.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Hour bucket (UTC, truncated to hour).
    /// </summary>
    public DateTimeOffset HourBucket { get; init; }

    /// <summary>
    /// Number of play_start events.
    /// </summary>
    public int PlayStartCount { get; set; }

    /// <summary>
    /// Number of play_complete events.
    /// </summary>
    public int PlayCompleteCount { get; set; }

    /// <summary>
    /// Total seconds played across all sessions.
    /// </summary>
    public double TotalSecondsPlayed { get; set; }

    /// <summary>
    /// Unique sessions that played this track.
    /// </summary>
    public int UniqueSessionCount { get; set; }

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Expiration for automatic cleanup (NF-6.3).
    /// RavenDB document expiration.
    /// </summary>
    [JsonPropertyName("@expires")]
    public DateTimeOffset? Expires { get; set; }
}

/// <summary>
/// Daily aggregate for dashboard queries (Req 11.3).
/// </summary>
public sealed class TrackDailyAggregate
{
    /// <summary>
    /// RavenDB document ID: "TrackDailyAggregates/{trackId}/{dateBucket}".
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Track ID (ULID).
    /// </summary>
    public string TrackId { get; init; } = string.Empty;

    /// <summary>
    /// Track owner user ID.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Day bucket (UTC date).
    /// </summary>
    public DateOnly DateBucket { get; init; }

    /// <summary>
    /// Total play_start events.
    /// </summary>
    public int TotalPlays { get; set; }

    /// <summary>
    /// Total play_complete events.
    /// </summary>
    public int CompletedPlays { get; set; }

    /// <summary>
    /// Total seconds played.
    /// </summary>
    public double TotalSecondsPlayed { get; set; }

    /// <summary>
    /// Unique listeners (estimated).
    /// </summary>
    public int UniqueListeners { get; set; }

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Expiration for automatic cleanup (NF-6.3).
    /// </summary>
    [JsonPropertyName("@expires")]
    public DateTimeOffset? Expires { get; set; }
}

/// <summary>
/// User activity summary for admin dashboards.
/// </summary>
public sealed class UserActivityAggregate
{
    /// <summary>
    /// RavenDB document ID: "UserActivityAggregates/{userId}/{dateBucket}".
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// User ID (ULID).
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Day bucket (UTC date).
    /// </summary>
    public DateOnly DateBucket { get; init; }

    /// <summary>
    /// Number of unique tracks played.
    /// </summary>
    public int TracksPlayed { get; set; }

    /// <summary>
    /// Total play_start events.
    /// </summary>
    public int TotalPlays { get; set; }

    /// <summary>
    /// Total seconds played.
    /// </summary>
    public double TotalSecondsPlayed { get; set; }

    /// <summary>
    /// Timestamp of last activity.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Expiration for automatic cleanup (NF-6.3).
    /// </summary>
    [JsonPropertyName("@expires")]
    public DateTimeOffset? Expires { get; set; }
}
