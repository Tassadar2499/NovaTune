using NovaTuneApp.ApiService.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Lightweight track access validator implementation.
/// Uses optimized RavenDB queries for fast validation.
/// </summary>
public class TrackAccessValidator : ITrackAccessValidator
{
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<TrackAccessValidator> _logger;

    public TrackAccessValidator(
        IDocumentStore documentStore,
        ILogger<TrackAccessValidator> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        try
        {
            using var session = _documentStore.OpenAsyncSession();

            // Use Include to avoid extra round-trips if needed later
            var track = await session.LoadAsync<Track>($"Tracks/{trackId}", ct);

            if (track is null)
            {
                _logger.LogDebug(
                    "Track access check: Track {TrackId} not found",
                    trackId);
                return false;
            }

            // Track must be in Ready status to allow playback telemetry
            if (track.Status != TrackStatus.Ready)
            {
                _logger.LogDebug(
                    "Track access check: Track {TrackId} status is {Status}, not Ready",
                    trackId,
                    track.Status);
                return false;
            }

            // Track must not be deleted
            if (track.DeletedAt.HasValue)
            {
                _logger.LogDebug(
                    "Track access check: Track {TrackId} is deleted",
                    trackId);
                return false;
            }

            // For now, only track owner can report telemetry
            // This can be expanded for public tracks or shared access
            if (track.UserId != userId)
            {
                _logger.LogDebug(
                    "Track access check: User {UserId} does not own track {TrackId}",
                    userId,
                    trackId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking track access for track {TrackId} and user {UserId}",
                trackId,
                userId);

            // Fail open for telemetry - log warning but don't block
            _logger.LogWarning(
                "Track access check failed, allowing access for telemetry: Track {TrackId}, User {UserId}",
                trackId,
                userId);
            return true;
        }
    }
}
