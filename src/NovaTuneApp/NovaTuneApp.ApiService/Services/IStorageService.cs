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
}
