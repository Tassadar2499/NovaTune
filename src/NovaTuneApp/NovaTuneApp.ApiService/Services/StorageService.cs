using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Registry;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Stub implementation of IStorageService with resilience scaffolding (NF-1.4).
/// Storage operations are wrapped with timeout, circuit breaker, and bulkhead policies.
/// </summary>
public class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public StorageService(
        ILogger<StorageService> logger,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _logger = logger;
        _resiliencePipeline = pipelineProvider.GetPipeline(ResilienceExtensions.StoragePipeline);
    }

    public async Task ScheduleDeletionAsync(Guid trackId, TimeSpan gracePeriod, CancellationToken ct = default)
    {
        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            // TODO: Replace stub with actual MinIO/S3 operations
            _logger.LogInformation(
                "StorageService.ScheduleDeletionAsync called for {TrackId} with grace period {GracePeriod} (stub)",
                trackId, gracePeriod);
            await Task.CompletedTask;
        }, ct);
    }
}
