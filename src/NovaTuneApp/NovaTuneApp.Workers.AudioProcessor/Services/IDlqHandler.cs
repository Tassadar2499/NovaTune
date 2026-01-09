using KafkaFlow;

namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Handles publishing failed messages to the Dead Letter Queue.
/// </summary>
public interface IDlqHandler
{
    /// <summary>
    /// Publishes a failed message to the DLQ topic.
    /// </summary>
    Task PublishAsync(IMessageContext context, Exception exception, int retryCount);
}
