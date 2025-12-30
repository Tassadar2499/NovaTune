namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Stub implementation of IStorageService for messaging handler dependencies.
/// </summary>
public class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;

    public StorageService(ILogger<StorageService> logger)
    {
        _logger = logger;
    }

    public Task ScheduleDeletionAsync(Guid trackId, TimeSpan gracePeriod, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "StorageService.ScheduleDeletionAsync called for {TrackId} with grace period {GracePeriod} (stub)",
            trackId, gracePeriod);
        return Task.CompletedTask;
    }
}
