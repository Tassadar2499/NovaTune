using KafkaFlow;
using NovaTuneApp.Workers.AudioProcessor.Services;

namespace NovaTuneApp.Workers.AudioProcessor.Middleware;

/// <summary>
/// Middleware that catches exceptions after retry exhaustion and publishes to DLQ.
/// Per 06-error-handling.md.
/// </summary>
public class DlqMiddleware : IMessageMiddleware
{
    private readonly IDlqHandler _dlqHandler;
    private readonly ILogger<DlqMiddleware> _logger;

    // Maximum retries configured in RetrySimple middleware
    private const int MaxRetries = 3;

    public DlqMiddleware(
        IDlqHandler dlqHandler,
        ILogger<DlqMiddleware> logger)
    {
        _dlqHandler = dlqHandler;
        _logger = logger;
    }

    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // If we get here, retries have been exhausted
            // Publish to DLQ and swallow exception to prevent consumer from crashing
            _logger.LogError(
                ex,
                "Message processing failed after all retries. Publishing to DLQ. Topic={Topic}",
                context.ConsumerContext.Topic);

            await _dlqHandler.PublishAsync(context, ex, MaxRetries);

            // Don't rethrow - message has been moved to DLQ
        }
    }
}
