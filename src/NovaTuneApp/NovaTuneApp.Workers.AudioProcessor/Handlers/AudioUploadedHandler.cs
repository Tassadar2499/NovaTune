using KafkaFlow;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.Workers.AudioProcessor.Services;
using Raven.Client.Documents;

namespace NovaTuneApp.Workers.AudioProcessor.Handlers;

/// <summary>
/// KafkaFlow handler for AudioUploadedEvent messages.
/// Implements Req 3.2 - Consume AudioUploadedEvent and invoke processing logic.
/// </summary>
public class AudioUploadedHandler : IMessageHandler<AudioUploadedEvent>
{
    private readonly IAudioProcessorService _processorService;
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<AudioUploadedHandler> _logger;
    private readonly AudioProcessorOptions _options;

    public AudioUploadedHandler(
        IAudioProcessorService processorService,
        IDocumentStore documentStore,
        ILogger<AudioUploadedHandler> logger,
        IOptions<AudioProcessorOptions> options)
    {
        _processorService = processorService;
        _documentStore = documentStore;
        _logger = logger;
        _options = options.Value;
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

        // Idempotency check (Req 3.5): Skip if track is already in terminal state
        if (await ShouldSkipProcessing(message.TrackId))
        {
            _logger.LogInformation(
                "Skipping processing for TrackId={TrackId} - already in terminal state",
                message.TrackId);
            return;
        }

        try
        {
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

    /// <summary>
    /// Check if track is in a terminal state (Ready, Failed, Deleted) to implement idempotency (Req 3.5).
    /// </summary>
    private async Task<bool> ShouldSkipProcessing(string trackId)
    {
        try
        {
            using var session = _documentStore.OpenAsyncSession();
            var track = await session.LoadAsync<dynamic>($"Tracks/{trackId}");

            if (track is null)
            {
                _logger.LogWarning("Track {TrackId} not found in database", trackId);
                return true; // Skip if track doesn't exist
            }

            var status = (string?)track.Status;

            // Terminal states that should skip processing
            var terminalStates = new[] { "Ready", "Failed", "Deleted" };
            return terminalStates.Contains(status, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check track status for {TrackId}, will proceed with processing", trackId);
            return false; // Proceed with processing if check fails
        }
    }
}
