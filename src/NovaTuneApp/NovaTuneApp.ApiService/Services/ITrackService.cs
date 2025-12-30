namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for track-related business logic.
/// </summary>
public interface ITrackService
{
    /// <summary>
    /// Processes an uploaded track (e.g., extract metadata, generate waveform).
    /// </summary>
    Task ProcessUploadedTrackAsync(Guid trackId, CancellationToken ct = default);
}
