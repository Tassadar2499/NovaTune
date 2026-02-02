namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Playlist visibility settings (for future sharing support).
/// </summary>
public enum PlaylistVisibility : byte
{
    /// <summary>
    /// Only the owner can access.
    /// </summary>
    Private = 0,

    /// <summary>
    /// Anyone with the link can view (future).
    /// </summary>
    Unlisted = 1,

    /// <summary>
    /// Publicly discoverable (future).
    /// </summary>
    Public = 2
}
