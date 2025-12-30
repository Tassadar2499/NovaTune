namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

/// <summary>
/// Event published when a track is deleted.
/// </summary>
public record TrackDeletedEvent
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid TrackId { get; init; }
    public required Guid UserId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
