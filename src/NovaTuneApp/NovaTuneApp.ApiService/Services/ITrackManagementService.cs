using NovaTuneApp.ApiService.Models;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for track CRUD operations with soft-delete support.
/// </summary>
public interface ITrackManagementService
{
    /// <summary>
    /// Lists tracks for a user with pagination, filtering, and sorting.
    /// </summary>
    Task<PagedResult<TrackListItem>> ListTracksAsync(
        string userId,
        TrackListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Gets track details by ID.
    /// </summary>
    Task<TrackDetails> GetTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates track metadata (title, artist).
    /// </summary>
    Task<TrackDetails> UpdateTrackAsync(
        string trackId,
        string userId,
        UpdateTrackRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a track.
    /// </summary>
    Task DeleteTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Restores a soft-deleted track within the grace period.
    /// </summary>
    Task<TrackDetails> RestoreTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);
}
