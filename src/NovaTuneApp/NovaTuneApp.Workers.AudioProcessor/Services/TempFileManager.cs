using Microsoft.Extensions.Options;

namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Implementation of temp file management for audio processing.
/// </summary>
public class TempFileManager : ITempFileManager
{
    private readonly ILogger<TempFileManager> _logger;
    private readonly AudioProcessorOptions _options;

    public TempFileManager(
        ILogger<TempFileManager> logger,
        IOptions<AudioProcessorOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Ensure base directory exists
        if (!Directory.Exists(_options.TempDirectory))
        {
            Directory.CreateDirectory(_options.TempDirectory);
            _logger.LogInformation("Created temp directory: {TempDirectory}", _options.TempDirectory);
        }
    }

    public string BaseTempDirectory => _options.TempDirectory;

    public string CreateTempDirectory(string trackId)
    {
        var trackTempDir = GetTrackTempDirectory(trackId);

        if (Directory.Exists(trackTempDir))
        {
            _logger.LogWarning(
                "Temp directory already exists for track {TrackId}, cleaning up first",
                trackId);
            CleanupTempDirectory(trackId);
        }

        Directory.CreateDirectory(trackTempDir);
        _logger.LogDebug("Created temp directory for track {TrackId}: {Path}", trackId, trackTempDir);

        return trackTempDir;
    }

    public string GetTempFilePath(string trackId, string fileName)
    {
        var trackTempDir = GetTrackTempDirectory(trackId);
        return Path.Combine(trackTempDir, fileName);
    }

    public void CleanupTempDirectory(string trackId)
    {
        var trackTempDir = GetTrackTempDirectory(trackId);

        if (!Directory.Exists(trackTempDir))
        {
            _logger.LogDebug("Temp directory does not exist for track {TrackId}, nothing to clean", trackId);
            return;
        }

        try
        {
            Directory.Delete(trackTempDir, recursive: true);
            _logger.LogDebug("Cleaned up temp directory for track {TrackId}", trackId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to clean up temp directory for track {TrackId}: {Path}",
                trackId,
                trackTempDir);
        }
    }

    private string GetTrackTempDirectory(string trackId)
    {
        return Path.Combine(_options.TempDirectory, trackId);
    }
}
