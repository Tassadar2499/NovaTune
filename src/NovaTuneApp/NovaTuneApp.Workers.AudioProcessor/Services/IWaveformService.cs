namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Service for generating audio waveform data using ffmpeg.
/// Implements per 04-waveform-generation.md.
/// </summary>
public interface IWaveformService
{
    /// <summary>
    /// Generates waveform peak data from an audio file.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file.</param>
    /// <param name="outputPath">Path to write the waveform JSON file.</param>
    /// <param name="peakCount">Number of peaks to generate.</param>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <exception cref="WaveformException">Thrown when waveform generation fails.</exception>
    Task GenerateAsync(string audioFilePath, string outputPath, int peakCount, CancellationToken cancellationToken);
}

/// <summary>
/// Exception thrown when waveform generation fails.
/// </summary>
public class WaveformException : Exception
{
    /// <summary>
    /// The failure reason code for track status update.
    /// </summary>
    public string FailureReason { get; }

    public WaveformException(string message, string failureReason)
        : base(message)
    {
        FailureReason = failureReason;
    }

    public WaveformException(string message, string failureReason, Exception innerException)
        : base(message, innerException)
    {
        FailureReason = failureReason;
    }
}
