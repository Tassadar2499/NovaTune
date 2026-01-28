namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for object storage operations (MinIO/S3).
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Schedules a track's storage objects for deletion after a grace period.
    /// </summary>
    Task ScheduleDeletionAsync(Guid trackId, TimeSpan gracePeriod, CancellationToken ct = default);

    /// <summary>
    /// Generates a presigned PUT URL for direct client upload to MinIO.
    /// </summary>
    /// <param name="objectKey">The storage object key.</param>
    /// <param name="contentType">Expected content type.</param>
    /// <param name="contentLength">Expected content length in bytes.</param>
    /// <param name="expiry">URL expiry duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Presigned URL and expiry timestamp.</returns>
    Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        long contentLength,
        TimeSpan expiry,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads an object to a local file using streaming IO (NF-2.4).
    /// Uses default 10s timeout.
    /// </summary>
    /// <param name="objectKey">The storage object key.</param>
    /// <param name="destinationPath">Local file path to write to.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DownloadToFileAsync(string objectKey, string destinationPath, CancellationToken ct = default);

    /// <summary>
    /// Downloads a large object to a local file using streaming IO (NF-2.4).
    /// Uses 5-minute timeout for files up to 500 MB per 10-resilience.md.
    /// </summary>
    /// <param name="objectKey">The storage object key.</param>
    /// <param name="destinationPath">Local file path to write to.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DownloadLargeFileAsync(string objectKey, string destinationPath, CancellationToken ct = default);

    /// <summary>
    /// Uploads a local file to storage using streaming IO (NF-2.4).
    /// </summary>
    /// <param name="objectKey">The storage object key.</param>
    /// <param name="sourcePath">Local file path to read from.</param>
    /// <param name="contentType">Content type of the file.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UploadFromFileAsync(string objectKey, string sourcePath, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Generates a presigned GET URL for streaming (Req 5.2).
    /// </summary>
    /// <param name="objectKey">The storage object key.</param>
    /// <param name="expiry">URL expiry duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Presigned URL and expiry timestamp.</returns>
    Task<PresignedDownloadResult> GeneratePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes an object from storage (Stage 5).
    /// </summary>
    /// <param name="objectKey">The storage object key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteObjectAsync(string objectKey, CancellationToken ct = default);
}

/// <summary>
/// Result of presigned upload URL generation.
/// </summary>
public record PresignedUploadResult(string Url, DateTimeOffset ExpiresAt);

/// <summary>
/// Result of presigned download URL generation.
/// </summary>
public record PresignedDownloadResult(string Url, DateTimeOffset ExpiresAt);
