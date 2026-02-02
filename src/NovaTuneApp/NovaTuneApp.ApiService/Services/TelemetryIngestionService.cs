using System.Diagnostics;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Messaging;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.ApiService.Models.Telemetry;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Implementation of telemetry ingestion service (Req 5.4).
/// </summary>
public class TelemetryIngestionService : ITelemetryIngestionService
{
    private readonly IMessageProducerService _messageProducer;
    private readonly ITrackAccessValidator _trackAccessValidator;
    private readonly IOptions<TelemetryOptions> _options;
    private readonly ILogger<TelemetryIngestionService> _logger;
    private readonly Random _random = new();

    public TelemetryIngestionService(
        IMessageProducerService messageProducer,
        ITrackAccessValidator trackAccessValidator,
        IOptions<TelemetryOptions> options,
        ILogger<TelemetryIngestionService> logger)
    {
        _messageProducer = messageProducer;
        _trackAccessValidator = trackAccessValidator;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TelemetryIngestionResult> IngestAsync(
        PlaybackEventRequest request,
        string userId,
        string correlationId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Validate timestamp bounds
        var maxPast = now.AddHours(-_options.Value.MaxEventAgeHours);
        var maxFuture = now.AddMinutes(_options.Value.MaxFutureMinutes);

        if (request.ClientTimestamp < maxPast || request.ClientTimestamp > maxFuture)
        {
            NovaTuneMetrics.RecordTelemetryEvent(request.EventType, "rejected", "invalid_timestamp");
            _logger.LogWarning(
                "Telemetry rejected: timestamp {ClientTimestamp} outside valid range [{MaxPast}, {MaxFuture}]",
                request.ClientTimestamp,
                maxPast,
                maxFuture);
            return TelemetryIngestionResult.Rejected("invalid_timestamp");
        }

        // Validate track access (lightweight check)
        if (!await _trackAccessValidator.HasAccessAsync(request.TrackId, userId, ct))
        {
            NovaTuneMetrics.RecordTelemetryEvent(request.EventType, "rejected", "access_denied");
            _logger.LogWarning(
                "Telemetry rejected: user {UserId} lacks access to track {TrackId}",
                userId,
                request.TrackId);
            return TelemetryIngestionResult.AccessDenied();
        }

        // Apply server-side sampling for play_progress under load
        if (_options.Value.EnableSampling &&
            request.EventType == PlaybackEventTypes.PlayProgress &&
            ShouldSample())
        {
            NovaTuneMetrics.RecordTelemetryEvent(request.EventType, "sampled", null);
            _logger.LogDebug(
                "Telemetry sampled: play_progress event for track {TrackId}",
                request.TrackId);
            return TelemetryIngestionResult.Sampled();
        }

        // Create telemetry event
        var evt = new TelemetryEvent
        {
            EventType = request.EventType,
            TrackId = request.TrackId,
            UserId = userId,
            ClientTimestamp = request.ClientTimestamp,
            ServerTimestamp = now,
            PositionSeconds = request.PositionSeconds,
            DurationPlayedSeconds = request.DurationPlayedSeconds,
            SessionId = request.SessionId,
            DeviceId = request.DeviceId,
            ClientVersion = request.ClientVersion,
            CorrelationId = correlationId
        };

        try
        {
            await _messageProducer.PublishTelemetryEventAsync(evt, ct);

            NovaTuneMetrics.RecordTelemetryEvent(request.EventType, "accepted", null);
            _logger.LogDebug(
                "Telemetry event {EventType} ingested for track {TrackId}, CorrelationId: {CorrelationId}",
                request.EventType,
                request.TrackId,
                correlationId);

            return TelemetryIngestionResult.Success(correlationId);
        }
        catch (Exception ex)
        {
            NovaTuneMetrics.RecordTelemetryEvent(request.EventType, "error", "publish_failed");
            _logger.LogError(
                ex,
                "Failed to publish telemetry event for track {TrackId}",
                request.TrackId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TelemetryBatchResult> IngestBatchAsync(
        IReadOnlyList<PlaybackEventRequest> events,
        string userId,
        string correlationId,
        CancellationToken ct = default)
    {
        var accepted = 0;
        var rejected = 0;
        var now = DateTimeOffset.UtcNow;
        var maxPastBatch = now.AddDays(-_options.Value.MaxBatchEventAgeDays);

        foreach (var request in events)
        {
            // Check batch event age limit
            if (request.ClientTimestamp < maxPastBatch)
            {
                rejected++;
                _logger.LogDebug(
                    "Batch event rejected: timestamp {ClientTimestamp} older than {MaxAge} days",
                    request.ClientTimestamp,
                    _options.Value.MaxBatchEventAgeDays);
                continue;
            }

            var result = await IngestAsync(request, userId, correlationId, ct);

            if (result.Accepted)
            {
                accepted++;
            }
            else
            {
                rejected++;
            }
        }

        NovaTuneMetrics.RecordTelemetryBatch(accepted, rejected);
        _logger.LogInformation(
            "Batch telemetry processed: {Accepted} accepted, {Rejected} rejected, CorrelationId: {CorrelationId}",
            accepted,
            rejected,
            correlationId);

        return new TelemetryBatchResult(accepted, rejected, correlationId);
    }

    /// <summary>
    /// Determines if the event should be sampled (dropped) based on sampling rate.
    /// </summary>
    private bool ShouldSample()
    {
        var samplingRate = _options.Value.ProgressEventSamplingRate;

        // If sampling rate is 1.0 or higher, never sample (keep all)
        if (samplingRate >= 1.0)
            return false;

        // If sampling rate is 0.0 or lower, always sample (drop all)
        if (samplingRate <= 0.0)
            return true;

        // Random sampling based on rate
        return _random.NextDouble() > samplingRate;
    }
}
