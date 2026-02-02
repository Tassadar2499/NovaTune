using System.Diagnostics.Metrics;

namespace NovaTuneApp.Workers.Telemetry.Services;

/// <summary>
/// Provides custom metrics for the telemetry worker.
/// </summary>
public class TelemetryWorkerMetrics
{
    /// <summary>
    /// The name of the meter.
    /// </summary>
    public const string MeterName = "NovaTune.TelemetryWorker";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter for telemetry events processed.
    /// Tags: event_type, status (success/failure)
    /// </summary>
    public static readonly Counter<long> EventsProcessed = Meter.CreateCounter<long>(
        name: "novatune.telemetry_worker.events_processed",
        unit: "{events}",
        description: "Total number of telemetry events processed by the worker");

    /// <summary>
    /// Histogram for event processing duration.
    /// Tags: event_type
    /// </summary>
    public static readonly Histogram<double> ProcessingDuration = Meter.CreateHistogram<double>(
        name: "novatune.telemetry_worker.processing_duration_ms",
        unit: "ms",
        description: "Duration of telemetry event processing");

    /// <summary>
    /// Counter for aggregation operations.
    /// Tags: aggregate_type (hourly/daily/user)
    /// </summary>
    public static readonly Counter<long> AggregationsTotal = Meter.CreateCounter<long>(
        name: "novatune.telemetry_worker.aggregations_total",
        unit: "{aggregations}",
        description: "Total number of aggregation operations");

    /// <summary>
    /// Gauge for consumer lag.
    /// </summary>
    public static readonly ObservableGauge<long> ConsumerLag = Meter.CreateObservableGauge(
        name: "novatune.telemetry_worker.consumer_lag",
        observeValue: () => 0L, // Would be populated from Kafka consumer metrics
        unit: "{messages}",
        description: "Consumer lag in messages");
}
