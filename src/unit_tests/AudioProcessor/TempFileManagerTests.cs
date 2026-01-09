using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NovaTuneApp.Workers.AudioProcessor.Services;

namespace NovaTune.UnitTests.AudioProcessor;

/// <summary>
/// Unit tests for TempFileManager.
/// Tests file/directory creation, cleanup, and disk space checks.
/// </summary>
public class TempFileManagerTests : IDisposable
{
    private readonly string _testTempDirectory;
    private readonly TempFileManager _tempFileManager;

    public TempFileManagerTests()
    {
        // Use a unique temp directory for each test
        _testTempDirectory = Path.Combine(Path.GetTempPath(), $"novatune-test-{Guid.NewGuid():N}");

        var options = Options.Create(new AudioProcessorOptions
        {
            TempDirectory = _testTempDirectory,
            MaxTempDiskSpaceMb = 2048 // 2 GB
        });

        _tempFileManager = new TempFileManager(
            NullLogger<TempFileManager>.Instance,
            options);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testTempDirectory))
        {
            try
            {
                Directory.Delete(_testTempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        GC.SuppressFinalize(this);
    }

    // ============================================================================
    // Directory Creation Tests
    // ============================================================================

    [Fact]
    public void CreateTempDirectory_Should_create_directory_for_track()
    {
        var trackId = Ulid.NewUlid().ToString();

        var path = _tempFileManager.CreateTempDirectory(trackId);

        Directory.Exists(path).ShouldBeTrue();
        path.ShouldContain(trackId);
    }

    [Fact]
    public void CreateTempDirectory_Should_return_path_under_base_directory()
    {
        var trackId = Ulid.NewUlid().ToString();

        var path = _tempFileManager.CreateTempDirectory(trackId);

        path.ShouldStartWith(_testTempDirectory);
    }

    [Fact]
    public void CreateTempDirectory_Should_cleanup_existing_directory_first()
    {
        var trackId = Ulid.NewUlid().ToString();

        // Create directory with a file
        var firstPath = _tempFileManager.CreateTempDirectory(trackId);
        var testFile = Path.Combine(firstPath, "test.txt");
        File.WriteAllText(testFile, "test content");
        File.Exists(testFile).ShouldBeTrue();

        // Create again - should clean up first
        var secondPath = _tempFileManager.CreateTempDirectory(trackId);

        secondPath.ShouldBe(firstPath);
        Directory.Exists(secondPath).ShouldBeTrue();
        File.Exists(testFile).ShouldBeFalse(); // File should be gone
    }

    [Fact]
    public void CreateTempDirectory_Should_handle_multiple_tracks()
    {
        var trackId1 = Ulid.NewUlid().ToString();
        var trackId2 = Ulid.NewUlid().ToString();

        var path1 = _tempFileManager.CreateTempDirectory(trackId1);
        var path2 = _tempFileManager.CreateTempDirectory(trackId2);

        path1.ShouldNotBe(path2);
        Directory.Exists(path1).ShouldBeTrue();
        Directory.Exists(path2).ShouldBeTrue();
    }

    // ============================================================================
    // File Path Tests
    // ============================================================================

    [Fact]
    public void GetTempFilePath_Should_return_correct_path()
    {
        var trackId = Ulid.NewUlid().ToString();
        var fileName = "audio.mp3";

        var path = _tempFileManager.GetTempFilePath(trackId, fileName);

        path.ShouldBe(Path.Combine(_testTempDirectory, trackId, fileName));
    }

    [Fact]
    public void GetTempFilePath_Should_work_with_nested_filename()
    {
        var trackId = Ulid.NewUlid().ToString();
        var fileName = "peaks.json";

        var path = _tempFileManager.GetTempFilePath(trackId, fileName);

        path.ShouldEndWith(fileName);
        path.ShouldContain(trackId);
    }

    // ============================================================================
    // Cleanup Tests
    // ============================================================================

    [Fact]
    public void CleanupTempDirectory_Should_delete_directory()
    {
        var trackId = Ulid.NewUlid().ToString();
        var path = _tempFileManager.CreateTempDirectory(trackId);
        File.WriteAllText(Path.Combine(path, "test.txt"), "content");

        _tempFileManager.CleanupTempDirectory(trackId);

        Directory.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void CleanupTempDirectory_Should_delete_all_files()
    {
        var trackId = Ulid.NewUlid().ToString();
        var path = _tempFileManager.CreateTempDirectory(trackId);

        // Create multiple files
        File.WriteAllText(Path.Combine(path, "audio.mp3"), "audio");
        File.WriteAllText(Path.Combine(path, "peaks.json"), "peaks");
        File.WriteAllText(Path.Combine(path, "temp.bin"), "temp");

        _tempFileManager.CleanupTempDirectory(trackId);

        Directory.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void CleanupTempDirectory_Should_not_throw_for_nonexistent_directory()
    {
        var trackId = Ulid.NewUlid().ToString();

        // Should not throw
        Should.NotThrow(() => _tempFileManager.CleanupTempDirectory(trackId));
    }

    [Fact]
    public void CleanupTempDirectory_Should_not_affect_other_tracks()
    {
        var trackId1 = Ulid.NewUlid().ToString();
        var trackId2 = Ulid.NewUlid().ToString();

        var path1 = _tempFileManager.CreateTempDirectory(trackId1);
        var path2 = _tempFileManager.CreateTempDirectory(trackId2);

        _tempFileManager.CleanupTempDirectory(trackId1);

        Directory.Exists(path1).ShouldBeFalse();
        Directory.Exists(path2).ShouldBeTrue();
    }

    // ============================================================================
    // Base Directory Tests
    // ============================================================================

    [Fact]
    public void BaseTempDirectory_Should_return_configured_path()
    {
        _tempFileManager.BaseTempDirectory.ShouldBe(_testTempDirectory);
    }

    [Fact]
    public void Constructor_Should_create_base_directory_if_not_exists()
    {
        var newTempDir = Path.Combine(Path.GetTempPath(), $"novatune-new-{Guid.NewGuid():N}");

        try
        {
            var options = Options.Create(new AudioProcessorOptions
            {
                TempDirectory = newTempDir
            });

            var manager = new TempFileManager(
                NullLogger<TempFileManager>.Instance,
                options);

            Directory.Exists(newTempDir).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(newTempDir))
            {
                Directory.Delete(newTempDir, recursive: true);
            }
        }
    }

    // ============================================================================
    // Disk Space Tests
    // ============================================================================

    [Fact]
    public void HasSufficientDiskSpace_Should_return_true_when_under_limit()
    {
        // Empty directory should be well under limit
        var result = _tempFileManager.HasSufficientDiskSpace();

        result.ShouldBeTrue();
    }

    [Fact]
    public void GetCurrentDiskUsageBytes_Should_return_zero_for_empty_directory()
    {
        var usage = _tempFileManager.GetCurrentDiskUsageBytes();

        usage.ShouldBe(0);
    }

    [Fact]
    public void GetCurrentDiskUsageBytes_Should_return_correct_size()
    {
        var trackId = Ulid.NewUlid().ToString();
        var path = _tempFileManager.CreateTempDirectory(trackId);

        // Write a known size
        var testData = new byte[1024]; // 1 KB
        File.WriteAllBytes(Path.Combine(path, "test.bin"), testData);

        var usage = _tempFileManager.GetCurrentDiskUsageBytes();

        usage.ShouldBe(1024);
    }

    [Fact]
    public void GetCurrentDiskUsageBytes_Should_sum_all_files()
    {
        var trackId1 = Ulid.NewUlid().ToString();
        var trackId2 = Ulid.NewUlid().ToString();

        var path1 = _tempFileManager.CreateTempDirectory(trackId1);
        var path2 = _tempFileManager.CreateTempDirectory(trackId2);

        File.WriteAllBytes(Path.Combine(path1, "file1.bin"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(path2, "file2.bin"), new byte[2048]);

        var usage = _tempFileManager.GetCurrentDiskUsageBytes();

        usage.ShouldBe(3072); // 1024 + 2048
    }

    [Fact]
    public void GetAvailableDiskSpaceBytes_Should_return_positive_value()
    {
        var available = _tempFileManager.GetAvailableDiskSpaceBytes();

        available.ShouldBeGreaterThan(0);
    }

    // ============================================================================
    // Orphaned Directory Cleanup Tests
    // ============================================================================

    [Fact]
    public void CleanupOrphanedDirectories_Should_not_throw_for_empty_directory()
    {
        Should.NotThrow(() => _tempFileManager.CleanupOrphanedDirectories());
    }

    [Fact]
    public void CleanupOrphanedDirectories_Should_not_delete_recent_directories()
    {
        var trackId = Ulid.NewUlid().ToString();
        var path = _tempFileManager.CreateTempDirectory(trackId);
        File.WriteAllText(Path.Combine(path, "test.txt"), "content");

        _tempFileManager.CleanupOrphanedDirectories();

        // Recently created directory should still exist
        Directory.Exists(path).ShouldBeTrue();
    }
}
