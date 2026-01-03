using System.Diagnostics;
using Serilog.Context;

namespace NovaTuneApp.ApiService.Infrastructure.Middleware;

/// <summary>
/// Middleware that extracts or generates a correlation ID for request tracking.
/// Propagates the correlation ID in response headers and enriches log context.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemsKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract correlation ID from incoming request, fall back to trace ID or generate new
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        // Store in HttpContext for downstream access
        context.Items[ItemsKey] = correlationId;

        // Add to response headers for client tracking
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Enrich all logs within this request with the correlation ID
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogDebug("Request started with CorrelationId: {CorrelationId}", correlationId);
            await _next(context);
        }
    }
}

/// <summary>
/// Extension methods for registering correlation ID middleware.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds correlation ID middleware to the request pipeline.
    /// Should be added early in the pipeline to ensure all requests are tracked.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
