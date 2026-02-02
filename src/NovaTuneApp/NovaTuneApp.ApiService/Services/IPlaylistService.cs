using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Playlists;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for playlist CRUD operations.
/// </summary>
public interface IPlaylistService
{
    /// <summary>
    /// Lists playlists for a user with pagination and sorting.
    /// </summary>
    Task<PagedResult<PlaylistListItem>> ListPlaylistsAsync(
        string userId,
        PlaylistListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new playlist.
    /// </summary>
    Task<PlaylistDetails> CreatePlaylistAsync(
        string userId,
        CreatePlaylistRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets playlist details by ID.
    /// </summary>
    Task<PlaylistDetails> GetPlaylistAsync(
        string playlistId,
        string userId,
        PlaylistDetailQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Updates playlist metadata.
    /// </summary>
    Task<PlaylistDetails> UpdatePlaylistAsync(
        string playlistId,
        string userId,
        UpdatePlaylistRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a playlist.
    /// </summary>
    Task DeletePlaylistAsync(
        string playlistId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Adds tracks to a playlist.
    /// </summary>
    Task<PlaylistDetails> AddTracksAsync(
        string playlistId,
        string userId,
        AddTracksRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a track from a playlist by position.
    /// </summary>
    Task RemoveTrackAsync(
        string playlistId,
        string userId,
        int position,
        CancellationToken ct = default);

    /// <summary>
    /// Reorders tracks within a playlist.
    /// </summary>
    Task<PlaylistDetails> ReorderTracksAsync(
        string playlistId,
        string userId,
        ReorderRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Removes deleted track references from all user playlists.
    /// Called by lifecycle worker after track physical deletion.
    /// </summary>
    Task RemoveDeletedTrackReferencesAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);
}
