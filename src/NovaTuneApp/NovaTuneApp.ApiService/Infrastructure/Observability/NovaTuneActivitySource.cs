using System.Diagnostics;

namespace NovaTuneApp.ApiService.Infrastructure.Observability;

/// <summary>
/// Provides a centralized ActivitySource for distributed tracing of NovaTune operations.
/// Use this to create custom spans for business operations.
/// </summary>
public static class NovaTuneActivitySource
{
    /// <summary>
    /// The name of the activity source, used for OpenTelemetry registration.
    /// </summary>
    public const string Name = "NovaTune.Api";

    /// <summary>
    /// The version of the activity source.
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// The shared ActivitySource instance for creating activities (spans).
    /// </summary>
    public static readonly ActivitySource Source = new(Name, Version);

    /// <summary>
    /// Starts an activity for audio upload processing.
    /// </summary>
    public static Activity? StartAudioUpload(string trackId, string? format = null)
    {
        var activity = Source.StartActivity("ProcessAudioUpload", ActivityKind.Internal);
        activity?.SetTag("track.id", trackId);
        if (format is not null)
        {
            activity?.SetTag("track.format", format);
        }
        return activity;
    }

    /// <summary>
    /// Starts an activity for track deletion processing.
    /// </summary>
    public static Activity? StartTrackDeletion(string trackId)
    {
        var activity = Source.StartActivity("ProcessTrackDeletion", ActivityKind.Internal);
        activity?.SetTag("track.id", trackId);
        return activity;
    }

    // ============================================================================
    // Track Management Operations (Stage 5)
    // ============================================================================

    /// <summary>
    /// Starts an activity for listing tracks.
    /// </summary>
    public static Activity? StartTrackList(string userId, string? search = null)
    {
        var activity = Source.StartActivity("track.list", ActivityKind.Internal);
        activity?.SetTag("user.id", userId);
        if (search is not null)
        {
            activity?.SetTag("query.search", search);
        }
        return activity;
    }

    /// <summary>
    /// Starts an activity for getting a track.
    /// </summary>
    public static Activity? StartTrackGet(string trackId, string userId)
    {
        var activity = Source.StartActivity("track.get", ActivityKind.Internal);
        activity?.SetTag("track.id", trackId);
        activity?.SetTag("user.id", userId);
        return activity;
    }

    /// <summary>
    /// Starts an activity for updating a track.
    /// </summary>
    public static Activity? StartTrackUpdate(string trackId, string userId)
    {
        var activity = Source.StartActivity("track.update", ActivityKind.Internal);
        activity?.SetTag("track.id", trackId);
        activity?.SetTag("user.id", userId);
        return activity;
    }

    /// <summary>
    /// Starts an activity for soft-deleting a track.
    /// </summary>
    public static Activity? StartTrackSoftDelete(string trackId, string userId)
    {
        var activity = Source.StartActivity("track.delete", ActivityKind.Internal);
        activity?.SetTag("track.id", trackId);
        activity?.SetTag("user.id", userId);
        return activity;
    }

    /// <summary>
    /// Starts an activity for restoring a track.
    /// </summary>
    public static Activity? StartTrackRestore(string trackId, string userId)
    {
        var activity = Source.StartActivity("track.restore", ActivityKind.Internal);
        activity?.SetTag("track.id", trackId);
        activity?.SetTag("user.id", userId);
        return activity;
    }

    // ============================================================================
    // Track Management Child Spans
    // ============================================================================

    /// <summary>
    /// Starts an activity for loading a track from the database.
    /// </summary>
    public static Activity? StartDbLoadTrack(string trackId)
    {
        var activity = Source.StartActivity("db.load_track", ActivityKind.Client);
        activity?.SetTag("track.id", trackId);
        return activity;
    }

    /// <summary>
    /// Starts an activity for updating track status in the database.
    /// </summary>
    public static Activity? StartDbUpdateStatus(string trackId)
    {
        var activity = Source.StartActivity("db.update_status", ActivityKind.Client);
        activity?.SetTag("track.id", trackId);
        return activity;
    }

    /// <summary>
    /// Starts an activity for writing to the outbox.
    /// </summary>
    public static Activity? StartOutboxWrite(string messageType)
    {
        var activity = Source.StartActivity("outbox.write", ActivityKind.Client);
        activity?.SetTag("message.type", messageType);
        return activity;
    }

    /// <summary>
    /// Starts an activity for cache invalidation.
    /// </summary>
    public static Activity? StartCacheInvalidate(string trackId)
    {
        var activity = Source.StartActivity("cache.invalidate", ActivityKind.Client);
        activity?.SetTag("track.id", trackId);
        return activity;
    }

    /// <summary>
    /// Starts an activity for cache operations.
    /// </summary>
    public static Activity? StartCacheOperation(string operation, string key)
    {
        var activity = Source.StartActivity($"Cache.{operation}", ActivityKind.Client);
        activity?.SetTag("cache.operation", operation);
        activity?.SetTag("cache.key", key);
        return activity;
    }

    /// <summary>
    /// Starts an activity for storage operations.
    /// </summary>
    public static Activity? StartStorageOperation(string operation, string? bucket = null)
    {
        var activity = Source.StartActivity($"Storage.{operation}", ActivityKind.Client);
        activity?.SetTag("storage.operation", operation);
        if (bucket is not null)
        {
            activity?.SetTag("storage.bucket", bucket);
        }
        return activity;
    }
}
