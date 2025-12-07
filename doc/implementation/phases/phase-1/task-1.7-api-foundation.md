# Task 1.7: API Foundation

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Pending
> **NFR Reference:** NF-8.4, NF-9.1

## Description

Set up API infrastructure including versioning, CORS, and documentation.

---

## Subtasks

### 1.7.1 Configure API Versioning

- [ ] Configure API versioning with `/api/v1/` prefix

**Create route groups in `Endpoints/` folder:**
```csharp
// Endpoints/EndpointRouteBuilderExtensions.cs
namespace NovaTuneApp.ApiService.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGroup("/api/v1")
            .WithOpenApi()
            .WithTags("v1");
    }
}
```

**Register in `Program.cs`:**
```csharp
var api = app.MapApiV1();

// Map endpoint groups
api.MapHealthEndpoints();
api.MapAuthEndpoints();  // Phase 2
api.MapTrackEndpoints(); // Phase 3+
```

**Example endpoint group:**
```csharp
// Endpoints/HealthEndpoints.cs
namespace NovaTuneApp.ApiService.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        var health = group.MapGroup("/health")
            .WithTags("Health");

        health.MapGet("/", () => Results.Ok(new { status = "healthy" }))
            .WithName("GetHealth")
            .WithSummary("Basic health check")
            .Produces<object>(StatusCodes.Status200OK);

        return group;
    }
}
```

---

### 1.7.2 Configure CORS Policy

- [ ] Configure CORS policy

**In `Program.cs`:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["https://localhost:5001"];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });

    // Restrictive policy for sensitive endpoints
    options.AddPolicy("Strict", policy =>
    {
        policy.WithOrigins("https://novatune.local")
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type", "Authorization")
            .AllowCredentials();
    });
});

// Apply CORS
app.UseCors("Default");
```

**Configuration in `appsettings.json`:**
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:5001",
      "https://novatune.local"
    ]
  }
}
```

**Development configuration:**
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173",
      "https://localhost:5001"
    ]
  }
}
```

---

### 1.7.3 Set Up Scalar OpenAPI Documentation

- [ ] Set up Scalar OpenAPI documentation at `/docs`

**Add NuGet package:**
```xml
<PackageReference Include="Scalar.AspNetCore" Version="1.2.0" />
```

**Configure in `Program.cs`:**
```csharp
// Add OpenAPI services
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "NovaTune API",
            Version = "v1",
            Description = "Audio management platform API",
            Contact = new OpenApiContact
            {
                Name = "NovaTune Team",
                Email = "api@novatune.local"
            }
        };
        return Task.CompletedTask;
    });
});

// Map OpenAPI endpoints
app.MapOpenApi();

// Configure Scalar UI
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options.Title = "NovaTune API";
        options.Theme = ScalarTheme.Purple;
        options.DarkMode = true;
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
        options.ShowSidebar = true;
        options.SearchHotKey = "k";
    });
}
```

---

### 1.7.4 Create Health Check Endpoints

- [ ] Create health check endpoints

**Full health endpoint configuration:**
```csharp
// Endpoints/HealthEndpoints.cs
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace NovaTuneApp.ApiService.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // Liveness - is the app running?
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse
        });

        // Readiness - are dependencies ready?
        app.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteResponse
        });

        // Detailed status (protected, for internal use)
        app.MapHealthChecks("/health/details", new HealthCheckOptions
        {
            ResponseWriter = WriteDetailedResponse
        }).RequireAuthorization("Admin");

        return app;
    }

    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow.ToString("O")
        };

        return context.Response.WriteAsJsonAsync(response);
    }

    private static Task WriteDetailedResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow.ToString("O"),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                data = e.Value.Data
            })
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}
```

---

### 1.7.5 Add Request/Response Logging Middleware

- [ ] Add request/response logging middleware

```csharp
// Infrastructure/RequestLoggingMiddleware.cs
namespace NovaTuneApp.ApiService.Infrastructure;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
```

**Register in `Program.cs`:**
```csharp
app.UseRequestLogging();
```

---

### 1.7.6 Configure JSON Serialization Options

- [ ] Configure JSON serialization options

```csharp
// In Program.cs
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

// For MVC controllers (if used)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });
```

---

## Complete Program.cs Template

```csharp
using NovaTuneApp.ApiService.Endpoints;
using NovaTuneApp.ApiService.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting NovaTune API");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));

    // Add service defaults (OpenTelemetry, health checks, etc.)
    builder.AddServiceDefaults();

    // Add OpenAPI
    builder.Services.AddOpenApi();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy =>
        {
            var origins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];
            policy.WithOrigins(origins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    // Configure JSON serialization
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    var app = builder.Build();

    // Middleware pipeline
    app.UseSecurityHeaders();
    app.UseCorrelationId();
    app.UseRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    app.UseHttpsRedirection();

    app.UseCors("Default");

    // OpenAPI/Scalar (development only)
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    // Map endpoints
    app.MapDefaultEndpoints(); // Health checks
    var api = app.MapApiV1();
    api.MapHealthEndpoints();

    app.Run();
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

## Acceptance Criteria

- [ ] `/health` returns 200
- [ ] `/docs` shows Scalar UI (development)
- [ ] CORS allows configured origins
- [ ] API uses `/api/v1/` prefix
- [ ] JSON responses use camelCase

---

## Verification Commands

```bash
# Test health endpoint
curl -s http://localhost:5000/health | jq .

# Test API versioning
curl -s http://localhost:5000/api/v1/health | jq .

# Test CORS preflight
curl -si -X OPTIONS http://localhost:5000/api/v1/health \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: GET"

# Open Scalar docs in browser
open http://localhost:5000/docs
```

---

## File Checklist

- [ ] `Endpoints/EndpointRouteBuilderExtensions.cs`
- [ ] `Endpoints/HealthEndpoints.cs`
- [ ] `Infrastructure/RequestLoggingMiddleware.cs`
- [ ] `Program.cs` (updated)
- [ ] `appsettings.json` (CORS configuration)

---

## Navigation

[Task 1.6: Security Headers](task-1.6-security-headers.md) | [Phase 1 Overview](overview.md) | [Task 1.8: Secrets Management](task-1.8-secrets-management.md)
