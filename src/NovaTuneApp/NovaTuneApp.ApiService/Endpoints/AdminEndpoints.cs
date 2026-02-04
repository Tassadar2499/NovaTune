using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Extensions;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;
using NovaTuneApp.ApiService.Services.Admin;

namespace NovaTuneApp.ApiService.Endpoints;

/// <summary>
/// Admin endpoints for user management, track moderation, analytics, and audit logs.
/// </summary>
public static class AdminEndpoints
{
    private static readonly string[] ValidUserSortFields =
        ["createdat", "lastloginat", "email", "trackcount"];

    private static readonly string[] ValidTrackSortFields =
        ["createdat", "title", "filesizebytes", "moderatedat"];

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin")
            .RequireAuthorization(PolicyNames.Admin)
            .WithTags("Admin")
            .WithOpenApi();

        // User Management
        var users = group.MapGroup("/users");

        users.MapGet("/", HandleListUsers)
            .WithName("AdminListUsers")
            .WithSummary("List all users with search, filter, and pagination")
            .Produces<PagedResult<AdminUserListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("admin-user-list");

        users.MapGet("/{userId}", HandleGetUser)
            .WithName("AdminGetUser")
            .WithSummary("Get user details for admin view")
            .Produces<AdminUserDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPatch("/{userId}", HandleUpdateUserStatus)
            .WithName("AdminUpdateUserStatus")
            .WithSummary("Update user status with reason code")
            .Produces<AdminUserDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("admin-user-modify");

        // Track Moderation
        var tracks = group.MapGroup("/tracks");

        tracks.MapGet("/", HandleListTracks)
            .WithName("AdminListTracks")
            .WithSummary("List all tracks with search, filter, and pagination")
            .Produces<PagedResult<AdminTrackListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("admin-track-list");

        tracks.MapGet("/{trackId}", HandleGetTrack)
            .WithName("AdminGetTrack")
            .WithSummary("Get track details for admin view")
            .Produces<AdminTrackDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tracks.MapPost("/{trackId}/moderate", HandleModerateTrack)
            .WithName("AdminModerateTrack")
            .WithSummary("Moderate a track with reason code")
            .Produces<AdminTrackDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("admin-track-modify");

        tracks.MapDelete("/{trackId}", HandleDeleteTrack)
            .WithName("AdminDeleteTrack")
            .WithSummary("Delete a track as admin with reason code")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting("admin-track-modify");

        // Analytics Dashboard
        var analytics = group.MapGroup("/analytics");

        analytics.MapGet("/overview", HandleGetOverview)
            .WithName("AdminAnalyticsOverview")
            .WithSummary("Get analytics overview for admin dashboard")
            .Produces<AnalyticsOverview>(StatusCodes.Status200OK)
            .RequireRateLimiting("admin-analytics");

        analytics.MapGet("/tracks/top", HandleGetTopTracks)
            .WithName("AdminTopTracks")
            .WithSummary("Get top tracks by play count")
            .Produces<TopTracksResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("admin-analytics");

        analytics.MapGet("/users/active", HandleGetActiveUsers)
            .WithName("AdminActiveUsers")
            .WithSummary("Get most active users")
            .Produces<ActiveUsersResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("admin-analytics");

        // Audit Logs (requires additional permission)
        var audit = group.MapGroup("/audit-logs")
            .RequireAuthorization(PolicyNames.AdminWithAuditAccess);

        audit.MapGet("/", HandleListAuditLogs)
            .WithName("AdminListAuditLogs")
            .WithSummary("List audit log entries with filtering")
            .Produces<PagedResult<AuditLogListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("admin-audit");

        audit.MapGet("/{auditId}", HandleGetAuditLog)
            .WithName("AdminGetAuditLog")
            .WithSummary("Get audit log entry details")
            .Produces<AuditLogDetails>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        audit.MapGet("/verify", HandleVerifyIntegrity)
            .WithName("AdminVerifyAuditIntegrity")
            .WithSummary("Verify audit log hash chain integrity")
            .Produces<AuditIntegrityResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    // ============================================================================
    // User Management Handlers
    // ============================================================================

    private static async Task<IResult> HandleListUsers(
        [AsParameters] AdminUserListQueryParams queryParams,
        [FromServices] IAdminUserService userService,
        CancellationToken ct)
    {
        // Validate sort field
        var sortBy = (queryParams.SortBy ?? "createdAt").ToLowerInvariant();
        if (!ValidUserSortFields.Contains(sortBy))
        {
            return Results.Problem(
                title: "Invalid sort field",
                detail: $"Sort field must be one of: {string.Join(", ", ValidUserSortFields)}",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-query-parameter");
        }

        var query = new AdminUserListQuery(
            queryParams.Search,
            queryParams.Status,
            sortBy,
            queryParams.SortOrder ?? "desc",
            queryParams.Cursor,
            queryParams.Limit ?? 50);

        var result = await userService.ListUsersAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetUser(
        [FromRoute] string userId,
        [FromServices] IAdminUserService userService,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(userId, out _))
        {
            return Results.Problem(
                title: "Invalid user ID",
                detail: "User ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-user-id");
        }

        var user = await userService.GetUserAsync(userId, ct);
        if (user is null)
        {
            return Results.Problem(
                title: "User not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/user-not-found");
        }

        return Results.Ok(user);
    }

    private static async Task<IResult> HandleUpdateUserStatus(
        [FromRoute] string userId,
        [FromBody] UpdateUserStatusRequest request,
        [FromServices] IAdminUserService userService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(userId, out _))
        {
            return Results.Problem(
                title: "Invalid user ID",
                detail: "User ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-user-id");
        }

        var adminUserId = httpContext.GetAdminUserId();
        if (string.IsNullOrEmpty(adminUserId))
            return TypedResults.Unauthorized();

        try
        {
            var result = await userService.UpdateUserStatusAsync(userId, request, adminUserId, ct);
            return Results.Ok(result);
        }
        catch (SelfModificationDeniedException)
        {
            return Results.Problem(
                title: "Self modification denied",
                detail: "Administrators cannot modify their own status.",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://novatune.dev/errors/self-modification-denied");
        }
        catch (UserNotFoundException)
        {
            return Results.Problem(
                title: "User not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/user-not-found");
        }
        catch (InvalidReasonCodeException ex)
        {
            return Results.Problem(
                title: "Invalid reason code",
                detail: $"Reason code '{ex.ReasonCode}' is not valid.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-reason-code");
        }
    }

    // ============================================================================
    // Track Moderation Handlers
    // ============================================================================

    private static async Task<IResult> HandleListTracks(
        [AsParameters] AdminTrackListQueryParams queryParams,
        [FromServices] IAdminTrackService trackService,
        CancellationToken ct)
    {
        // Validate sort field
        var sortBy = (queryParams.SortBy ?? "createdAt").ToLowerInvariant();
        if (!ValidTrackSortFields.Contains(sortBy))
        {
            return Results.Problem(
                title: "Invalid sort field",
                detail: $"Sort field must be one of: {string.Join(", ", ValidTrackSortFields)}",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-query-parameter");
        }

        var query = new AdminTrackListQuery(
            queryParams.Search,
            queryParams.Status,
            queryParams.ModerationStatus,
            queryParams.UserId,
            sortBy,
            queryParams.SortOrder ?? "desc",
            queryParams.Cursor,
            queryParams.Limit ?? 50);

        var result = await trackService.ListTracksAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetTrack(
        [FromRoute] string trackId,
        [FromServices] IAdminTrackService trackService,
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

        var track = await trackService.GetTrackAsync(trackId, ct);
        if (track is null)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }

        return Results.Ok(track);
    }

    private static async Task<IResult> HandleModerateTrack(
        [FromRoute] string trackId,
        [FromBody] ModerateTrackRequest request,
        [FromServices] IAdminTrackService trackService,
        HttpContext httpContext,
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

        var adminUserId = httpContext.GetAdminUserId();
        if (string.IsNullOrEmpty(adminUserId))
            return TypedResults.Unauthorized();

        try
        {
            var result = await trackService.ModerateTrackAsync(trackId, request, adminUserId, ct);
            return Results.Ok(result);
        }
        catch (TrackNotFoundException)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }
        catch (InvalidReasonCodeException ex)
        {
            return Results.Problem(
                title: "Invalid reason code",
                detail: $"Reason code '{ex.ReasonCode}' is not valid.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-reason-code");
        }
    }

    private static async Task<IResult> HandleDeleteTrack(
        [FromRoute] string trackId,
        [FromBody] AdminDeleteTrackRequest request,
        [FromServices] IAdminTrackService trackService,
        HttpContext httpContext,
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

        var adminUserId = httpContext.GetAdminUserId();
        if (string.IsNullOrEmpty(adminUserId))
            return TypedResults.Unauthorized();

        try
        {
            await trackService.DeleteTrackAsync(trackId, request, adminUserId, ct);
            return Results.NoContent();
        }
        catch (TrackNotFoundException)
        {
            return Results.Problem(
                title: "Track not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/track-not-found");
        }
        catch (InvalidReasonCodeException ex)
        {
            return Results.Problem(
                title: "Invalid reason code",
                detail: $"Reason code '{ex.ReasonCode}' is not valid.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-reason-code");
        }
    }

    // ============================================================================
    // Analytics Dashboard Handlers
    // ============================================================================

    private static async Task<IResult> HandleGetOverview(
        [FromServices] IAdminAnalyticsService analyticsService,
        CancellationToken ct)
    {
        var overview = await analyticsService.GetOverviewAsync(ct);
        return Results.Ok(overview);
    }

    private static async Task<IResult> HandleGetTopTracks(
        [FromQuery] int? count,
        [FromQuery] string? period,
        [FromServices] IAdminAnalyticsService analyticsService,
        CancellationToken ct)
    {
        var parsedPeriod = ParsePeriod(period ?? "7d");
        var result = await analyticsService.GetTopTracksAsync(count ?? 10, parsedPeriod, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetActiveUsers(
        [FromQuery] int? count,
        [FromQuery] string? period,
        [FromServices] IAdminAnalyticsService analyticsService,
        CancellationToken ct)
    {
        var parsedPeriod = ParsePeriod(period ?? "7d");
        var result = await analyticsService.GetActiveUsersAsync(count ?? 10, parsedPeriod, ct);
        return Results.Ok(result);
    }

    // ============================================================================
    // Audit Log Handlers
    // ============================================================================

    private static async Task<IResult> HandleListAuditLogs(
        [AsParameters] AuditLogQueryParams queryParams,
        [FromServices] IAuditLogService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var adminUserId = httpContext.GetAdminUserId();

        // Log audit access
        await auditService.LogAsync(httpContext.CreateAuditRequest(
            AuditActions.AuditLogViewed,
            AuditTargetTypes.AuditLog,
            "list",
            reasonCode: null,
            reasonText: null,
            previousState: null,
            newState: new { Filters = queryParams }), ct);

        var query = new AuditLogQuery(
            queryParams.ActorUserId,
            queryParams.Action,
            queryParams.TargetType,
            queryParams.TargetId,
            !string.IsNullOrEmpty(queryParams.StartDate) ? DateOnly.Parse(queryParams.StartDate) : null,
            !string.IsNullOrEmpty(queryParams.EndDate) ? DateOnly.Parse(queryParams.EndDate) : null,
            queryParams.Cursor,
            queryParams.Limit ?? 50);

        var result = await auditService.ListAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetAuditLog(
        [FromRoute] string auditId,
        [FromServices] IAuditLogService auditService,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(auditId, out _))
        {
            return Results.Problem(
                title: "Invalid audit ID",
                detail: "Audit ID must be a valid ULID.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-audit-id");
        }

        var entry = await auditService.GetAsync(auditId, ct);
        if (entry is null)
        {
            return Results.Problem(
                title: "Audit entry not found",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://novatune.dev/errors/audit-not-found");
        }

        return Results.Ok(entry);
    }

    private static async Task<IResult> HandleVerifyIntegrity(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromServices] IAuditLogService auditService,
        CancellationToken ct)
    {
        if (!DateOnly.TryParse(startDate, out var start))
        {
            return Results.Problem(
                title: "Invalid start date",
                detail: "Start date must be in YYYY-MM-DD format.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-date");
        }

        if (!DateOnly.TryParse(endDate, out var end))
        {
            return Results.Problem(
                title: "Invalid end date",
                detail: "End date must be in YYYY-MM-DD format.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://novatune.dev/errors/invalid-date");
        }

        var result = await auditService.VerifyIntegrityAsync(start, end, ct);
        return Results.Ok(result);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private static AnalyticsPeriod ParsePeriod(string period) => period.ToLowerInvariant() switch
    {
        "24h" => AnalyticsPeriod.Last24Hours,
        "7d" => AnalyticsPeriod.Last7Days,
        "30d" => AnalyticsPeriod.Last30Days,
        "all" => AnalyticsPeriod.AllTime,
        _ => AnalyticsPeriod.Last7Days
    };
}

/// <summary>
/// Query parameters for admin user list.
/// </summary>
public record AdminUserListQueryParams(
    [FromQuery] string? Search,
    [FromQuery] UserStatus? Status,
    [FromQuery] string? SortBy,
    [FromQuery] string? SortOrder,
    [FromQuery] string? Cursor,
    [FromQuery] int? Limit);

/// <summary>
/// Query parameters for admin track list.
/// </summary>
public record AdminTrackListQueryParams(
    [FromQuery] string? Search,
    [FromQuery] TrackStatus? Status,
    [FromQuery] ModerationStatus? ModerationStatus,
    [FromQuery] string? UserId,
    [FromQuery] string? SortBy,
    [FromQuery] string? SortOrder,
    [FromQuery] string? Cursor,
    [FromQuery] int? Limit);

/// <summary>
/// Query parameters for audit log list.
/// </summary>
public record AuditLogQueryParams(
    [FromQuery] string? ActorUserId,
    [FromQuery] string? Action,
    [FromQuery] string? TargetType,
    [FromQuery] string? TargetId,
    [FromQuery] string? StartDate,
    [FromQuery] string? EndDate,
    [FromQuery] string? Cursor,
    [FromQuery] int? Limit);
