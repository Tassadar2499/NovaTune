namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

/// <summary>
/// Event published when a track is soft-deleted.
/// Used by lifecycle worker to schedule physical deletion.
/// </summary>
public record TrackDeletedEvent
{
    /// <summary>
    /// Schema version for backwards compatibility.
    /// Version 2 uses ULID strings instead of Guids.
    /// </summary>
    public int SchemaVersion { get; init; } = 2;

    /// <summary>
    /// Track identifier (ULID string).
    /// </summary>
    public required string TrackId { get; init; }

    /// <summary>
    /// User identifier (ULID string).
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// MinIO object key for the audio file.
    /// Used by lifecycle worker for physical deletion.
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// MinIO object key for waveform data (if exists).
    /// Used by lifecycle worker for physical deletion.
    /// </summary>
    public string? WaveformObjectKey { get; init; }

    /// <summary>
    /// File size in bytes for quota adjustment.
    /// </summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>
    /// When the track was soft-deleted.
    /// </summary>
    public required DateTimeOffset DeletedAt { get; init; }

    /// <summary>
    /// When physical deletion is scheduled.
    /// </summary>
    public required DateTimeOffset ScheduledDeletionAt { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Event timestamp.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}
