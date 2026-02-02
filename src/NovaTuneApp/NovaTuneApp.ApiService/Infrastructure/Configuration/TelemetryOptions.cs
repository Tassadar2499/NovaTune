namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Configuration options for telemetry ingestion and analytics (Stage 7).
/// </summary>
public class TelemetryOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Analytics data retention in days.
    /// Default: 30 (per NF-6.3).
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Maximum age of accepted events in hours.
    /// Default: 24.
    /// </summary>
    public int MaxEventAgeHours { get; set; } = 24;

    /// <summary>
    /// Maximum future timestamp tolerance in minutes.
    /// Default: 5.
    /// </summary>
    public int MaxFutureMinutes { get; set; } = 5;

    /// <summary>
    /// Sampling rate for play_progress events under load (0.0-1.0).
    /// Default: 0.9 (keep 90%).
    /// </summary>
    public double ProgressEventSamplingRate { get; set; } = 0.9;

    /// <summary>
    /// Maximum events per batch request.
    /// Default: 50.
    /// </summary>
    public int MaxBatchSize { get; set; } = 50;

    /// <summary>
    /// Maximum event age for batch events in days.
    /// Default: 7.
    /// </summary>
    public int MaxBatchEventAgeDays { get; set; } = 7;

    /// <summary>
    /// Enable server-side sampling under load.
    /// Default: true.
    /// </summary>
    public bool EnableSampling { get; set; } = true;

    /// <summary>
    /// Load threshold (events/second) to trigger sampling.
    /// Default: 1000.
    /// </summary>
    public int SamplingLoadThreshold { get; set; } = 1000;
}
