namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Manages temporary file storage for audio processing.
/// Implements cleanup strategy per 02-processing-pipeline.md.
/// </summary>
public interface ITempFileManager
{
    /// <summary>
    /// Creates a temp directory for the given track and returns the path.
    /// Directory: /tmp/novatune-processing/{TrackId}/
    /// </summary>
    /// <param name="trackId">The track ID to create a directory for.</param>
    /// <returns>Path to the created temp directory.</returns>
    string CreateTempDirectory(string trackId);

    /// <summary>
    /// Gets the path for a temp file within the track's temp directory.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>Full path to the temp file.</returns>
    string GetTempFilePath(string trackId, string fileName);

    /// <summary>
    /// Cleans up the temp directory for the given track.
    /// Should always be called in finally block regardless of success/failure.
    /// </summary>
    /// <param name="trackId">The track ID to clean up.</param>
    void CleanupTempDirectory(string trackId);

    /// <summary>
    /// Gets the base temp directory path.
    /// </summary>
    string BaseTempDirectory { get; }

    /// <summary>
    /// Checks if there is sufficient disk space for processing.
    /// Per 10-resilience.md: fail fast if temp disk space exceeds 2 GB limit.
    /// </summary>
    /// <returns>True if sufficient space is available; false otherwise.</returns>
    bool HasSufficientDiskSpace();

    /// <summary>
    /// Gets the current disk usage in the temp directory.
    /// </summary>
    /// <returns>Current disk usage in bytes.</returns>
    long GetCurrentDiskUsageBytes();

    /// <summary>
    /// Gets the available disk space on the temp directory volume.
    /// </summary>
    /// <returns>Available disk space in bytes.</returns>
    long GetAvailableDiskSpaceBytes();

    /// <summary>
    /// Cleans up all orphaned temp directories (from crashed/abandoned processing).
    /// </summary>
    void CleanupOrphanedDirectories();
}
