namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Track item within a playlist.
/// </summary>
public record PlaylistTrackItem(
    int Position,
    string TrackId,
    string Title,
    string? Artist,
    TimeSpan Duration,
    TrackStatus Status,
    DateTimeOffset AddedAt);
