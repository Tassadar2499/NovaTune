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
    // Upload Initiation Metrics (NF-4.4)
    // ============================================================================

    /// <summary>
    /// Counter for upload initiation requests.
    /// Tags: status (success, error)
    /// </summary>
    public static readonly Counter<long> UploadInitiateTotal = Meter.CreateCounter<long>(
        name: "novatune.upload_initiate_total",
        unit: "{requests}",
        description: "Total number of upload initiation requests");

    /// <summary>
    /// Histogram for upload initiation duration.
    /// </summary>
    public static readonly Histogram<double> UploadInitiateDuration = Meter.CreateHistogram<double>(
        name: "novatune.upload_initiate_duration_ms",
        unit: "ms",
        description: "Upload initiation request duration in milliseconds");

    /// <summary>
    /// Counter for upload sessions created.
    /// </summary>
    public static readonly Counter<long> UploadSessionCreatedTotal = Meter.CreateCounter<long>(
        name: "novatune.upload_session_created_total",
        unit: "{sessions}",
        description: "Total number of upload sessions created");

    /// <summary>
    /// Counter for MinIO notifications received.
    /// </summary>
    public static readonly Counter<long> MinioNotificationReceivedTotal = Meter.CreateCounter<long>(
        name: "novatune.minio_notification_received_total",
        unit: "{notifications}",
        description: "Total number of MinIO notifications received");

    /// <summary>
    /// Counter for tracks created from uploads.
    /// </summary>
    public static readonly Counter<long> TrackCreatedTotal = Meter.CreateCounter<long>(
        name: "novatune.track_created_total",
        unit: "{tracks}",
        description: "Total number of tracks created from uploads");

    // ============================================================================
    // Audio Processing Metrics (NF-4.2)
    // ============================================================================

    /// <summary>
    /// Counter for audio processing started.
    /// </summary>
    public static readonly Counter<long> AudioProcessingStartedTotal = Meter.CreateCounter<long>(
        name: "novatune.audio_processing_started_total",
        unit: "{events}",
        description: "Total number of audio processing jobs started");

    /// <summary>
    /// Counter for audio processing completed successfully.
    /// </summary>
    public static readonly Counter<long> AudioProcessingCompletedTotal = Meter.CreateCounter<long>(
        name: "novatune.audio_processing_completed_total",
        unit: "{events}",
        description: "Total number of audio processing jobs completed successfully");

    /// <summary>
    /// Counter for audio processing failures.
    /// Tags: reason (timeout, validation_error, ffprobe_error, etc.)
    /// </summary>
    public static readonly Counter<long> AudioProcessingFailedTotal = Meter.CreateCounter<long>(
        name: "novatune.audio_processing_failed_total",
        unit: "{events}",
        description: "Total number of audio processing jobs that failed");

    /// <summary>
    /// Histogram for audio processing duration.
    /// </summary>
    public static readonly Histogram<double> AudioProcessingDuration = Meter.CreateHistogram<double>(
        name: "novatune.audio_processing_duration_ms",
        unit: "ms",
        description: "Audio processing duration in milliseconds");

    // ============================================================================
    // Outbox Metrics (NF-4.4)
    // ============================================================================

    /// <summary>
    /// Counter for outbox messages published.
    /// Tags: event_type
    /// </summary>
    public static readonly Counter<long> OutboxPublishedTotal = Meter.CreateCounter<long>(
        name: "novatune.outbox_published_total",
        unit: "{messages}",
        description: "Total number of outbox messages published");

    /// <summary>
    /// Counter for outbox messages that failed permanently.
    /// Tags: event_type
    /// </summary>
    public static readonly Counter<long> OutboxFailedTotal = Meter.CreateCounter<long>(
        name: "novatune.outbox_failed_total",
        unit: "{messages}",
        description: "Total number of outbox messages that failed permanently");

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
    // Authentication Metrics
    // ============================================================================

    /// <summary>
    /// Counter for authentication operations.
    /// Tags: operation (register, login, refresh, logout), result (success, failure, etc.)
    /// </summary>
    public static readonly Counter<long> AuthOperations = Meter.CreateCounter<long>(
        name: "novatune.auth.operations",
        unit: "{operations}",
        description: "Number of authentication operations");

    /// <summary>
    /// Histogram for authentication operation duration.
    /// Tags: operation (register, login, refresh, logout)
    /// </summary>
    public static readonly Histogram<double> AuthDuration = Meter.CreateHistogram<double>(
        name: "novatune.auth.duration",
        unit: "ms",
        description: "Authentication operation duration in milliseconds");

    /// <summary>
    /// Counter for rate limit violations.
    /// Tags: endpoint, policy
    /// </summary>
    public static readonly Counter<long> RateLimitViolations = Meter.CreateCounter<long>(
        name: "novatune.ratelimit.violations",
        unit: "{violations}",
        description: "Number of rate limit violations");

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

    /// <summary>
    /// Records an authentication operation.
    /// </summary>
    public static void IncrementAuthOperation(string operation, string result)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "result", result }
        };
        AuthOperations.Add(1, tags);
    }

    /// <summary>
    /// Records authentication operation duration.
    /// </summary>
    public static void RecordAuthDuration(string operation, double durationMs)
    {
        var tags = new TagList { { "operation", operation } };
        AuthDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a rate limit violation.
    /// </summary>
    public static void RecordRateLimitViolation(string endpoint, string policy)
    {
        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "policy", policy }
        };
        RateLimitViolations.Add(1, tags);
    }

    /// <summary>
    /// Records an upload initiation request.
    /// </summary>
    public static void RecordUploadInitiate(string status, double durationMs)
    {
        var tags = new TagList { { "status", status } };
        UploadInitiateTotal.Add(1, tags);
        UploadInitiateDuration.Record(durationMs);
    }

    /// <summary>
    /// Records an upload session creation.
    /// </summary>
    public static void RecordUploadSessionCreated()
    {
        UploadSessionCreatedTotal.Add(1);
    }

    /// <summary>
    /// Records a MinIO notification received.
    /// </summary>
    public static void RecordMinioNotificationReceived()
    {
        MinioNotificationReceivedTotal.Add(1);
    }

    /// <summary>
    /// Records a track creation.
    /// </summary>
    public static void RecordTrackCreated()
    {
        TrackCreatedTotal.Add(1);
    }

    /// <summary>
    /// Records an outbox message published.
    /// </summary>
    public static void RecordOutboxPublished(string eventType)
    {
        var tags = new TagList { { "event_type", eventType } };
        OutboxPublishedTotal.Add(1, tags);
    }

    /// <summary>
    /// Records an outbox message that failed permanently.
    /// </summary>
    public static void RecordOutboxFailed(string eventType)
    {
        var tags = new TagList { { "event_type", eventType } };
        OutboxFailedTotal.Add(1, tags);
    }

    /// <summary>
    /// Records an audio processing job started.
    /// </summary>
    public static void RecordAudioProcessingStarted()
    {
        AudioProcessingStartedTotal.Add(1);
    }

    /// <summary>
    /// Records an audio processing job completed successfully.
    /// </summary>
    public static void RecordAudioProcessingCompleted()
    {
        AudioProcessingCompletedTotal.Add(1);
    }

    /// <summary>
    /// Records an audio processing job failure.
    /// </summary>
    public static void RecordAudioProcessingFailed(string? reason = null)
    {
        var tags = new TagList();
        if (reason != null)
        {
            tags.Add("reason", reason);
        }
        AudioProcessingFailedTotal.Add(1, tags);
    }

    /// <summary>
    /// Records audio processing duration.
    /// </summary>
    public static void RecordAudioProcessingDuration(double durationMs)
    {
        AudioProcessingDuration.Record(durationMs);
    }
}
