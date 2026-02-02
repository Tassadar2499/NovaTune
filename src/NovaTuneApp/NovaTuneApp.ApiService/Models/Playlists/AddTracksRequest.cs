namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Request to add tracks to a playlist.
/// </summary>
/// <param name="TrackIds">Track IDs to add (1-100 per request).</param>
/// <param name="Position">Insert position (null = append to end).</param>
public record AddTracksRequest(
    IReadOnlyList<string> TrackIds,
    int? Position = null);
