using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Infrastructure;
using NovaTuneApp.ApiService.Models.Auth;
using NovaTuneApp.ApiService.Services;

namespace NovaTuneApp.ApiService.Endpoints;

/// <summary>
/// Authentication endpoints (Req 1.1, 1.2, 1.5).
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication")
            .WithOpenApi()
            .AddEndpointFilter<AuthExceptionFilter>();

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithDescription("Register a new user account")
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("auth-register");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithDescription("Authenticate and receive JWT tokens")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireRateLimiting("auth-login");

        group.MapPost("/refresh", Refresh)
            .WithName("RefreshToken")
            .WithDescription("Exchange refresh token for new token pair")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting("auth-refresh");

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithDescription("Revoke current session")
            .RequireAuthorization(PolicyNames.Listener)
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> Register(
        [FromBody] RegisterRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        var user = await authService.RegisterAsync(request, ct);
        return TypedResults.Created($"/users/{user.UserId}", user);
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var deviceId = httpContext.Request.Headers["X-Device-Id"].FirstOrDefault();
        var response = await authService.LoginAsync(request, deviceId, ct);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> Refresh(
        [FromBody] RefreshRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var deviceId = httpContext.Request.Headers["X-Device-Id"].FirstOrDefault();
        var response = await authService.RefreshAsync(request, deviceId, ct);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> Logout(
        [FromBody] RefreshRequest? request,
        ClaimsPrincipal user,
        IAuthService authService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Unauthorized();
        }

        if (request != null && !string.IsNullOrEmpty(request.RefreshToken))
        {
            await authService.LogoutAsync(userId, request.RefreshToken, ct);
        }
        else
        {
            // If no refresh token provided, revoke all sessions
            await authService.LogoutAllAsync(userId, ct);
        }

        return TypedResults.NoContent();
    }
}
