using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models.Telemetry;
using NovaTuneApp.ApiService.Services;

namespace NovaTuneApp.ApiService.Endpoints;

/// <summary>
/// Telemetry endpoints for playback event ingestion (Req 5.4).
/// </summary>
public static class TelemetryEndpoints
{
    public static void MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/telemetry")
            .RequireAuthorization(PolicyNames.ActiveUser)
            .WithTags("Telemetry")
            .WithOpenApi();

        group.MapPost("/playback", HandleIngestPlayback)
            .WithName("IngestPlaybackEvent")
            .WithSummary("Report playback telemetry event")
            .WithDescription("Reports a single playback telemetry event (play_start, play_stop, play_progress, play_complete, seek)")
            .Produces<TelemetryAcceptedResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting("telemetry-ingest");

        group.MapPost("/playback/batch", HandleIngestBatch)
            .WithName("IngestPlaybackEventBatch")
            .WithSummary("Report multiple playback telemetry events")
            .WithDescription("Reports a batch of playback telemetry events (for offline-buffered events)")
            .Produces<TelemetryBatchResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting("telemetry-ingest-batch");
    }

    private static async Task<IResult> HandleIngestPlayback(
        [FromBody] PlaybackEventRequest request,
        [FromServices] ITelemetryIngestionService telemetryService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // Validate event type
        if (!PlaybackEventTypes.Valid.Contains(request.EventType))
        {
            return Results.Problem(
                title: "Invalid event type",
                detail: $"Event type must be one of: {string.Join(", ", PlaybackEventTypes.Valid)}",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-event-type");
        }

        // Validate track ID format
        if (!Ulid.TryParse(request.TrackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        // Validate position
        if (request.PositionSeconds.HasValue && request.PositionSeconds.Value < 0)
        {
            return Results.Problem(
                title: "Invalid position",
                detail: "Position must be a non-negative number.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        // Validate duration played
        if (request.DurationPlayedSeconds.HasValue && request.DurationPlayedSeconds.Value < 0)
        {
            return Results.Problem(
                title: "Invalid duration",
                detail: "Duration played must be a non-negative number.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return TypedResults.Unauthorized();

        var correlationId = Activity.Current?.Id ?? Ulid.NewUlid().ToString();

        try
        {
            var result = await telemetryService.IngestAsync(request, userId, correlationId, ct);

            if (!result.Accepted && result.RejectionReason == "access_denied")
            {
                return Results.Problem(
                    title: "Track not accessible",
                    detail: "You do not have access to report telemetry for this track.",
                    statusCode: StatusCodes.Status403Forbidden,
                    type: "https://novatune.dev/errors/track-access-denied");
            }

            if (!result.Accepted && result.RejectionReason == "invalid_timestamp")
            {
                return Results.Problem(
                    title: "Invalid timestamp",
                    detail: "The client timestamp is outside the acceptable range.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://novatune.dev/errors/invalid-timestamp");
            }

            // Return 202 Accepted for both accepted and sampled events
            return Results.Accepted(
                value: new TelemetryAcceptedResponse(true, correlationId));
        }
        catch (Exception)
        {
            return Results.Problem(
                title: "Service unavailable",
                detail: "Unable to process telemetry event. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
    }

    private static async Task<IResult> HandleIngestBatch(
        [FromBody] PlaybackEventBatchRequest request,
        [FromServices] ITelemetryIngestionService telemetryService,
        [FromServices] IOptions<TelemetryOptions> options,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // Validate batch size
        if (request.Events.Count == 0)
        {
            return Results.Problem(
                title: "Empty batch",
                detail: "Batch must contain at least one event.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        if (request.Events.Count > options.Value.MaxBatchSize)
        {
            return Results.Problem(
                title: "Batch too large",
                detail: $"Maximum {options.Value.MaxBatchSize} events per batch.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/batch-too-large");
        }

        // Validate all events in batch
        foreach (var evt in request.Events)
        {
            if (!PlaybackEventTypes.Valid.Contains(evt.EventType))
            {
                return Results.Problem(
                    title: "Invalid event type",
                    detail: $"Event type must be one of: {string.Join(", ", PlaybackEventTypes.Valid)}",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://novatune.dev/errors/invalid-event-type");
            }

            if (!Ulid.TryParse(evt.TrackId, out _))
            {
                return Results.Problem(
                    title: "Invalid track ID",
                    detail: "All track IDs must be valid ULIDs.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://novatune.dev/errors/invalid-track-id");
            }
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return TypedResults.Unauthorized();

        var correlationId = Activity.Current?.Id ?? Ulid.NewUlid().ToString();

        try
        {
            var result = await telemetryService.IngestBatchAsync(
                request.Events,
                userId,
                correlationId,
                ct);

            return Results.Accepted(
                value: new TelemetryBatchResponse(
                    result.AcceptedCount,
                    result.RejectedCount,
                    result.CorrelationId));
        }
        catch (Exception)
        {
            return Results.Problem(
                title: "Service unavailable",
                detail: "Unable to process telemetry batch. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
    }
}
