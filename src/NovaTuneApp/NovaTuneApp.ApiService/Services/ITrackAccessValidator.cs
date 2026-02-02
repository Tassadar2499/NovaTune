namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Lightweight track access validator for telemetry endpoints.
/// Performs fast access checks without loading full track data.
/// </summary>
public interface ITrackAccessValidator
{
    /// <summary>
    /// Checks if a user has access to a track (for telemetry purposes).
    /// This is a lightweight check that validates:
    /// - Track exists
    /// - Track is not deleted
    /// - Track status allows playback (Ready status)
    /// - User has access (owner or public, depending on requirements)
    /// </summary>
    /// <param name="trackId">The track ID to check.</param>
    /// <param name="userId">The user ID requesting access.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user has access, false otherwise.</returns>
    Task<bool> HasAccessAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);
}
