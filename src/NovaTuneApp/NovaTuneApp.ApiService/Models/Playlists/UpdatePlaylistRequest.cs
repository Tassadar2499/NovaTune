namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Request to update playlist metadata.
/// Uses nullable properties to distinguish between "not provided" and "set to null".
/// </summary>
public record UpdatePlaylistRequest
{
    /// <summary>
    /// New playlist name, if provided.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// New playlist description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether Description was explicitly set (allows clearing with null).
    /// </summary>
    public bool HasDescription { get; init; }
}
