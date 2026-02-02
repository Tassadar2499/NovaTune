using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models.Telemetry;

/// <summary>
/// Client-reported playback event (Req 5.4).
/// </summary>
public sealed class PlaybackEventRequest
{
    /// <summary>
    /// Event type: "play_start", "play_stop", "play_progress", "play_complete", "seek".
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Track being played (ULID).
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string TrackId { get; init; } = string.Empty;

    /// <summary>
    /// Client-reported timestamp (ISO 8601).
    /// </summary>
    [Required]
    public DateTimeOffset ClientTimestamp { get; init; }

    /// <summary>
    /// Current playback position in seconds.
    /// </summary>
    public double? PositionSeconds { get; init; }

    /// <summary>
    /// Total duration played in this session (seconds).
    /// </summary>
    public double? DurationPlayedSeconds { get; init; }

    /// <summary>
    /// Unique session identifier for grouping events.
    /// </summary>
    [MaxLength(64)]
    public string? SessionId { get; init; }

    /// <summary>
    /// Client device identifier (hashed).
    /// </summary>
    [MaxLength(64)]
    public string? DeviceId { get; init; }

    /// <summary>
    /// Client application version.
    /// </summary>
    [MaxLength(32)]
    public string? ClientVersion { get; init; }
}

/// <summary>
/// Playback event types.
/// </summary>
public static class PlaybackEventTypes
{
    public const string PlayStart = "play_start";
    public const string PlayStop = "play_stop";
    public const string PlayProgress = "play_progress";
    public const string PlayComplete = "play_complete";
    public const string Seek = "seek";

    public static readonly HashSet<string> Valid = new(StringComparer.OrdinalIgnoreCase)
    {
        PlayStart, PlayStop, PlayProgress, PlayComplete, Seek
    };
}

/// <summary>
/// Request for batch telemetry ingestion.
/// </summary>
public sealed class PlaybackEventBatchRequest
{
    /// <summary>
    /// List of playback events to ingest.
    /// </summary>
    [Required]
    public IReadOnlyList<PlaybackEventRequest> Events { get; init; } = [];
}
