using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.Workers.Lifecycle.Configuration;

/// <summary>
/// Configuration options for the lifecycle worker.
/// </summary>
public class LifecycleOptions
{
    public const string SectionName = "Lifecycle";

    /// <summary>
    /// Interval between polling runs for tracks pending physical deletion.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of tracks to process per polling run.
    /// Default: 50.
    /// </summary>
    [Range(1, 500)]
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Maximum number of concurrent deletion operations.
    /// Default: 10.
    /// </summary>
    [Range(1, 50)]
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// Retry delay for failed deletions before next attempt.
    /// Default: 1 minute.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum number of retry attempts for failed deletions.
    /// Default: 3.
    /// </summary>
    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 3;
}
