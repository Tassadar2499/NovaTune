# 7. Endpoint Implementation

## `StreamEndpoints.cs`

```csharp
namespace NovaTuneApp.ApiService.Endpoints;

public static class StreamEndpoints
{
    public static void MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tracks")
            .RequireAuthorization(PolicyNames.ActiveUser)
            .WithTags("Streaming");

        group.MapPost("/{trackId}/stream", HandleStreamRequest)
            .WithName("GetStreamUrl")
            .WithSummary("Get presigned streaming URL for a track")
            .Produces<StreamResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
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
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (track.UserId != userId)
        {
            return Results.Problem(
                title: "Access denied",
                detail: "You do not have permission to access this track.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/forbidden");
        }

        // 4. Check track status
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

        // 5. Get or generate streaming URL
        var result = await streamingService.GetStreamUrlAsync(trackId, userId, ct);

        return Results.Ok(new StreamResponse(
            result.StreamUrl,
            result.ExpiresAt,
            result.ContentType,
            result.FileSizeBytes,
            result.SupportsRangeRequests));
    }
}

public record StreamResponse(
    string StreamUrl,
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes,
    bool SupportsRangeRequests);
```
