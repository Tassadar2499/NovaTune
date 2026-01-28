using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.Workers.Lifecycle.Configuration;
using Raven.Client.Documents;

namespace NovaTuneApp.Workers.Lifecycle.Services;

/// <summary>
/// Health check that monitors the physical deletion backlog.
/// Reports degraded status if too many tracks are awaiting physical deletion.
/// </summary>
public class PhysicalDeletionHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<LifecycleOptions> _options;
    private readonly ILogger<PhysicalDeletionHealthCheck> _logger;

    private const int DegradedThreshold = 100;

    public PhysicalDeletionHealthCheck(
        IServiceProvider serviceProvider,
        IOptions<LifecycleOptions> options,
        ILogger<PhysicalDeletionHealthCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        try
        {
            using var session = store.OpenAsyncSession();

            // Check for stale deletions (older than 2x polling interval)
            var staleThreshold = DateTimeOffset.UtcNow.Add(-_options.Value.PollingInterval * 2);

            var staleCount = await session
                .Query<Track, Tracks_ByScheduledDeletion>()
                .Where(t => t.Status == TrackStatus.Deleted
                         && t.ScheduledDeletionAt <= staleThreshold)
                .CountAsync(ct);

            if (staleCount > DegradedThreshold)
            {
                _logger.LogWarning(
                    "Physical deletion backlog detected: {Count} tracks pending",
                    staleCount);

                return HealthCheckResult.Degraded(
                    $"Physical deletion backlog: {staleCount} tracks pending",
                    data: new Dictionary<string, object>
                    {
                        ["staleDeletionCount"] = staleCount,
                        ["threshold"] = DegradedThreshold
                    });
            }

            return HealthCheckResult.Healthy(
                $"Physical deletion queue healthy ({staleCount} pending)",
                data: new Dictionary<string, object>
                {
                    ["staleDeletionCount"] = staleCount
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check physical deletion health");
            return HealthCheckResult.Unhealthy("Failed to check deletion status", ex);
        }
    }
}
