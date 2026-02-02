namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Request to create a new playlist.
/// </summary>
public record CreatePlaylistRequest(
    string Name,
    string? Description = null);
