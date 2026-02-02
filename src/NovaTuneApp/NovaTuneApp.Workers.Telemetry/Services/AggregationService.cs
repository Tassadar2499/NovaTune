using System.Diagnostics;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.ApiService.Models.Analytics;
using NovaTuneApp.ApiService.Models.Telemetry;
using NovaTuneApp.Workers.Telemetry.Configuration;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.Workers.Telemetry.Services;

/// <summary>
/// Implementation of aggregation service for telemetry events.
/// Updates hourly, daily, and user activity aggregates in RavenDB.
/// </summary>
public class AggregationService : IAggregationService
{
    private readonly IDocumentStore _documentStore;
    private readonly IOptions<TelemetryWorkerOptions> _options;
    private readonly ILogger<AggregationService> _logger;

    public AggregationService(
        IDocumentStore documentStore,
        IOptions<TelemetryWorkerOptions> options,
        ILogger<AggregationService> logger)
    {
        _documentStore = documentStore;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessEventAsync(TelemetryEvent evt, CancellationToken ct = default)
    {
        using var session = _documentStore.OpenAsyncSession();

        var hourBucket = TruncateToHour(evt.ServerTimestamp);
        var dayBucket = DateOnly.FromDateTime(evt.ServerTimestamp.UtcDateTime);

        // Update hourly aggregate
        await UpdateHourlyAggregateAsync(session, evt, hourBucket, ct);

        // Update daily aggregate (for dashboard queries)
        await UpdateDailyAggregateAsync(session, evt, dayBucket, ct);

        // Update user activity aggregate
        await UpdateUserActivityAsync(session, evt, dayBucket, ct);

        await session.SaveChangesAsync(ct);
    }

    private async Task UpdateHourlyAggregateAsync(
        IAsyncDocumentSession session,
        TelemetryEvent evt,
        DateTimeOffset hourBucket,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var docId = $"TrackHourlyAggregates/{evt.TrackId}/{hourBucket:yyyyMMddHH}";

        var aggregate = await session.LoadAsync<TrackHourlyAggregate>(docId, ct);

        if (aggregate is null)
        {
            aggregate = new TrackHourlyAggregate
            {
                Id = docId,
                TrackId = evt.TrackId,
                UserId = evt.UserId,
                HourBucket = hourBucket,
                Expires = hourBucket.AddDays(_options.Value.RetentionDays)
            };
            await session.StoreAsync(aggregate, ct);

            _logger.LogDebug(
                "Created new hourly aggregate: {DocId}",
                docId);
        }

        // Update counters based on event type
        switch (evt.EventType)
        {
            case PlaybackEventTypes.PlayStart:
                aggregate.PlayStartCount++;
                break;
            case PlaybackEventTypes.PlayComplete:
                aggregate.PlayCompleteCount++;
                break;
        }

        // Add duration played if available
        if (evt.DurationPlayedSeconds.HasValue)
        {
            aggregate.TotalSecondsPlayed += evt.DurationPlayedSeconds.Value;
        }

        // Track unique sessions (simplified; production may use HyperLogLog)
        if (!string.IsNullOrEmpty(evt.SessionId))
        {
            aggregate.UniqueSessionCount++;
        }

        aggregate.UpdatedAt = DateTimeOffset.UtcNow;

        stopwatch.Stop();
        NovaTuneMetrics.RecordTelemetryAggregation("hourly", stopwatch.ElapsedMilliseconds);
    }

    private async Task UpdateDailyAggregateAsync(
        IAsyncDocumentSession session,
        TelemetryEvent evt,
        DateOnly dayBucket,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var docId = $"TrackDailyAggregates/{evt.TrackId}/{dayBucket:yyyyMMdd}";

        var aggregate = await session.LoadAsync<TrackDailyAggregate>(docId, ct);

        if (aggregate is null)
        {
            aggregate = new TrackDailyAggregate
            {
                Id = docId,
                TrackId = evt.TrackId,
                UserId = evt.UserId,
                DateBucket = dayBucket,
                Expires = dayBucket.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    .AddDays(_options.Value.RetentionDays)
            };
            await session.StoreAsync(aggregate, ct);

            _logger.LogDebug(
                "Created new daily aggregate: {DocId}",
                docId);
        }

        // Update counters based on event type
        if (evt.EventType == PlaybackEventTypes.PlayStart)
            aggregate.TotalPlays++;

        if (evt.EventType == PlaybackEventTypes.PlayComplete)
            aggregate.CompletedPlays++;

        if (evt.DurationPlayedSeconds.HasValue)
            aggregate.TotalSecondsPlayed += evt.DurationPlayedSeconds.Value;

        aggregate.UpdatedAt = DateTimeOffset.UtcNow;

        stopwatch.Stop();
        NovaTuneMetrics.RecordTelemetryAggregation("daily", stopwatch.ElapsedMilliseconds);
    }

    private async Task UpdateUserActivityAsync(
        IAsyncDocumentSession session,
        TelemetryEvent evt,
        DateOnly dayBucket,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var docId = $"UserActivityAggregates/{evt.UserId}/{dayBucket:yyyyMMdd}";

        var aggregate = await session.LoadAsync<UserActivityAggregate>(docId, ct);

        if (aggregate is null)
        {
            aggregate = new UserActivityAggregate
            {
                Id = docId,
                UserId = evt.UserId,
                DateBucket = dayBucket,
                Expires = dayBucket.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    .AddDays(_options.Value.RetentionDays)
            };
            await session.StoreAsync(aggregate, ct);

            _logger.LogDebug(
                "Created new user activity aggregate: {DocId}",
                docId);
        }

        // Update counters based on event type
        if (evt.EventType == PlaybackEventTypes.PlayStart)
        {
            aggregate.TotalPlays++;
            // TracksPlayed would require deduplication - simplified here
        }

        if (evt.DurationPlayedSeconds.HasValue)
            aggregate.TotalSecondsPlayed += evt.DurationPlayedSeconds.Value;

        aggregate.LastActivityAt = evt.ServerTimestamp;
        aggregate.UpdatedAt = DateTimeOffset.UtcNow;

        stopwatch.Stop();
        NovaTuneMetrics.RecordTelemetryAggregation("user", stopwatch.ElapsedMilliseconds);
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset dt) =>
        new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Offset);
}
