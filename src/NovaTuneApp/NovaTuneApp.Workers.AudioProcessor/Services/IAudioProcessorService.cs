using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Service interface for audio processing operations.
/// Implements Req 3.1 - Asynchronous audio processing.
/// </summary>
public interface IAudioProcessorService
{
    /// <summary>
    /// Process an uploaded audio track: extract metadata, generate waveform, update track status.
    /// </summary>
    /// <param name="event">The audio uploaded event containing track information.</param>
    /// <param name="cancellationToken">Cancellation token for operation timeout.</param>
    /// <returns>True if processing succeeded; false if it should be retried.</returns>
    Task<bool> ProcessAsync(AudioUploadedEvent @event, CancellationToken cancellationToken);
}
