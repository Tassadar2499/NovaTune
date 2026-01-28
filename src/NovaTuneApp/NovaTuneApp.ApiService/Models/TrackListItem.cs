namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Summary information for a track in list results.
/// </summary>
public record TrackListItem(
    string TrackId,
    string Title,
    string? Artist,
    TimeSpan Duration,
    TrackStatus Status,
    long FileSizeBytes,
    string MimeType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ProcessedAt);
