using KafkaFlow;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Services;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles AudioUploadedEvent messages for track processing.
/// </summary>
public class AudioUploadedHandler : IMessageHandler<AudioUploadedEvent>
{
    private readonly ILogger<AudioUploadedHandler> _logger;
    private readonly ITrackService _trackService;

    public AudioUploadedHandler(ILogger<AudioUploadedHandler> logger, ITrackService trackService)
    {
        _logger = logger;
        _trackService = trackService;
    }

    public async Task Handle(IMessageContext context, AudioUploadedEvent message)
    {
        _logger.LogInformation(
            "Processing audio upload for track {TrackId}, correlation: {CorrelationId}",
            message.TrackId, message.CorrelationId);

        await _trackService.ProcessUploadedTrackAsync(message.TrackId);

        _logger.LogInformation("Completed processing for track {TrackId}", message.TrackId);
    }
}
