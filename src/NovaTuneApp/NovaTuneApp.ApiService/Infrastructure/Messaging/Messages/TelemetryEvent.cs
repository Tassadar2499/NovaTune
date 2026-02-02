namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

/// <summary>
/// Internal telemetry event for Kafka/Redpanda pipeline (Req 9.1, 9.4).
/// </summary>
public record TelemetryEvent
{
    /// <summary>
    /// Schema version for backwards compatibility.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Event type from client.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Track ID (ULID) - used as partition key (Req 9.5).
    /// </summary>
    public required string TrackId { get; init; }

    /// <summary>
    /// User who triggered the event.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Client-reported timestamp.
    /// </summary>
    public required DateTimeOffset ClientTimestamp { get; init; }

    /// <summary>
    /// Server-received timestamp.
    /// </summary>
    public required DateTimeOffset ServerTimestamp { get; init; }

    /// <summary>
    /// Playback position in seconds.
    /// </summary>
    public double? PositionSeconds { get; init; }

    /// <summary>
    /// Duration played in seconds.
    /// </summary>
    public double? DurationPlayedSeconds { get; init; }

    /// <summary>
    /// Session identifier for grouping.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Hashed device identifier.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// Client version.
    /// </summary>
    public string? ClientVersion { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing (Req 9.3).
    /// </summary>
    public required string CorrelationId { get; init; }
}
