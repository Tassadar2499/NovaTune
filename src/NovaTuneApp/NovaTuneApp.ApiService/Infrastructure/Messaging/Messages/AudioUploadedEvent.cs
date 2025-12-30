namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

/// <summary>
/// Event published when an audio file is uploaded.
/// </summary>
public record AudioUploadedEvent
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid TrackId { get; init; }
    public required Guid UserId { get; init; }
    public required string ObjectKey { get; init; }
    public required string MimeType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
