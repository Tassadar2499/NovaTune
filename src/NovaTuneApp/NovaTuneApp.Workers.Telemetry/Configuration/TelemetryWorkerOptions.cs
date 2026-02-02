namespace NovaTuneApp.Workers.Telemetry.Configuration;

/// <summary>
/// Configuration options for the telemetry aggregation worker.
/// </summary>
public class TelemetryWorkerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TelemetryWorker";

    /// <summary>
    /// Number of days to retain analytics aggregates.
    /// Default: 30 (per NF-6.3).
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Number of Kafka consumer workers.
    /// Default: 3.
    /// </summary>
    public int ConsumerWorkerCount { get; set; } = 3;

    /// <summary>
    /// Consumer buffer size.
    /// Default: 100.
    /// </summary>
    public int ConsumerBufferSize { get; set; } = 100;

    /// <summary>
    /// Maximum retry attempts for aggregation operations.
    /// Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds.
    /// Default: 1000.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
}
