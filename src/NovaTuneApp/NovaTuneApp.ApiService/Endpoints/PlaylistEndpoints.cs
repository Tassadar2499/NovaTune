using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Playlists;
using NovaTuneApp.ApiService.Services;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NovaTuneApp.ApiService.Endpoints;

/// <summary>
/// Playlist management endpoints for CRUD operations and track management.
/// </summary>
public static class PlaylistEndpoints
{
    private static readonly string[] ValidSortFields = ["createdat", "updatedat", "name", "trackcount"];

    public static void MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/playlists")
            .RequireAuthorization(PolicyNames.ActiveUser)
            .WithTags("Playlists")
            .WithOpenApi();

        // CRUD endpoints
        group.MapGet("/", HandleListPlaylists)
            .WithName("ListPlaylists")
            .WithSummary("List user's playlists with search and pagination")
            .Produces<PagedResult<PlaylistListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("playlist-list");

        group.MapPost("/", HandleCreatePlaylist)
            .WithName("CreatePlaylist")
            .WithSummary("Create a new playlist")
            .Produces<PlaylistDetails>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireRateLimiting("playlist-create");

        group.MapGet("/{playlistId}", HandleGetPlaylist)
            .WithName("GetPlaylist")
            .WithSummary("Get playlist details with tracks")
            .Produces<PlaylistDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{playlistId}", HandleUpdatePlaylist)
            .WithName("UpdatePlaylist")
            .WithSummary("Update playlist metadata")
            .Produces<PlaylistDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("playlist-update");

        group.MapDelete("/{playlistId}", HandleDeletePlaylist)
            .WithName("DeletePlaylist")
            .WithSummary("Delete a playlist")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("playlist-delete");

        // Track management endpoints
        group.MapPost("/{playlistId}/tracks", HandleAddTracks)
            .WithName("AddTracksToPlaylist")
            .WithSummary("Add tracks to a playlist")
            .Produces<PlaylistDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("playlist-tracks-add");

        group.MapDelete("/{playlistId}/tracks/{position:int}", HandleRemoveTrack)
            .WithName("RemoveTrackFromPlaylist")
            .WithSummary("Remove a track from a playlist by position")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("playlist-tracks-remove");

        group.MapPost("/{playlistId}/reorder", HandleReorderTracks)
            .WithName("ReorderPlaylistTracks")
            .WithSummary("Reorder tracks within a playlist")
            .Produces<PlaylistDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("playlist-reorder");
    }

    private static async Task<IResult> HandleListPlaylists(
        [AsParameters] PlaylistListQueryParams queryParams,
        [FromServices] IPlaylistService playlistService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = GetUserId(user);
        if (userId is null)
            return TypedResults.Unauthorized();

        // Validate sort field
        var sortBy = (queryParams.SortBy ?? "updatedAt").ToLowerInvariant();
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

        var query = new PlaylistListQuery(
            queryParams.Search,
            sortBy,
            sortOrder,
            queryParams.Cursor,
            queryParams.Limit ?? 20);

        try
        {
            var result = await playlistService.ListPlaylistsAsync(userId, query, ct);
            return Results.Ok(result);
        }
        catch (TimeoutRejectedException)
        {
            return ServiceUnavailableTimeout();
        }
        catch (BrokenCircuitException)
        {
            return ServiceUnavailableCircuitBreaker();
        }
    }

    private static async Task<IResult> HandleCreatePlaylist(
        [FromBody] CreatePlaylistRequest request,
        [FromServices] IPlaylistService playlistService,
        [FromServices] IOptions<PlaylistOptions> options,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = GetUserId(user);
        if (userId is null)
            return TypedResults.Unauthorized();

        // Validate name
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.Problem(
                title: "Invalid name",
                detail: "Playlist name is required.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        if (request.Name.Length > options.Value.MaxNameLength)
        {
            return Results.Problem(
                title: "Invalid name",
                detail: $"Playlist name must be {options.Value.MaxNameLength} characters or less.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        // Validate description
        if (request.Description?.Length > options.Value.MaxDescriptionLength)
        {
            return Results.Problem(
                title: "Invalid description",
                detail: $"Description must be {options.Value.MaxDescriptionLength} characters or less.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        try
        {
            var playlist = await playlistService.CreatePlaylistAsync(userId, request, ct);
            return Results.Created($"/playlists/{playlist.PlaylistId}", playlist);
        }
        catch (PlaylistQuotaExceededException ex)
        {
            return Results.Problem(
                title: "Playlist quota exceeded",
                detail: $"You have reached the maximum number of playlists ({ex.MaxCount}).",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/playlist-quota-exceeded",
                extensions: new Dictionary<string, object?>
                {
                    ["currentCount"] = ex.CurrentCount,
                    ["maxCount"] = ex.MaxCount
                });
        }
        catch (TimeoutRejectedException)
        {
            return ServiceUnavailableTimeout();
        }
        catch (BrokenCircuitException)
        {
            return ServiceUnavailableCircuitBreaker();
        }
    }

    private static async Task<IResult> HandleGetPlaylist(
        [FromRoute] string playlistId,
        [AsParameters] PlaylistDetailQueryParams queryParams,
        [FromServices] IPlaylistService playlistService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(playlistId, out _))
        {
            return Results.Problem(
                title: "Invalid playlist ID",
                detail: "Playlist ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-playlist-id");
        }

        var userId = GetUserId(user);
        if (userId is null)
            return TypedResults.Unauthorized();

        var query = new PlaylistDetailQuery(
            queryParams.IncludeTracks ?? true,
            queryParams.TrackCursor,
            queryParams.TrackLimit ?? 50);

        try
        {
            var playlist = await playlistService.GetPlaylistAsync(playlistId, userId, query, ct);
            return Results.Ok(playlist);
        }
        catch (PlaylistNotFoundException)
        {
            return PlaylistNotFound();
        }
        catch (PlaylistAccessDeniedException)
        {
            return PlaylistAccessDenied();
        }
        catch (TimeoutRejectedException)
        {
            return ServiceUnavailableTimeout();
        }
        catch (BrokenCircuitException)
        {
            return ServiceUnavailableCircuitBreaker();
        }
    }

    private static async Task<IResult> HandleUpdatePlaylist(
        [FromRoute] string playlistId,
        [FromBody] UpdatePlaylistRequest request,
        [FromServices] IPlaylistService playlistService,
        [FromServices] IOptions<PlaylistOptions> options,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(playlistId, out _))
        {
            return Results.Problem(
                title: "Invalid playlist ID",
                detail: "Playlist ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-playlist-id");
        }

        var userId = GetUserId(user);
        if (userId is null)
            return TypedResults.Unauthorized();

        // Validate name if provided
        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.Problem(
                    title: "Invalid name",
                    detail: "Playlist name cannot be empty.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://novatune.dev/errors/validation-error");
            }

            if (request.Name.Length > options.Value.MaxNameLength)
            {
                return Results.Problem(
                    title: "Invalid name",
                    detail: $"Playlist name must be {options.Value.MaxNameLength} characters or less.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://novatune.dev/errors/validation-error");
            }
        }

        // Validate description if provided
        if (request.HasDescription && request.Description?.Length > options.Value.MaxDescriptionLength)
        {
            return Results.Problem(
                title: "Invalid description",
                detail: $"Description must be {options.Value.MaxDescriptionLength} characters or less.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        try
        {
            var playlist = await playlistService.UpdatePlaylistAsync(playlistId, userId, request, ct);
            return Results.Ok(playlist);
        }
        catch (PlaylistNotFoundException)
        {
            return PlaylistNotFound();
        }
        catch (PlaylistAccessDeniedException)
        {
            return PlaylistAccessDenied();
        }
        catch (TimeoutRejectedException)
        {
            return ServiceUnavailableTimeout();
        }
        catch (BrokenCircuitException)
        {
            return ServiceUnavailableCircuitBreaker();
        }
    }

    private static async Task<IResult> HandleDeletePlaylist(
        [FromRoute] string playlistId,
        [FromServices] IPlaylistService playlistService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(playlistId, out _))
        {
            return Results.Problem(
                title: "Invalid playlist ID",
                detail: "Playlist ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-playlist-id");
        }

        var userId = GetUserId(user);
        if (userId is null)
            return TypedResults.Unauthorized();

        try
        {
            await playlistService.DeletePlaylistAsync(playlistId, userId, ct);
            return Results.NoContent();
        }
        catch (PlaylistNotFoundException)
        {
            return PlaylistNotFound();
        }
        catch (PlaylistAccessDeniedException)
        {
            return PlaylistAccessDenied();
        }
        catch (TimeoutRejectedException)
        {
            return ServiceUnavailableTimeout();
        }
        catch (BrokenCircuitException)
        {
            return ServiceUnavailableCircuitBreaker();
        }
    }

    private static async Task<IResult> HandleAddTracks(
        [FromRoute] string playlistId,
        [FromBody] AddTracksRequest request,
        [FromServices] IPlaylistService playlistService,
        [FromServices] IOptions<PlaylistOptions> options,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(playlistId, out _))
        {
            return Results.Problem(
                title: "Invalid playlist ID",
                detail: "Playlist ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-playlist-id");
        }

        var userId = GetUserId(user);
        if (userId is null)
            return TypedResults.Unauthorized();

        // Validate track IDs
        if (request.TrackIds is null || request.TrackIds.Count == 0)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: "At least one track ID is required.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        if (request.TrackIds.Count > options.Value.MaxTracksPerAddRequest)
        {
            return Results.Problem(
                title: "Too many tracks",
                detail: $"Maximum {options.Value.MaxTracksPerAddRequest} tracks per request.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        foreach (var trackId in request.TrackIds)
        {
            if (!Ulid.TryParse(trackId, out _))
            {
                return Results.Problem(
                    title: "Invalid track ID",
                    detail: $"Track ID '{trackId}' is not a valid ULID.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://novatune.dev/errors/invalid-track-id");
            }
        }

        // Validate position if provided
        if (request.Position.HasValue && request.Position.Value < 0)
        {
            return Results.Problem(
                title: "Invalid position",
                detail: "Position must be non-negative.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-position");
        }

        try
        {
            var playlist = await playlistService.AddTracksAsync(playlistId, userId, request, ct);
            return Results.Ok(playlist);
        }
        catch (PlaylistNotFoundException)
        {
            return PlaylistNotFound();
        }
        catch (PlaylistAccessDeniedException)
        {
            return PlaylistAccessDenied();
        }
        catch (TrackNotFoundException ex)
        {
            return Results.Problem(
                title: "Track not found",
                detail: $"Track '{ex.TrackId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }
        catch (TrackAccessDeniedException ex)
        {
            return Results.Problem(
                title: "Track access denied",
                detail: $"You do not have access to track '{ex.TrackId}'.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/track-forbidden");
        }
        catch (TrackDeletedException ex)
        {
            return Results.Problem(
                title: "Track is deleted",
                detail: $"Track '{ex.TrackId}' has been deleted and cannot be added.",
                statusCode: StatusCodes.Status409Conflict,
                type: "https://novatune.dev/errors/track-deleted");
        }
        catch (PlaylistTrackLimitExceededException ex)
        {
            return Results.Problem(
                title: "Track limit exceeded",
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/playlist-track-limit-exceeded",
                extensions: new Dictionary<string, object?>
                {
                    ["currentCount"] = ex.CurrentCount,
                    ["addCount"] = ex.AddCount,
                    ["maxCount"] = ex.MaxCount
                });
        }
        catch (InvalidPositionException ex)
        {
            return Results.Problem(
                title: "Invalid position",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-position",
                extensions: new Dictionary<string, object?>
                {
                    ["position"] = ex.Position,
                    ["maxPosition"] = ex.MaxPosition
                });
        }
        catch (TimeoutRejectedException)
        {
            return ServiceUnavailableTimeout();
        }
        catch (BrokenCircuitException)
        {
            return ServiceUnavailableCircuitBreaker();
        }
    }

    private static async Task<IResult> HandleRemoveTrack(
        [FromRoute] string playlistId,
        [FromRoute] int position,
        [FromServices] IPlaylistService playlistService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(playlistId, out _))
        {
            return Results.Problem(
                title: "Invalid playlist ID",
                detail: "Playlist ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-playlist-id");
        }

        if (position < 0)
        {
            return Results.Problem(
                title: "Invalid position",
                detail: "Position must be non-negative.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-position");
        }

        var userId = GetUserId(user);
        if (userId is null)
            return TypedResults.Unauthorized();

        try
        {
            await playlistService.RemoveTrackAsync(playlistId, userId, position, ct);
            return Results.NoContent();
        }
        catch (PlaylistNotFoundException)
        {
            return PlaylistNotFound();
        }
        catch (PlaylistAccessDeniedException)
        {
            return PlaylistAccessDenied();
        }
        catch (PlaylistTrackNotFoundException ex)
        {
            return Results.Problem(
                title: "Track not found in playlist",
                detail: $"No track at position {ex.Position}.",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-in-playlist");
        }
        catch (TimeoutRejectedException)
        {
            return ServiceUnavailableTimeout();
        }
        catch (BrokenCircuitException)
        {
            return ServiceUnavailableCircuitBreaker();
        }
    }

    private static async Task<IResult> HandleReorderTracks(
        [FromRoute] string playlistId,
        [FromBody] ReorderRequest request,
        [FromServices] IPlaylistService playlistService,
        [FromServices] IOptions<PlaylistOptions> options,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(playlistId, out _))
        {
            return Results.Problem(
                title: "Invalid playlist ID",
                detail: "Playlist ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-playlist-id");
        }

        var userId = GetUserId(user);
        if (userId is null)
            return TypedResults.Unauthorized();

        // Validate moves
        if (request.Moves is null || request.Moves.Count == 0)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: "At least one move operation is required.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        if (request.Moves.Count > options.Value.MaxMovesPerReorderRequest)
        {
            return Results.Problem(
                title: "Too many moves",
                detail: $"Maximum {options.Value.MaxMovesPerReorderRequest} moves per request.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/validation-error");
        }

        foreach (var move in request.Moves)
        {
            if (move.From < 0)
            {
                return Results.Problem(
                    title: "Invalid from position",
                    detail: "From position must be non-negative.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://novatune.dev/errors/invalid-position");
            }

            if (move.To < 0)
            {
                return Results.Problem(
                    title: "Invalid to position",
                    detail: "To position must be non-negative.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://novatune.dev/errors/invalid-position");
            }
        }

        try
        {
            var playlist = await playlistService.ReorderTracksAsync(playlistId, userId, request, ct);
            return Results.Ok(playlist);
        }
        catch (PlaylistNotFoundException)
        {
            return PlaylistNotFound();
        }
        catch (PlaylistAccessDeniedException)
        {
            return PlaylistAccessDenied();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("empty playlist"))
        {
            return Results.Problem(
                title: "Cannot reorder empty playlist",
                detail: "The playlist has no tracks to reorder.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/empty-playlist");
        }
        catch (InvalidPositionException ex)
        {
            return Results.Problem(
                title: "Invalid position",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-position",
                extensions: new Dictionary<string, object?>
                {
                    ["position"] = ex.Position,
                    ["maxPosition"] = ex.MaxPosition
                });
        }
        catch (TimeoutRejectedException)
        {
            return ServiceUnavailableTimeout();
        }
        catch (BrokenCircuitException)
        {
            return ServiceUnavailableCircuitBreaker();
        }
    }

    #region Helpers

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

    private static IResult PlaylistNotFound() =>
        Results.Problem(
            title: "Playlist not found",
            statusCode: StatusCodes.Status404NotFound,
            type: "https://novatune.dev/errors/playlist-not-found");

    private static IResult PlaylistAccessDenied() =>
        Results.Problem(
            title: "Access denied",
            detail: "You do not have permission to access this playlist.",
            statusCode: StatusCodes.Status403Forbidden,
            type: "https://novatune.dev/errors/forbidden");

    private static IResult ServiceUnavailableTimeout() =>
        Results.Problem(
            title: "Service temporarily unavailable",
            detail: "The operation timed out. Please try again.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: "https://novatune.dev/errors/service-unavailable");

    private static IResult ServiceUnavailableCircuitBreaker() =>
        Results.Problem(
            title: "Service temporarily unavailable",
            detail: "The service is experiencing issues. Please try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: "https://novatune.dev/errors/service-unavailable");

    #endregion
}

/// <summary>
/// Query parameters for listing playlists.
/// </summary>
public record PlaylistListQueryParams(
    [FromQuery] string? Search,
    [FromQuery] string? SortBy,
    [FromQuery] string? SortOrder,
    [FromQuery] string? Cursor,
    [FromQuery] int? Limit);

/// <summary>
/// Query parameters for getting playlist details.
/// </summary>
public record PlaylistDetailQueryParams(
    [FromQuery] bool? IncludeTracks,
    [FromQuery] string? TrackCursor,
    [FromQuery] int? TrackLimit);
