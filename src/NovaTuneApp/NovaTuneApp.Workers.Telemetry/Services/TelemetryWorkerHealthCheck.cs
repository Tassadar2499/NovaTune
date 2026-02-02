using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NovaTuneApp.Workers.Telemetry.Services;

/// <summary>
/// Health check for the telemetry worker.
/// Reports unhealthy if the worker has not processed events recently.
/// </summary>
public class TelemetryWorkerHealthCheck : IHealthCheck
{
    private static DateTimeOffset _lastProcessedAt = DateTimeOffset.UtcNow;
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var timeSinceLastProcessed = DateTimeOffset.UtcNow - _lastProcessedAt;

        if (timeSinceLastProcessed > StaleThreshold)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"No events processed in {timeSinceLastProcessed.TotalMinutes:F1} minutes"));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Last event processed {timeSinceLastProcessed.TotalSeconds:F0} seconds ago"));
    }

    /// <summary>
    /// Records that an event was processed.
    /// Called by the event handler after successful processing.
    /// </summary>
    public static void RecordEventProcessed()
    {
        _lastProcessedAt = DateTimeOffset.UtcNow;
    }
}
