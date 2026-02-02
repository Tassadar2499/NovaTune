using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Services;
using NovaTuneApp.Workers.Lifecycle.Configuration;
using Raven.Client.Documents;

namespace NovaTuneApp.Workers.Lifecycle.Services;

/// <summary>
/// Background service that polls RavenDB for tracks past their scheduled deletion time
/// and performs physical deletion of storage objects and database documents.
/// </summary>
public class PhysicalDeletionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<LifecycleOptions> _options;
    private readonly ILogger<PhysicalDeletionService> _logger;

    public PhysicalDeletionService(
        IServiceProvider serviceProvider,
        IOptions<LifecycleOptions> options,
        ILogger<PhysicalDeletionService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    private static readonly TimeSpan HealthCheckPauseInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "PhysicalDeletionService started, polling every {Interval}",
            _options.Value.PollingInterval);

        // Initial delay to allow other services to start
        await Task.Delay(TimeSpan.FromSeconds(10), ct);

        while (!ct.IsCancellationRequested)
        {
            // Check RavenDB health before processing (fail-tolerant: pause if unhealthy)
            if (!await IsRavenDbHealthyAsync(ct))
            {
                _logger.LogWarning(
                    "RavenDB unhealthy, pausing deletion processing for {PauseInterval}",
                    HealthCheckPauseInterval);
                await Task.Delay(HealthCheckPauseInterval, ct);
                continue;
            }

            try
            {
                var deletedCount = await ProcessDeletionsAsync(ct);
                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Processed {Count} physical deletions",
                        deletedCount);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing physical deletions");
            }

            await Task.Delay(_options.Value.PollingInterval, ct);
        }

        _logger.LogInformation("PhysicalDeletionService stopped");
    }

    private async Task<bool> IsRavenDbHealthyAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
            using var session = store.OpenAsyncSession();

            // Simple health check: try to query the database
            await session.Query<Track>().Take(1).ToListAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RavenDB health check failed");
            return false;
        }
    }

    private async Task<int> ProcessDeletionsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var metrics = scope.ServiceProvider.GetService<PhysicalDeletionMetrics>();

        using var session = store.OpenAsyncSession();

        var tracksToDelete = await session
            .Query<Track, Tracks_ByScheduledDeletion>()
            .Where(t => t.Status == TrackStatus.Deleted
                     && t.ScheduledDeletionAt <= DateTimeOffset.UtcNow)
            .Take(_options.Value.BatchSize)
            .ToListAsync(ct);

        if (tracksToDelete.Count == 0)
        {
            return 0;
        }

        _logger.LogDebug(
            "Found {Count} tracks ready for physical deletion",
            tracksToDelete.Count);

        var deletedCount = 0;

        foreach (var track in tracksToDelete)
        {
            try
            {
                await DeleteTrackPhysicallyAsync(session, storageService, track, metrics, ct);
                deletedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to physically delete track {TrackId}",
                    track.TrackId);
                metrics?.RecordFailure();
                // Continue with next track; will retry on next poll
            }
        }

        return deletedCount;
    }

    private async Task DeleteTrackPhysicallyAsync(
        Raven.Client.Documents.Session.IAsyncDocumentSession session,
        IStorageService storageService,
        Track track,
        PhysicalDeletionMetrics? metrics,
        CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Starting physical deletion of track {TrackId} for user {UserId}",
            track.TrackId, track.UserId);

        // 1. Delete MinIO audio object
        try
        {
            await storageService.DeleteObjectAsync(track.ObjectKey, ct);
            _logger.LogDebug("Deleted audio object {ObjectKey}", track.ObjectKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete audio object {ObjectKey}, continuing with deletion",
                track.ObjectKey);
            // Continue - object might already be deleted or bucket cleaned up
        }

        // 2. Delete MinIO waveform object (if exists)
        if (track.WaveformObjectKey is not null)
        {
            try
            {
                await storageService.DeleteObjectAsync(track.WaveformObjectKey, ct);
                _logger.LogDebug("Deleted waveform object {ObjectKey}", track.WaveformObjectKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete waveform object {ObjectKey}, continuing with deletion",
                    track.WaveformObjectKey);
            }
        }

        // 3. Remove track references from playlists (Stage 6)
        try
        {
            using var playlistScope = _serviceProvider.CreateScope();
            var playlistService = playlistScope.ServiceProvider.GetRequiredService<IPlaylistService>();
            await playlistService.RemoveDeletedTrackReferencesAsync(track.TrackId, track.UserId, ct);
            _logger.LogDebug("Removed track {TrackId} references from playlists", track.TrackId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to remove track {TrackId} from playlists, continuing with deletion",
                track.TrackId);
            // Continue - playlist cleanup is best effort; track will be gone anyway
        }

        // 4. Delete RavenDB document
        session.Delete(track);
        await session.SaveChangesAsync(ct);

        var duration = DateTimeOffset.UtcNow - startTime;

        _logger.LogInformation(
            "Physically deleted track {TrackId} for user {UserId}, freed {Bytes} bytes in {Duration}ms",
            track.TrackId, track.UserId, track.FileSizeBytes, duration.TotalMilliseconds);

        // 5. Record metrics
        metrics?.RecordDeletion(track.FileSizeBytes, duration.TotalMilliseconds);
    }
}
