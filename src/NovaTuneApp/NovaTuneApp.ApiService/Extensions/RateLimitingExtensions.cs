using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.ApiService.Infrastructure.RateLimiting;

namespace NovaTuneApp.ApiService.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds NovaTune rate limiting services for authentication endpoints.
    /// </summary>
    public static IHostApplicationBuilder AddNovaTuneRateLimiting(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<RateLimitSettings>(
            builder.Configuration.GetSection(RateLimitSettings.SectionName));

        var rateLimitSettings = builder.Configuration
            .GetSection(RateLimitSettings.SectionName)
            .Get<RateLimitSettings>() ?? new RateLimitSettings();

        // Register login rate limiter policy as singleton for shared state and proper disposal
        // Uses IOptions<RateLimitSettings> constructor for DI resolution
        builder.Services.AddSingleton<LoginRateLimiterPolicy>();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Auth: Login - chained limiter for both IP and per-account (Req 8.2)
            // Policy resolved from DI for proper lifecycle management
            options.AddPolicy<string, LoginRateLimiterPolicy>("auth-login");

            // Auth: Register per IP
            options.AddPolicy("auth-register", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitSettings.Auth.RegisterPerIp.PermitLimit,
                        Window = TimeSpan.FromMinutes(rateLimitSettings.Auth.RegisterPerIp.WindowMinutes),
                        SegmentsPerWindow = 4
                    }));

            // Auth: Refresh per IP
            options.AddPolicy("auth-refresh", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitSettings.Auth.RefreshPerIp.PermitLimit,
                        Window = TimeSpan.FromMinutes(rateLimitSettings.Auth.RefreshPerIp.WindowMinutes),
                        SegmentsPerWindow = 4
                    }));

            // Upload: Initiate per user (Req 8.2, NF-2.5 - 10 req/min per user)
            options.AddPolicy("upload-initiate", context =>
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub")
                    ?? "anonymous";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"upload-initiate:{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4
                    });
            });

            // Streaming: Per user (Req 8.2, NF-2.5 - 60 req/min per user)
            options.AddPolicy("stream-url", context =>
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub")
                    ?? "anonymous";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"stream-url:{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4
                    });
            });

            // On rejected: add Retry-After header and return Problem Details
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                // Record metric
                NovaTuneMetrics.RecordRateLimitViolation(
                    context.HttpContext.Request.Path,
                    "auth");

                // Log the violation
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<LoginRateLimiterPolicy>>();
                logger.LogWarning(
                    "Rate limit exceeded for {Endpoint} from {IP}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress);

                // Return Problem Details (Req 8.1)
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Type = "https://novatune.example/errors/rate-limit-exceeded",
                    Title = "Rate Limit Exceeded",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Too many requests. Please try again later.",
                    Instance = context.HttpContext.Request.Path
                }, token);
            };
        });

        return builder;
    }
}
