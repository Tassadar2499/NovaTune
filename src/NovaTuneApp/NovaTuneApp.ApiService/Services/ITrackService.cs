namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for track-related business logic.
/// </summary>
public interface ITrackService
{
    /// <summary>
    /// Processes an uploaded track (e.g., extract metadata, generate waveform).
    /// </summary>
    /// <param name="trackId">Track identifier (ULID string per cross-cutting decision 3.1).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessUploadedTrackAsync(string trackId, CancellationToken ct = default);
}
