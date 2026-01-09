using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Fake implementation of IStorageService for unit testing.
/// </summary>
public class StorageServiceFake : IStorageService
{
    /// <summary>
    /// In-memory object storage.
    /// </summary>
    public Dictionary<string, byte[]> Objects { get; } = new();

    /// <summary>
    /// List of object keys that were downloaded.
    /// </summary>
    public List<string> DownloadedKeys { get; } = new();

    /// <summary>
    /// List of uploaded files (key, path, contentType).
    /// </summary>
    public List<(string Key, string Path, string ContentType)> UploadedFiles { get; } = new();

    /// <summary>
    /// List of deleted object keys.
    /// </summary>
    public List<string> DeletedKeys { get; } = new();

    /// <summary>
    /// List of scheduled deletions (trackId, gracePeriod).
    /// </summary>
    public List<(Guid TrackId, TimeSpan GracePeriod)> ScheduledDeletions { get; } = new();

    /// <summary>
    /// Custom callback for DownloadLargeFileAsync.
    /// </summary>
    public Func<string, string, CancellationToken, Task>? OnDownloadLargeFileAsync { get; set; }

    /// <summary>
    /// Custom callback for UploadFromFileAsync.
    /// </summary>
    public Func<string, string, string, CancellationToken, Task>? OnUploadFromFileAsync { get; set; }

    /// <summary>
    /// Exception to throw from operations.
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    public Task ScheduleDeletionAsync(Guid trackId, TimeSpan gracePeriod, CancellationToken ct = default)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        ScheduledDeletions.Add((trackId, gracePeriod));
        return Task.CompletedTask;
    }

    public Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        long contentLength,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        var url = $"https://storage.example.com/{objectKey}?presigned=true";
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        return Task.FromResult(new PresignedUploadResult(url, expiresAt));
    }

    public Task<PresignedDownloadResult> GeneratePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        var url = $"https://storage.example.com/{objectKey}?presigned=true&action=download";
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        return Task.FromResult(new PresignedDownloadResult(url, expiresAt));
    }

    public async Task DownloadToFileAsync(string objectKey, string destinationPath, CancellationToken ct = default)
    {
        await DownloadLargeFileAsync(objectKey, destinationPath, ct);
    }

    public async Task DownloadLargeFileAsync(string objectKey, string destinationPath, CancellationToken ct = default)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        DownloadedKeys.Add(objectKey);

        if (OnDownloadLargeFileAsync != null)
        {
            await OnDownloadLargeFileAsync(objectKey, destinationPath, ct);
            return;
        }

        if (Objects.TryGetValue(objectKey, out var data))
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(destinationPath, data, ct);
        }
        else
        {
            throw new FileNotFoundException($"Object not found: {objectKey}");
        }
    }

    public async Task UploadFromFileAsync(string objectKey, string sourcePath, string contentType, CancellationToken ct = default)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        UploadedFiles.Add((objectKey, sourcePath, contentType));

        if (OnUploadFromFileAsync != null)
        {
            await OnUploadFromFileAsync(objectKey, sourcePath, contentType, ct);
            return;
        }

        if (File.Exists(sourcePath))
        {
            Objects[objectKey] = await File.ReadAllBytesAsync(sourcePath, ct);
        }
    }

    /// <summary>
    /// Resets the fake to its initial state.
    /// </summary>
    public void Reset()
    {
        Objects.Clear();
        DownloadedKeys.Clear();
        UploadedFiles.Clear();
        DeletedKeys.Clear();
        ScheduledDeletions.Clear();
        OnDownloadLargeFileAsync = null;
        OnUploadFromFileAsync = null;
        ExceptionToThrow = null;
    }
}
