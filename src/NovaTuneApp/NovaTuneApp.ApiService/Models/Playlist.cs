using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Represents a user-owned playlist with ordered track references.
/// </summary>
public sealed class Playlist
{
    /// <summary>
    /// RavenDB document ID (e.g., "Playlists/{PlaylistId}").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Public ULID identifier.
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string PlaylistId { get; init; } = string.Empty;

    /// <summary>
    /// Owner user ID (ULID).
    /// </summary>
    [Required]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Playlist display name.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Ordered list of track entries.
    /// </summary>
    public List<PlaylistTrackEntry> Tracks { get; set; } = [];

    /// <summary>
    /// Total number of tracks (denormalized for list queries).
    /// </summary>
    public int TrackCount { get; set; }

    /// <summary>
    /// Total duration of all tracks (denormalized).
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Playlist visibility for future sharing support.
    /// </summary>
    public PlaylistVisibility Visibility { get; set; } = PlaylistVisibility.Private;

    /// <summary>
    /// Timestamp when the playlist was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Timestamp of the last modification.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
