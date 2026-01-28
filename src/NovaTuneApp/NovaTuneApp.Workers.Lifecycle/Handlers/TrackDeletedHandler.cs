using KafkaFlow;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.Workers.Lifecycle.Handlers;

/// <summary>
/// Handles TrackDeletedEvent messages from the track-deletions topic.
/// This handler receives events for logging/monitoring purposes.
/// Physical deletion is handled by PhysicalDeletionService via polling.
/// </summary>
public class TrackDeletedHandler : IMessageHandler<TrackDeletedEvent>
{
    private readonly ILogger<TrackDeletedHandler> _logger;

    public TrackDeletedHandler(ILogger<TrackDeletedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(IMessageContext context, TrackDeletedEvent message)
    {
        _logger.LogInformation(
            "Received TrackDeletedEvent: TrackId={TrackId}, UserId={UserId}, ScheduledDeletionAt={ScheduledAt}, CorrelationId={CorrelationId}",
            message.TrackId,
            message.UserId,
            message.ScheduledDeletionAt,
            message.CorrelationId);

        // Event-driven handling for immediate actions (logging, metrics, etc.)
        // Physical deletion is handled by PhysicalDeletionService via polling
        // which queries for tracks past their ScheduledDeletionAt time

        return Task.CompletedTask;
    }
}
