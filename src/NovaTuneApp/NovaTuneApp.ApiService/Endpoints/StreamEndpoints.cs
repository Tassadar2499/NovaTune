using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Services;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Endpoints;

/// <summary>
/// Streaming endpoints for generating presigned streaming URLs (Req 5.1, 5.2).
/// </summary>
public static class StreamEndpoints
{
    public static void MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tracks")
            .WithTags("Streaming")
            .WithOpenApi()
            .RequireAuthorization(PolicyNames.ActiveUser);

        group.MapPost("/{trackId}/stream", HandleStreamRequest)
            .WithName("GetStreamUrl")
            .WithSummary("Get presigned streaming URL for a track")
            .WithDescription("Generates a short-lived presigned URL for streaming audio. URLs are cached and encrypted at rest.")
            .Produces<StreamResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting("stream-url");
    }

    private static async Task<IResult> HandleStreamRequest(
        [FromRoute] string trackId,
        [FromServices] IStreamingService streamingService,
        [FromServices] IAsyncDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // 1. Validate track ID format
        if (!Ulid.TryParse(trackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        // 2. Load and validate track
        var track = await session.LoadAsync<Track>($"Tracks/{trackId}", ct);
        if (track is null)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }

        // 3. Check ownership
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Unauthorized();
        }

        if (track.UserId != userId)
        {
            return Results.Problem(
                title: "Access denied",
                detail: "You do not have permission to access this track.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/forbidden");
        }

        // 4. Check track status (Req 5.1, NF-6.1)
        if (track.Status != TrackStatus.Ready)
        {
            return Results.Problem(
                title: "Track not ready for streaming",
                detail: $"Track is currently {track.Status}. Please wait until processing completes.",
                statusCode: StatusCodes.Status409Conflict,
                type: "https://novatune.dev/errors/track-not-ready",
                extensions: new Dictionary<string, object?>
                {
                    ["trackId"] = trackId,
                    ["currentStatus"] = track.Status.ToString()
                });
        }

        try
        {
            // 5. Get or generate streaming URL
            var result = await streamingService.GetStreamUrlAsync(trackId, userId, ct);

            return Results.Ok(new StreamResponse(
                result.StreamUrl,
                result.ExpiresAt,
                result.ContentType,
                result.FileSizeBytes,
                result.SupportsRangeRequests));
        }
        catch (Exception)
        {
            // MinIO or cache unavailable - fail-closed for MinIO per 09-resilience.md
            return Results.Problem(
                title: "Service unavailable",
                detail: "Unable to generate streaming URL. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
    }
}

/// <summary>
/// Response model for stream URL generation.
/// </summary>
public record StreamResponse(
    string StreamUrl,
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes,
    bool SupportsRangeRequests);
