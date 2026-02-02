namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Full playlist details with optional track list.
/// </summary>
public record PlaylistDetails(
    string PlaylistId,
    string Name,
    string? Description,
    int TrackCount,
    TimeSpan TotalDuration,
    PlaylistVisibility Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PagedResult<PlaylistTrackItem>? Tracks);
