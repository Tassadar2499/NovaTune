using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NovaTuneApp.ApiService.Infrastructure.Observability;

/// <summary>
/// Provides custom metrics for monitoring NovaTune application behavior.
/// Metrics follow OpenTelemetry semantic conventions where applicable.
/// </summary>
public static class NovaTuneMetrics
{
    /// <summary>
    /// The name of the meter, used for OpenTelemetry registration.
    /// </summary>
    public const string MeterName = "NovaTune.Api";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    // ============================================================================
    // Track Upload Metrics
    // ============================================================================

    /// <summary>
    /// Counter for the total number of tracks uploaded.
    /// Tags: format (mp3, flac, etc.), user.tier (free, premium)
    /// </summary>
    public static readonly Counter<long> TracksUploaded = Meter.CreateCounter<long>(
        name: "novatune.tracks.uploaded",
        unit: "{tracks}",
        description: "Total number of tracks uploaded");

    /// <summary>
    /// Histogram for track upload processing duration.
    /// </summary>
    public static readonly Histogram<double> UploadDuration = Meter.CreateHistogram<double>(
        name: "novatune.upload.duration",
        unit: "ms",
        description: "Audio upload processing duration in milliseconds");

    /// <summary>
    /// UpDownCounter for currently active uploads.
    /// </summary>
    public static readonly UpDownCounter<int> ActiveUploads = Meter.CreateUpDownCounter<int>(
        name: "novatune.uploads.active",
        unit: "{uploads}",
        description: "Number of currently processing uploads");

    /// <summary>
    /// Histogram for uploaded track file sizes.
    /// </summary>
    public static readonly Histogram<long> UploadSize = Meter.CreateHistogram<long>(
        name: "novatune.upload.size",
        unit: "By",
        description: "Size of uploaded audio files in bytes");

    // ============================================================================
    // Track Deletion Metrics
    // ============================================================================

    /// <summary>
    /// Counter for the total number of tracks deleted.
    /// </summary>
    public static readonly Counter<long> TracksDeleted = Meter.CreateCounter<long>(
        name: "novatune.tracks.deleted",
        unit: "{tracks}",
        description: "Total number of tracks deleted");

    // ============================================================================
    // Cache Metrics
    // ============================================================================

    /// <summary>
    /// Counter for cache hits.
    /// Tags: cache.type (session, presigned_url, etc.)
    /// </summary>
    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        name: "novatune.cache.hits",
        unit: "{hits}",
        description: "Number of cache hits");

    /// <summary>
    /// Counter for cache misses.
    /// Tags: cache.type (session, presigned_url, etc.)
    /// </summary>
    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        name: "novatune.cache.misses",
        unit: "{misses}",
        description: "Number of cache misses");

    // ============================================================================
    // Messaging Metrics
    // ============================================================================

    /// <summary>
    /// Counter for messages produced to Kafka.
    /// Tags: topic, message.type
    /// </summary>
    public static readonly Counter<long> MessagesProduced = Meter.CreateCounter<long>(
        name: "novatune.messages.produced",
        unit: "{messages}",
        description: "Number of messages produced to Kafka");

    /// <summary>
    /// Counter for messages consumed from Kafka.
    /// Tags: topic, consumer.group, message.type
    /// </summary>
    public static readonly Counter<long> MessagesConsumed = Meter.CreateCounter<long>(
        name: "novatune.messages.consumed",
        unit: "{messages}",
        description: "Number of messages consumed from Kafka");

    /// <summary>
    /// Histogram for message processing duration.
    /// </summary>
    public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
        name: "novatune.message.processing.duration",
        unit: "ms",
        description: "Message processing duration in milliseconds");

    // ============================================================================
    // Helper Methods
    // ============================================================================

    /// <summary>
    /// Records a track upload with associated metadata.
    /// </summary>
    public static void RecordTrackUpload(string format, string userTier, double durationMs, long sizeBytes)
    {
        var tags = new TagList
        {
            { "format", format },
            { "user.tier", userTier }
        };

        TracksUploaded.Add(1, tags);
        UploadDuration.Record(durationMs, tags);
        UploadSize.Record(sizeBytes, tags);
    }

    /// <summary>
    /// Records a cache operation result.
    /// </summary>
    public static void RecordCacheAccess(string cacheType, bool isHit)
    {
        var tags = new TagList { { "cache.type", cacheType } };

        if (isHit)
        {
            CacheHits.Add(1, tags);
        }
        else
        {
            CacheMisses.Add(1, tags);
        }
    }
}
