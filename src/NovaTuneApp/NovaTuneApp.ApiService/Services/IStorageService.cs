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
}

/// <summary>
/// Result of presigned URL generation.
/// </summary>
public record PresignedUploadResult(string Url, DateTimeOffset ExpiresAt);
