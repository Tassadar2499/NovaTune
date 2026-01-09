using NovaTuneApp.ApiService.Models;
using NovaTuneApp.Workers.AudioProcessor.Services;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Fake implementation of IFfprobeService for unit testing.
/// </summary>
public class FfprobeServiceFake : IFfprobeService
{
    /// <summary>
    /// The metadata to return from ExtractMetadataAsync.
    /// If null, returns default valid metadata.
    /// </summary>
    public AudioMetadata? MetadataToReturn { get; set; }

    /// <summary>
    /// Exception to throw from ExtractMetadataAsync.
    /// If set, will be thrown instead of returning metadata.
    /// </summary>
    public FfprobeException? ExceptionToThrow { get; set; }

    /// <summary>
    /// List of file paths that were processed.
    /// </summary>
    public List<string> ProcessedFiles { get; } = new();

    public Task<AudioMetadata> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        ProcessedFiles.Add(filePath);

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(MetadataToReturn ?? CreateDefaultMetadata());
    }

    /// <summary>
    /// Creates valid default metadata for testing.
    /// </summary>
    public static AudioMetadata CreateDefaultMetadata() => new()
    {
        Duration = TimeSpan.FromMinutes(3),
        SampleRate = 44100,
        Channels = 2,
        BitRate = 320000,
        Codec = "mp3",
        CodecLongName = "MP3 (MPEG audio layer 3)",
        FileSizeBytes = 5_000_000,
        MimeType = "audio/mpeg"
    };

    /// <summary>
    /// Resets the fake to its initial state.
    /// </summary>
    public void Reset()
    {
        MetadataToReturn = null;
        ExceptionToThrow = null;
        ProcessedFiles.Clear();
    }
}
