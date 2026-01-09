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

    /// <inheritdoc />
    public bool HasSufficientDiskSpace()
    {
        var currentUsage = GetCurrentDiskUsageBytes();
        var maxBytes = (long)_options.MaxTempDiskSpaceMb * 1024 * 1024;

        if (currentUsage >= maxBytes)
        {
            _logger.LogWarning(
                "Temp disk space limit exceeded: {CurrentMb} MB used of {MaxMb} MB allowed",
                currentUsage / (1024 * 1024),
                _options.MaxTempDiskSpaceMb);
            return false;
        }

        // Also check available volume space - ensure at least 500MB free for safety margin
        var availableSpace = GetAvailableDiskSpaceBytes();
        const long minimumFreeSpace = 500 * 1024 * 1024; // 500 MB

        if (availableSpace < minimumFreeSpace)
        {
            _logger.LogWarning(
                "Insufficient disk space available: {AvailableMb} MB free (minimum {MinMb} MB required)",
                availableSpace / (1024 * 1024),
                minimumFreeSpace / (1024 * 1024));
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public long GetCurrentDiskUsageBytes()
    {
        if (!Directory.Exists(_options.TempDirectory))
        {
            return 0;
        }

        try
        {
            return new DirectoryInfo(_options.TempDirectory)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate temp directory size");
            return 0;
        }
    }

    /// <inheritdoc />
    public long GetAvailableDiskSpaceBytes()
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(_options.TempDirectory) ?? "/");
            return driveInfo.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get available disk space");
            return long.MaxValue; // Assume sufficient space on error
        }
    }

    /// <inheritdoc />
    public void CleanupOrphanedDirectories()
    {
        if (!Directory.Exists(_options.TempDirectory))
        {
            return;
        }

        try
        {
            var directories = Directory.GetDirectories(_options.TempDirectory);
            foreach (var dir in directories)
            {
                var dirInfo = new DirectoryInfo(dir);
                // Clean up directories older than 1 hour (abandoned processing)
                if (dirInfo.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1))
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                        _logger.LogInformation("Cleaned up orphaned temp directory: {Directory}", dir);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up orphaned directory: {Directory}", dir);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate temp directories for cleanup");
        }
    }
}
