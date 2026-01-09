using NovaTuneApp.Workers.AudioProcessor.Services;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Fake implementation of ITempFileManager for unit testing.
/// </summary>
public class TempFileManagerFake : ITempFileManager
{
    private readonly string _baseTempDirectory;

    public TempFileManagerFake()
    {
        // Use a unique temp directory per test instance
        _baseTempDirectory = Path.Combine(Path.GetTempPath(), $"novatune-test-{Guid.NewGuid():N}");
    }

    /// <summary>
    /// Set of track IDs for which directories were created.
    /// </summary>
    public HashSet<string> CreatedDirectories { get; } = new();

    /// <summary>
    /// Set of track IDs for which directories were cleaned up.
    /// </summary>
    public HashSet<string> CleanedDirectories { get; } = new();

    /// <summary>
    /// Whether HasSufficientDiskSpace should return true.
    /// </summary>
    public bool HasSufficientSpace { get; set; } = true;

    /// <summary>
    /// Current disk usage to return.
    /// </summary>
    public long CurrentDiskUsage { get; set; } = 0;

    /// <summary>
    /// Available disk space to return.
    /// </summary>
    public long AvailableDiskSpace { get; set; } = 10L * 1024 * 1024 * 1024; // 10 GB

    public string BaseTempDirectory => _baseTempDirectory;

    public string CreateTempDirectory(string trackId)
    {
        CreatedDirectories.Add(trackId);
        var path = Path.Combine(_baseTempDirectory, trackId);
        Directory.CreateDirectory(path);
        return path;
    }

    public void CleanupTempDirectory(string trackId)
    {
        CleanedDirectories.Add(trackId);
        var path = Path.Combine(_baseTempDirectory, trackId);
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    public string GetTempFilePath(string trackId, string fileName)
    {
        return Path.Combine(_baseTempDirectory, trackId, fileName);
    }

    public bool HasSufficientDiskSpace() => HasSufficientSpace;

    public long GetCurrentDiskUsageBytes() => CurrentDiskUsage;

    public long GetAvailableDiskSpaceBytes() => AvailableDiskSpace;

    public void CleanupOrphanedDirectories()
    {
        // No-op in tests
    }

    /// <summary>
    /// Resets the fake to its initial state and cleans up temp directory.
    /// </summary>
    public void Reset()
    {
        CreatedDirectories.Clear();
        CleanedDirectories.Clear();
        HasSufficientSpace = true;
        CurrentDiskUsage = 0;
        AvailableDiskSpace = 10L * 1024 * 1024 * 1024;

        if (Directory.Exists(_baseTempDirectory))
        {
            try
            {
                Directory.Delete(_baseTempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Cleans up the test temp directory.
    /// Call this in test cleanup/dispose.
    /// </summary>
    public void Dispose()
    {
        Reset();
    }
}
