using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.Workers.Telemetry.Services;

/// <summary>
/// Service for aggregating telemetry events into analytics data.
/// </summary>
public interface IAggregationService
{
    /// <summary>
    /// Processes a telemetry event and updates relevant aggregates.
    /// </summary>
    /// <param name="evt">The telemetry event to process.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessEventAsync(TelemetryEvent evt, CancellationToken ct = default);
}
