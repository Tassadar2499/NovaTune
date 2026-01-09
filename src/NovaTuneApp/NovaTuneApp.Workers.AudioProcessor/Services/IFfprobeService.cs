using NovaTuneApp.ApiService.Models;

namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Service for extracting audio metadata using ffprobe.
/// Implements Req 3.3 per 03-metadata-extraction.md.
/// </summary>
public interface IFfprobeService
{
    /// <summary>
    /// Extracts audio metadata from a file using ffprobe.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>Extracted audio metadata.</returns>
    /// <exception cref="FfprobeException">Thrown when ffprobe fails or times out.</exception>
    Task<AudioMetadata> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken);
}

/// <summary>
/// Exception thrown when ffprobe operations fail.
/// </summary>
public class FfprobeException : Exception
{
    /// <summary>
    /// The failure reason code for track status update.
    /// </summary>
    public string FailureReason { get; }

    public FfprobeException(string message, string failureReason)
        : base(message)
    {
        FailureReason = failureReason;
    }

    public FfprobeException(string message, string failureReason, Exception innerException)
        : base(message, innerException)
    {
        FailureReason = failureReason;
    }
}
