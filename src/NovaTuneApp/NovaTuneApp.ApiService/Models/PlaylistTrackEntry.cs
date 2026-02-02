using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Represents a track entry within a playlist.
/// </summary>
public sealed class PlaylistTrackEntry
{
    /// <summary>
    /// Position in the playlist (0-based, stable ordering).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Reference to the track ID (ULID).
    /// </summary>
    [Required]
    public string TrackId { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when this track was added.
    /// </summary>
    public DateTimeOffset AddedAt { get; init; }
}
