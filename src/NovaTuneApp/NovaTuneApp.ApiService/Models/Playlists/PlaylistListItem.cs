namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Playlist item in list response.
/// </summary>
public record PlaylistListItem(
    string PlaylistId,
    string Name,
    string? Description,
    int TrackCount,
    TimeSpan TotalDuration,
    PlaylistVisibility Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
