using System.Diagnostics;
using KafkaFlow;
using NovaTuneApp.ApiService.Infrastructure.Observability;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging;

/// <summary>
/// KafkaFlow middleware that adds OpenTelemetry tracing to message processing.
/// Creates spans for both message production and consumption.
/// </summary>
public class TracingMiddleware : IMessageMiddleware
{
    private readonly ILogger<TracingMiddleware> _logger;

    public TracingMiddleware(ILogger<TracingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        var isConsumer = context.ConsumerContext is not null;
        var operationType = isConsumer ? "consume" : "produce";
        var topic = isConsumer
            ? context.ConsumerContext?.Topic
            : context.ProducerContext?.Topic;

        using var activity = NovaTuneActivitySource.Source.StartActivity(
            $"kafka.{operationType}",
            isConsumer ? ActivityKind.Consumer : ActivityKind.Producer);

        if (activity is not null)
        {
            // Standard OpenTelemetry messaging semantic conventions
            activity.SetTag("messaging.system", "kafka");
            activity.SetTag("messaging.operation", operationType);

            if (topic is not null)
            {
                activity.SetTag("messaging.destination.name", topic);
            }

            if (isConsumer && context.ConsumerContext is not null)
            {
                activity.SetTag("messaging.kafka.consumer.group", context.ConsumerContext.GroupId);
                activity.SetTag("messaging.kafka.partition", context.ConsumerContext.Partition);
                activity.SetTag("messaging.kafka.offset", context.ConsumerContext.Offset);
            }

            // Add message type if available
            var messageType = context.Message.Value?.GetType().Name;
            if (messageType is not null)
            {
                activity.SetTag("messaging.message.type", messageType);
            }
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);

            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Record metrics
            if (isConsumer)
            {
                NovaTuneMetrics.MessagesConsumed.Add(1,
                    new("topic", topic ?? "unknown"),
                    new("consumer.group", context.ConsumerContext?.GroupId ?? "unknown"),
                    new("message.type", context.Message.Value?.GetType().Name ?? "unknown"));

                NovaTuneMetrics.MessageProcessingDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                    new("topic", topic ?? "unknown"),
                    new("operation", "consume"));
            }
            else
            {
                NovaTuneMetrics.MessagesProduced.Add(1,
                    new("topic", topic ?? "unknown"),
                    new("message.type", context.Message.Value?.GetType().Name ?? "unknown"));
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", ex.GetType().FullName },
                { "exception.message", ex.Message },
                { "exception.stacktrace", ex.StackTrace }
            }));

            _logger.LogError(ex,
                "Error processing Kafka message. Topic: {Topic}, Operation: {Operation}",
                topic,
                operationType);

            throw;
        }
    }
}
