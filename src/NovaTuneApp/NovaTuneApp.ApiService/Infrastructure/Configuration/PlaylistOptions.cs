using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Configuration options for playlist management API (Stage 6).
/// </summary>
public class PlaylistOptions
{
    public const string SectionName = "Playlists";

    /// <summary>
    /// Maximum playlists per user.
    /// Default: 200 (per NF-2.4).
    /// </summary>
    [Range(1, 10000, ErrorMessage = "MaxPlaylistsPerUser must be between 1 and 10000")]
    public int MaxPlaylistsPerUser { get; set; } = 200;

    /// <summary>
    /// Maximum tracks per playlist.
    /// Default: 10,000 (per NF-2.4).
    /// </summary>
    [Range(1, 100000, ErrorMessage = "MaxTracksPerPlaylist must be between 1 and 100000")]
    public int MaxTracksPerPlaylist { get; set; } = 10_000;

    /// <summary>
    /// Maximum tracks to add in a single request.
    /// Default: 100.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "MaxTracksPerAddRequest must be between 1 and 1000")]
    public int MaxTracksPerAddRequest { get; set; } = 100;

    /// <summary>
    /// Maximum move operations per reorder request.
    /// Default: 50.
    /// </summary>
    [Range(1, 500, ErrorMessage = "MaxMovesPerReorderRequest must be between 1 and 500")]
    public int MaxMovesPerReorderRequest { get; set; } = 50;

    /// <summary>
    /// Maximum playlist name length.
    /// Default: 100.
    /// </summary>
    [Range(1, 500, ErrorMessage = "MaxNameLength must be between 1 and 500")]
    public int MaxNameLength { get; set; } = 100;

    /// <summary>
    /// Maximum playlist description length.
    /// Default: 500.
    /// </summary>
    [Range(0, 5000, ErrorMessage = "MaxDescriptionLength must be between 0 and 5000")]
    public int MaxDescriptionLength { get; set; } = 500;

    /// <summary>
    /// Default page size for playlist list.
    /// Default: 20.
    /// </summary>
    [Range(1, 100, ErrorMessage = "DefaultPageSize must be between 1 and 100")]
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Maximum page size for playlist list.
    /// Default: 50.
    /// </summary>
    [Range(1, 500, ErrorMessage = "MaxPageSize must be between 1 and 500")]
    public int MaxPageSize { get; set; } = 50;
}
