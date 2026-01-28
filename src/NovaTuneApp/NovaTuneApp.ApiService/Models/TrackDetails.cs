namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Full track details including metadata and soft-delete information.
/// </summary>
public record TrackDetails(
    string TrackId,
    string Title,
    string? Artist,
    TimeSpan Duration,
    TrackStatus Status,
    long FileSizeBytes,
    string MimeType,
    AudioMetadata? Metadata,
    bool HasWaveform,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset? DeletedAt,
    DateTimeOffset? ScheduledDeletionAt);
