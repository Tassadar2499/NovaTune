using KafkaFlow;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.Workers.AudioProcessor.Services;

namespace NovaTuneApp.Workers.AudioProcessor.Handlers;

/// <summary>
/// KafkaFlow handler for AudioUploadedEvent messages.
/// Implements Req 3.2 - Consume AudioUploadedEvent and invoke processing logic.
/// </summary>
public class AudioUploadedHandler : IMessageHandler<AudioUploadedEvent>
{
    private readonly IAudioProcessorService _processorService;
    private readonly ILogger<AudioUploadedHandler> _logger;

    public AudioUploadedHandler(
        IAudioProcessorService processorService,
        ILogger<AudioUploadedHandler> logger)
    {
        _processorService = processorService;
        _logger = logger;
    }

    public async Task Handle(IMessageContext context, AudioUploadedEvent message)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["TrackId"] = message.TrackId,
            ["CorrelationId"] = message.CorrelationId,
            ["UserId"] = message.UserId
        });

        _logger.LogInformation(
            "Received AudioUploadedEvent for TrackId={TrackId}, ObjectKey={ObjectKey}",
            message.TrackId,
            message.ObjectKey);

        // Record metric (NF-4.2)
        NovaTuneMetrics.RecordAudioProcessingStarted();

        try
        {
            // The service handles:
            // - Orphan event detection (track not found)
            // - Idempotency (track already in terminal state)
            // - Full processing pipeline
            // - Track status updates
            var success = await _processorService.ProcessAsync(
                message,
                context.ConsumerContext.WorkerStopped);

            if (success)
            {
                NovaTuneMetrics.RecordAudioProcessingCompleted();
                _logger.LogInformation(
                    "Successfully processed audio for TrackId={TrackId}",
                    message.TrackId);
            }
            else
            {
                NovaTuneMetrics.RecordAudioProcessingFailed();
                _logger.LogWarning(
                    "Processing returned false for TrackId={TrackId}, will retry",
                    message.TrackId);

                // Throw to trigger retry mechanism
                throw new InvalidOperationException($"Processing failed for track {message.TrackId}");
            }
        }
        catch (Exception ex)
        {
            NovaTuneMetrics.RecordAudioProcessingFailed();
            _logger.LogError(ex, "Failed to process audio for TrackId={TrackId}", message.TrackId);

            // Re-throw to trigger KafkaFlow retry/DLQ
            throw;
        }
    }
}
