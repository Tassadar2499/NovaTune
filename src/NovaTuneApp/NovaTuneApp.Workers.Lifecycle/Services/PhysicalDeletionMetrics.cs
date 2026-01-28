using System.Diagnostics.Metrics;

namespace NovaTuneApp.Workers.Lifecycle.Services;

/// <summary>
/// Metrics for tracking physical deletion operations.
/// Provides observability into deletion throughput, failures, and storage freed.
/// </summary>
public class PhysicalDeletionMetrics
{
    private readonly Counter<long> _deletionsTotal;
    private readonly Counter<long> _deletionFailuresTotal;
    private readonly Histogram<double> _deletionDurationMs;
    private readonly Counter<long> _storageFreedBytesTotal;

    public PhysicalDeletionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("NovaTune.Lifecycle");

        _deletionsTotal = meter.CreateCounter<long>(
            "track_physical_deletions_total",
            description: "Total number of physical deletions");

        _deletionFailuresTotal = meter.CreateCounter<long>(
            "track_physical_deletion_failures_total",
            description: "Total number of failed physical deletions");

        _deletionDurationMs = meter.CreateHistogram<double>(
            "track_physical_deletion_duration_ms",
            unit: "ms",
            description: "Duration of physical deletion operations");

        _storageFreedBytesTotal = meter.CreateCounter<long>(
            "storage_freed_bytes_total",
            unit: "bytes",
            description: "Total bytes freed by physical deletions");
    }

    public void RecordDeletion(long freedBytes, double durationMs)
    {
        _deletionsTotal.Add(1);
        _storageFreedBytesTotal.Add(freedBytes);
        _deletionDurationMs.Record(durationMs);
    }

    public void RecordFailure()
    {
        _deletionFailuresTotal.Add(1);
    }
}
