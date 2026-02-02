using System.Diagnostics;
using KafkaFlow;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.Workers.Telemetry.Services;

namespace NovaTuneApp.Workers.Telemetry.Handlers;

/// <summary>
/// Handles TelemetryEvent messages from the telemetry-events topic.
/// Processes events and updates analytics aggregates.
/// </summary>
public class TelemetryEventHandler : IMessageHandler<TelemetryEvent>
{
    private readonly IAggregationService _aggregationService;
    private readonly ILogger<TelemetryEventHandler> _logger;

    public TelemetryEventHandler(
        IAggregationService aggregationService,
        ILogger<TelemetryEventHandler> logger)
    {
        _aggregationService = aggregationService;
        _logger = logger;
    }

    public async Task Handle(IMessageContext context, TelemetryEvent message)
    {
        var stopwatch = Stopwatch.StartNew();

        using var activity = Activity.Current?.Source?.StartActivity(
            "telemetry.aggregate",
            ActivityKind.Consumer);

        activity?.SetTag("event.type", message.EventType);
        activity?.SetTag("track.id", message.TrackId);
        activity?.SetTag("correlation.id", message.CorrelationId);

        try
        {
            _logger.LogDebug(
                "Processing telemetry event: Type={EventType}, TrackId={TrackId}, CorrelationId={CorrelationId}",
                message.EventType,
                message.TrackId,
                message.CorrelationId);

            await _aggregationService.ProcessEventAsync(message, context.ConsumerContext.WorkerStopped);

            stopwatch.Stop();
            NovaTuneMetrics.RecordTelemetryWorkerEvent(message.EventType, true, stopwatch.ElapsedMilliseconds);

            _logger.LogDebug(
                "Telemetry event processed in {Duration}ms: Type={EventType}, TrackId={TrackId}",
                stopwatch.ElapsedMilliseconds,
                message.EventType,
                message.TrackId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            NovaTuneMetrics.RecordTelemetryWorkerEvent(message.EventType, false, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to process telemetry event: Type={EventType}, TrackId={TrackId}, CorrelationId={CorrelationId}",
                message.EventType,
                message.TrackId,
                message.CorrelationId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Let KafkaFlow handle retries
            throw;
        }
    }
}
