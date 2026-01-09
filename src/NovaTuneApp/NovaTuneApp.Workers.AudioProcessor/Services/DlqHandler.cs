using System.Text;
using System.Text.Json;
using KafkaFlow;
using KafkaFlow.Producers;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Implementation of DLQ handler that publishes failed messages to the DLQ topic.
/// Per 06-error-handling.md.
/// </summary>
public class DlqHandler : IDlqHandler
{
    private readonly IProducerAccessor _producerAccessor;
    private readonly ILogger<DlqHandler> _logger;
    private const string DlqProducerName = "dlq-producer";

    public DlqHandler(
        IProducerAccessor producerAccessor,
        ILogger<DlqHandler> logger)
    {
        _producerAccessor = producerAccessor;
        _logger = logger;
    }

    public async Task PublishAsync(IMessageContext context, Exception exception, int retryCount)
    {
        try
        {
            var producer = _producerAccessor.GetProducer(DlqProducerName);

            // Serialize original message payload
            var originalPayload = context.Message.Value is not null
                ? JsonSerializer.Serialize(context.Message.Value)
                : string.Empty;

            var dlqMessage = new DlqMessage
            {
                OriginalTopic = context.ConsumerContext.Topic,
                OriginalKey = context.Message.Key is byte[] keyBytes
                    ? Encoding.UTF8.GetString(keyBytes)
                    : context.Message.Key?.ToString() ?? string.Empty,
                OriginalPayload = originalPayload,
                ErrorMessage = exception.Message,
                ErrorStackTrace = exception.StackTrace ?? string.Empty,
                RetryCount = retryCount,
                FailedAt = DateTimeOffset.UtcNow
            };

            await producer.ProduceAsync(
                dlqMessage.OriginalKey,
                dlqMessage);

            _logger.LogWarning(
                "Published message to DLQ after {RetryCount} retries. Topic={OriginalTopic}, Key={Key}, Error={Error}",
                retryCount,
                dlqMessage.OriginalTopic,
                dlqMessage.OriginalKey,
                exception.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish message to DLQ. Original error: {OriginalError}",
                exception.Message);
        }
    }
}
