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
