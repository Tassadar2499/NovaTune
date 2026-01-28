using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Services;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NovaTuneApp.ApiService.Endpoints;

/// <summary>
/// Track management endpoints for CRUD operations with soft-delete support.
/// </summary>
public static class TrackEndpoints
{
    private static readonly string[] ValidSortFields =
        ["createdat", "updatedat", "title", "artist", "duration"];

    public static void MapTrackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tracks")
            .RequireAuthorization(PolicyNames.ActiveUser)
            .WithTags("Tracks")
            .WithOpenApi();

        group.MapGet("/", HandleListTracks)
            .WithName("ListTracks")
            .WithSummary("List user's tracks with search, filter, and pagination")
            .Produces<PagedResult<TrackListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("track-list");

        group.MapGet("/{trackId}", HandleGetTrack)
            .WithName("GetTrack")
            .WithSummary("Get track details")
            .Produces<TrackDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{trackId}", HandleUpdateTrack)
            .WithName("UpdateTrack")
            .WithSummary("Update track metadata")
            .Produces<TrackDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("track-update");

        group.MapDelete("/{trackId}", HandleDeleteTrack)
            .WithName("DeleteTrack")
            .WithSummary("Soft-delete a track")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("track-delete");

        group.MapPost("/{trackId}/restore", HandleRestoreTrack)
            .WithName("RestoreTrack")
            .WithSummary("Restore a soft-deleted track")
            .Produces<TrackDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
    }

    private static async Task<IResult> HandleListTracks(
        [AsParameters] TrackListQueryParams queryParams,
        [FromServices] ITrackManagementService trackService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return TypedResults.Unauthorized();

        // Validate sort field
        var sortBy = (queryParams.SortBy ?? "createdAt").ToLowerInvariant();
        if (!ValidSortFields.Contains(sortBy))
        {
            return Results.Problem(
                title: "Invalid sort field",
                detail: $"Sort field must be one of: {string.Join(", ", ValidSortFields)}",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-query-parameter");
        }

        // Validate sort order
        var sortOrder = (queryParams.SortOrder ?? "desc").ToLowerInvariant();
        if (sortOrder != "asc" && sortOrder != "desc")
        {
            return Results.Problem(
                title: "Invalid sort order",
                detail: "Sort order must be 'asc' or 'desc'",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-query-parameter");
        }

        var query = new TrackListQuery(
            queryParams.Search,
            queryParams.Status,
            sortBy,
            sortOrder,
            queryParams.Cursor,
            queryParams.Limit ?? 20,
            queryParams.IncludeDeleted ?? false);

        try
        {
            var result = await trackService.ListTracksAsync(userId, query, ct);
            return Results.Ok(result);
        }
        catch (TimeoutRejectedException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The operation timed out. Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
        catch (BrokenCircuitException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The service is experiencing issues. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
    }

    private static async Task<IResult> HandleGetTrack(
        [FromRoute] string trackId,
        [FromServices] ITrackManagementService trackService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(trackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return TypedResults.Unauthorized();

        try
        {
            var track = await trackService.GetTrackAsync(trackId, userId, ct);
            return Results.Ok(track);
        }
        catch (TrackNotFoundException)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }
        catch (TrackAccessDeniedException)
        {
            return Results.Problem(
                title: "Access denied",
                detail: "You do not have permission to access this track.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/forbidden");
        }
        catch (TimeoutRejectedException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The operation timed out. Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
        catch (BrokenCircuitException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The service is experiencing issues. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
    }

    private static async Task<IResult> HandleUpdateTrack(
        [FromRoute] string trackId,
        [FromBody] UpdateTrackRequest request,
        [FromServices] ITrackManagementService trackService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(trackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        // Validate request
        if (request.Title is not null && (request.Title.Length == 0 || request.Title.Length > 255))
        {
            return Results.Problem(
                title: "Invalid title",
                detail: "Title must be between 1 and 255 characters.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        if (request.Artist is not null && request.Artist.Length > 255)
        {
            return Results.Problem(
                title: "Invalid artist",
                detail: "Artist must be 255 characters or less.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return TypedResults.Unauthorized();

        try
        {
            var track = await trackService.UpdateTrackAsync(trackId, userId, request, ct);
            return Results.Ok(track);
        }
        catch (TrackNotFoundException)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }
        catch (TrackAccessDeniedException)
        {
            return Results.Problem(
                title: "Access denied",
                detail: "You do not have permission to access this track.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/forbidden");
        }
        catch (TrackDeletedException ex)
        {
            return Results.Problem(
                title: "Track is deleted",
                detail: "Cannot update a deleted track. Restore the track first.",
                statusCode: StatusCodes.Status409Conflict,
                type: "https://novatune.dev/errors/track-deleted",
                extensions: new Dictionary<string, object?>
                {
                    ["trackId"] = trackId,
                    ["deletedAt"] = ex.DeletedAt
                });
        }
        catch (TimeoutRejectedException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The operation timed out. Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
        catch (BrokenCircuitException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The service is experiencing issues. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
    }

    private static async Task<IResult> HandleDeleteTrack(
        [FromRoute] string trackId,
        [FromServices] ITrackManagementService trackService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(trackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return TypedResults.Unauthorized();

        try
        {
            await trackService.DeleteTrackAsync(trackId, userId, ct);
            return Results.NoContent();
        }
        catch (TrackNotFoundException)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }
        catch (TrackAccessDeniedException)
        {
            return Results.Problem(
                title: "Access denied",
                detail: "You do not have permission to access this track.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/forbidden");
        }
        catch (TrackAlreadyDeletedException)
        {
            return Results.Problem(
                title: "Track already deleted",
                detail: "The track has already been deleted.",
                statusCode: StatusCodes.Status409Conflict,
                type: "https://novatune.dev/errors/already-deleted");
        }
        catch (TimeoutRejectedException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The operation timed out. Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
        catch (BrokenCircuitException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The service is experiencing issues. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
    }

    private static async Task<IResult> HandleRestoreTrack(
        [FromRoute] string trackId,
        [FromServices] ITrackManagementService trackService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(trackId, out _))
        {
            return Results.Problem(
                title: "Invalid track ID",
                detail: "Track ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-track-id");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return TypedResults.Unauthorized();

        try
        {
            var track = await trackService.RestoreTrackAsync(trackId, userId, ct);
            return Results.Ok(track);
        }
        catch (TrackNotFoundException)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }
        catch (TrackAccessDeniedException)
        {
            return Results.Problem(
                title: "Access denied",
                detail: "You do not have permission to access this track.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/forbidden");
        }
        catch (TrackNotDeletedException)
        {
            return Results.Problem(
                title: "Track not deleted",
                detail: "Only deleted tracks can be restored.",
                statusCode: StatusCodes.Status409Conflict,
                type: "https://novatune.dev/errors/not-deleted");
        }
        catch (TrackRestorationExpiredException ex)
        {
            return Results.Problem(
                title: "Restoration period expired",
                detail: "The track cannot be restored because the grace period has expired.",
                statusCode: StatusCodes.Status410Gone,
                type: "https://novatune.dev/errors/restoration-expired",
                extensions: new Dictionary<string, object?>
                {
                    ["trackId"] = trackId,
                    ["deletedAt"] = ex.DeletedAt,
                    ["scheduledDeletionAt"] = ex.ScheduledDeletionAt
                });
        }
        catch (TimeoutRejectedException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The operation timed out. Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
        catch (BrokenCircuitException)
        {
            return Results.Problem(
                title: "Service temporarily unavailable",
                detail: "The service is experiencing issues. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://novatune.dev/errors/service-unavailable");
        }
    }
}

/// <summary>
/// Query parameters for listing tracks.
/// </summary>
public record TrackListQueryParams(
    [FromQuery] string? Search,
    [FromQuery] TrackStatus? Status,
    [FromQuery] string? SortBy,
    [FromQuery] string? SortOrder,
    [FromQuery] string? Cursor,
    [FromQuery] int? Limit,
    [FromQuery] bool? IncludeDeleted);
