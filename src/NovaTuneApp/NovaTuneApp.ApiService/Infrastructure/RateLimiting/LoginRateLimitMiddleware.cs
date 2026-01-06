using System.Text.Json;
using NovaTuneApp.ApiService.Models.Auth;

namespace NovaTuneApp.ApiService.Infrastructure.RateLimiting;

/// <summary>
/// Middleware to extract email from login request body for per-account rate limiting.
/// </summary>
public class LoginRateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public LoginRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/auth/login") &&
            context.Request.Method == "POST" &&
            context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();

            try
            {
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                var login = JsonSerializer.Deserialize<LoginRequest>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (!string.IsNullOrEmpty(login?.Email))
                {
                    context.Items["login-email"] = login.Email.ToLowerInvariant();
                }
            }
            catch
            {
                // Ignore parse errors, fall back to IP-based limiting
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for rate limiting middleware.
/// </summary>
public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseLoginRateLimitMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LoginRateLimitMiddleware>();
    }
}
