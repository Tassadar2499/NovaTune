# Task 1.5: ServiceDefaults Configuration

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Pending

## Description

Configure shared service defaults for observability and resilience.

---

## Subtasks

### 1.5.1 Configure OpenTelemetry

- [ ] Configure OpenTelemetry in `NovaTuneApp.ServiceDefaults/Extensions.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NovaTuneApp.ServiceDefaults;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(
        this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("NovaTune.Upload")
                    .AddMeter("NovaTune.Streaming")
                    .AddMeter("NovaTune.Auth");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("NovaTune.Upload")
                    .AddSource("NovaTune.Streaming")
                    .AddSource("NovaTune.Auth");
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(
        this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry()
                .UseOtlpExporter();
        }

        return builder;
    }
}
```

---

### 1.5.2 Configure Serilog Structured Logging

- [ ] Configure Serilog with structured logging

**Add NuGet packages to ApiService:**
```xml
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Serilog.Enrichers.CorrelationId" Version="3.0.1" />
<PackageReference Include="Serilog.Formatting.Compact" Version="2.0.0" />
```

**Configure in `Program.cs`:**
```csharp
using Serilog;
using Serilog.Enrichers;
using Serilog.Events;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

try
{
    Log.Information("Starting NovaTune API");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ... rest of configuration
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

---

### 1.5.3 Add Correlation ID Middleware

- [ ] Create `Infrastructure/CorrelationIdMiddleware.cs`:

```csharp
namespace NovaTuneApp.ApiService.Infrastructure;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Add to log context
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogDebug("Request started. CorrelationId={CorrelationId}", correlationId);
            await _next(context);
            _logger.LogDebug("Request completed. CorrelationId={CorrelationId}", correlationId);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId)
            && !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
```

**Register in `Program.cs`:**
```csharp
app.UseCorrelationId();
```

---

### 1.5.4 Configure Health Check Endpoints

- [ ] Add health check endpoints to ServiceDefaults:

```csharp
public static IHostApplicationBuilder AddDefaultHealthChecks(
    this IHostApplicationBuilder builder)
{
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

    return builder;
}

public static WebApplication MapDefaultEndpoints(this WebApplication app)
{
    // Liveness probe - basic "is the app running" check
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => false // No checks, just "am I responding"
    });

    // Liveness probe with basic checks
    app.MapHealthChecks("/alive", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live")
    });

    // Readiness probe - check all dependencies
    app.MapHealthChecks("/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    return app;
}
```

---

### 1.5.5 Add Infrastructure Health Checks

- [ ] Create custom health checks for each infrastructure dependency

**RavenDB Health Check:**
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Raven.Client.Documents;

namespace NovaTuneApp.ApiService.Infrastructure.HealthChecks;

public class RavenDbHealthCheck : IHealthCheck
{
    private readonly IDocumentStore _documentStore;

    public RavenDbHealthCheck(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var session = _documentStore.OpenAsyncSession();
            await session.Query<object>().Take(1).ToListAsync(cancellationToken);
            return HealthCheckResult.Healthy("RavenDB is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RavenDB is not accessible", ex);
        }
    }
}
```

**MinIO Health Check:**
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;

namespace NovaTuneApp.ApiService.Infrastructure.HealthChecks;

public class MinioHealthCheck : IHealthCheck
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;

    public MinioHealthCheck(IMinioClient minioClient, IConfiguration configuration)
    {
        _minioClient = minioClient;
        _bucketName = configuration["MinIO:Bucket"] ?? "novatune-audio";
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName),
                cancellationToken);

            return exists
                ? HealthCheckResult.Healthy("MinIO bucket is accessible")
                : HealthCheckResult.Degraded($"MinIO bucket '{_bucketName}' does not exist");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO is not accessible", ex);
        }
    }
}
```

**Register health checks in ApiService:**
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<RavenDbHealthCheck>("ravendb", tags: ["ready", "db"])
    .AddCheck<MinioHealthCheck>("minio", tags: ["ready", "storage"]);
```

---

## Acceptance Criteria

- [ ] Logs include correlation IDs
- [ ] Health endpoints return proper status
- [ ] OpenTelemetry exports to Aspire dashboard

---

## Verification Commands

```bash
# Test health endpoint
curl -s http://localhost:5000/health | jq .

# Test readiness endpoint
curl -s http://localhost:5000/ready | jq .

# Test liveness endpoint
curl -s http://localhost:5000/alive | jq .

# Verify correlation ID in response
curl -si http://localhost:5000/health | grep -i "x-correlation-id"

# Check logs for structured JSON with correlation IDs
docker logs novatune-apiservice 2>&1 | head -20
```

---

## File Checklist

- [ ] `NovaTuneApp.ServiceDefaults/Extensions.cs` (updated)
- [ ] `NovaTuneApp.ApiService/Infrastructure/CorrelationIdMiddleware.cs`
- [ ] `NovaTuneApp.ApiService/Infrastructure/HealthChecks/RavenDbHealthCheck.cs`
- [ ] `NovaTuneApp.ApiService/Infrastructure/HealthChecks/MinioHealthCheck.cs`
- [ ] `NovaTuneApp.ApiService/Program.cs` (updated with Serilog)

---

## Navigation

[Task 1.4: Aspire AppHost](task-1.4-aspire-apphost.md) | [Phase 1 Overview](overview.md) | [Task 1.6: Security Headers](task-1.6-security-headers.md)
