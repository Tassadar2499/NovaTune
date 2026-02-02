namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Query parameters for getting playlist details.
/// </summary>
public record PlaylistDetailQuery(
    bool IncludeTracks = true,
    string? TrackCursor = null,
    int TrackLimit = 50);
