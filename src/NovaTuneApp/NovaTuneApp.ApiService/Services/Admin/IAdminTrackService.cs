using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;

namespace NovaTuneApp.ApiService.Services.Admin;

/// <summary>
/// Service for admin track moderation operations (Req 11.2).
/// </summary>
public interface IAdminTrackService
{
    /// <summary>
    /// Lists tracks with search, filtering, and pagination.
    /// </summary>
    Task<PagedResult<AdminTrackListItem>> ListTracksAsync(
        AdminTrackListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Gets detailed track information for admin view.
    /// </summary>
    Task<AdminTrackDetails?> GetTrackAsync(
        string trackId,
        CancellationToken ct = default);

    /// <summary>
    /// Moderates a track with the specified status and reason.
    /// </summary>
    /// <exception cref="TrackNotFoundException">When track doesn't exist.</exception>
    /// <exception cref="InvalidReasonCodeException">When reason code is invalid.</exception>
    Task<AdminTrackDetails> ModerateTrackAsync(
        string trackId,
        ModerateTrackRequest request,
        string adminUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a track as admin (bypasses ownership check).
    /// </summary>
    /// <exception cref="TrackNotFoundException">When track doesn't exist.</exception>
    /// <exception cref="InvalidReasonCodeException">When reason code is invalid.</exception>
    Task DeleteTrackAsync(
        string trackId,
        AdminDeleteTrackRequest request,
        string adminUserId,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown when a track is not found.
/// </summary>
public class TrackNotFoundException : Exception
{
    public string TrackId { get; }

    public TrackNotFoundException(string trackId)
        : base($"Track '{trackId}' not found.")
    {
        TrackId = trackId;
    }
}
