using KafkaFlow;
using KafkaFlow.Producers;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.ApiService.Models.Outbox;
using Raven.Client.Documents;

namespace NovaTuneApp.ApiService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that polls for pending outbox messages and publishes them to Kafka/Redpanda.
/// Implements the outbox pattern (NF-5.2) for reliable event publication.
/// </summary>
public class OutboxProcessorService : BackgroundService
{
    private readonly IDocumentStore _documentStore;
    private readonly IProducerAccessor _producerAccessor;
    private readonly IOptions<NovaTuneOptions> _options;
    private readonly ILogger<OutboxProcessorService> _logger;

    public OutboxProcessorService(
        IDocumentStore documentStore,
        IProducerAccessor producerAccessor,
        IOptions<NovaTuneOptions> options,
        ILogger<OutboxProcessorService> logger)
    {
        _documentStore = documentStore;
        _producerAccessor = producerAccessor;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processorOptions = _options.Value.OutboxProcessor;

        _logger.LogInformation(
            "Outbox processor service starting. Polling interval: {Interval}ms, Batch size: {BatchSize}, Max retries: {MaxRetries}",
            processorOptions.PollingIntervalMs,
            processorOptions.BatchSize,
            processorOptions.MaxRetries);

        // Initial delay to let the application start and Kafka to become available
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during outbox processing");
            }

            await Task.Delay(processorOptions.PollingInterval, stoppingToken);
        }

        _logger.LogInformation("Outbox processor service stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        var processorOptions = _options.Value.OutboxProcessor;

        using var session = _documentStore.OpenAsyncSession();

        var pendingMessages = await session
            .Query<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(processorOptions.BatchSize)
            .ToListAsync(ct);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {Count} pending outbox messages", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await PublishMessageAsync(message, ct);

                message.Status = OutboxMessageStatus.Published;
                message.PublishedAt = DateTimeOffset.UtcNow;

                // Record metric (NF-4.4)
                NovaTuneMetrics.RecordOutboxPublished(message.MessageType);

                // Outbox published log (NF-4.x) - Debug level
                _logger.LogDebug(
                    "Outbox published: EventType={EventType}, TrackId={TrackId}",
                    message.MessageType,
                    message.PartitionKey);
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.ErrorMessage = ex.Message;

                if (message.Attempts >= processorOptions.MaxRetries)
                {
                    message.Status = OutboxMessageStatus.Failed;

                    // Record metric (NF-4.4)
                    NovaTuneMetrics.RecordOutboxFailed(message.MessageType);

                    _logger.LogError(
                        ex,
                        "Outbox message {MessageId} failed permanently after {Attempts} attempts. MessageType: {MessageType}, Topic: {Topic}",
                        message.Id,
                        message.Attempts,
                        message.MessageType,
                        message.Topic);
                }
                else
                {
                    var backoffDelay = CalculateBackoffDelay(message.Attempts, processorOptions);

                    _logger.LogWarning(
                        ex,
                        "Failed to publish outbox message {MessageId}. Attempt {Attempts}/{MaxRetries}. Next retry in {BackoffMs}ms",
                        message.Id,
                        message.Attempts,
                        processorOptions.MaxRetries,
                        backoffDelay.TotalMilliseconds);
                }
            }
        }

        await session.SaveChangesAsync(ct);
    }

    private async Task PublishMessageAsync(OutboxMessage message, CancellationToken ct)
    {
        // Get producer based on topic - use a generic approach since we store raw JSON
        var producer = GetProducerForTopic(message.Topic);

        var headers = new MessageHeaders();
        if (!string.IsNullOrEmpty(message.CorrelationId))
        {
            headers.Add("correlation-id", System.Text.Encoding.UTF8.GetBytes(message.CorrelationId));
        }
        headers.Add("message-type", System.Text.Encoding.UTF8.GetBytes(message.MessageType));

        // Produce raw JSON payload - the consumer will deserialize
        await producer.ProduceAsync(
            topic: message.Topic,
            messageKey: message.PartitionKey,
            messageValue: message.Payload,
            headers: headers
        );
    }

    private IMessageProducer GetProducerForTopic(string topic)
    {
        // Use the appropriate producer based on topic naming convention
        // Audio events use audio-producer, deletions use deletion-producer
        if (topic.Contains("audio-events", StringComparison.OrdinalIgnoreCase))
        {
            return _producerAccessor.GetProducer("audio-producer");
        }

        if (topic.Contains("track-deletions", StringComparison.OrdinalIgnoreCase))
        {
            return _producerAccessor.GetProducer("deletion-producer");
        }

        // Default to audio-producer for other topics
        _logger.LogWarning("No specific producer found for topic {Topic}, using audio-producer", topic);
        return _producerAccessor.GetProducer("audio-producer");
    }

    private static TimeSpan CalculateBackoffDelay(int attempt, OutboxProcessorOptions options)
    {
        // Exponential backoff with jitter: delay = min(maxBackoff, initialBackoff * 2^attempt) + random jitter
        var exponentialDelay = options.InitialBackoffMs * Math.Pow(2, attempt - 1);
        var cappedDelay = Math.Min(exponentialDelay, options.MaxBackoffMs);

        // Add up to 10% jitter
        var jitter = Random.Shared.NextDouble() * 0.1 * cappedDelay;

        return TimeSpan.FromMilliseconds(cappedDelay + jitter);
    }
}
