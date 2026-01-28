using KafkaFlow;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles TrackDeletedEvent messages for immediate cleanup operations.
/// Physical deletion is handled by the lifecycle worker after the grace period.
/// </summary>
public class TrackDeletedHandler : IMessageHandler<TrackDeletedEvent>
{
    private readonly ILogger<TrackDeletedHandler> _logger;
    private readonly ICacheService _cacheService;

    public TrackDeletedHandler(
        ILogger<TrackDeletedHandler> logger,
        ICacheService cacheService)
    {
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task Handle(IMessageContext context, TrackDeletedEvent message)
    {
        _logger.LogInformation(
            "Processing track deletion event: TrackId={TrackId}, ScheduledDeletionAt={ScheduledDeletionAt}",
            message.TrackId,
            message.ScheduledDeletionAt);

        // Invalidate presigned URL cache (best-effort, may have already been invalidated)
        try
        {
            await _cacheService.RemoveAsync($"presigned:{message.UserId}:{message.TrackId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to invalidate cache for track {TrackId}, will expire naturally",
                message.TrackId);
        }

        // Note: Physical deletion of storage objects is handled by the lifecycle worker
        // which polls for tracks past their ScheduledDeletionAt time

        _logger.LogDebug(
            "Track deletion event processed: TrackId={TrackId}, ObjectKey={ObjectKey}",
            message.TrackId,
            message.ObjectKey);
    }
}
