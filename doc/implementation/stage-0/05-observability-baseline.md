# Observability Baseline (`NF-4.x`)

Establish the instrumentation spine before feature work. Observability enables debugging, performance analysis, and incident response across the distributed system.

## Current State

OpenTelemetry is already configured in `ServiceDefaults/Extensions.cs`:

| Pillar   | Implementation                                                     | Status          |
|----------|--------------------------------------------------------------------|-----------------|
| Logging  | OpenTelemetry logging with `IncludeFormattedMessage` and scopes    | ✅ Implemented  |
| Tracing  | ASP.NET Core + HttpClient instrumentation, health checks filtered  | ✅ Implemented  |
| Metrics  | ASP.NET Core + HttpClient + Runtime instrumentation                | ✅ Implemented  |
| Exporter | OTLP exporter (auto-enabled when `OTEL_EXPORTER_OTLP_ENDPOINT` set)| ✅ Implemented  |

```csharp
// Current implementation in Extensions.cs
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(builder.Environment.ApplicationName)
            .AddAspNetCoreInstrumentation(options =>
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments("/health")
                    && !context.Request.Path.StartsWithSegments("/ready")
            )
            .AddHttpClientInstrumentation();
    });
```

## Requirements Summary

| Concern             | Implementation                                                       | Status         |
|---------------------|----------------------------------------------------------------------|----------------|
| Structured logging  | Serilog with JSON output; `CorrelationId` enricher                   | ⏳ Pending     |
| Distributed tracing | OpenTelemetry via Aspire; `traceparent` propagation                  | ✅ Implemented |
| Metrics             | Request rate/latency/error via OpenTelemetry                         | ✅ Implemented |
| Correlation         | `X-Correlation-Id` header propagation from gateway                   | ⏳ Pending     |
| Redaction           | Never log passwords, tokens, presigned URLs, object keys (`NF-4.5`) | ⏳ Pending     |

---

## Tasks

### NF-4.1: Enhance Logging with Serilog

Replace default .NET logging with Serilog for richer structured logging capabilities.

**Packages to add:**
```xml
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.0.1" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
<PackageReference Include="Serilog.Sinks.OpenTelemetry" Version="4.1.1" />
```

**Program.cs configuration:**
```csharp
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "NovaTuneApp.ApiService"
        };
    })
    .CreateLogger();

builder.Host.UseSerilog();
```

### NF-4.2: Correlation ID Propagation

Implement correlation ID middleware to track requests across service boundaries.

**Create `CorrelationIdMiddleware.cs`:**
```csharp
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

**Propagate to outgoing HTTP calls:**
```csharp
builder.Services.AddHttpClient("downstream", client => { })
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_httpContextAccessor.HttpContext?.Items["CorrelationId"] is string correlationId)
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
```

### NF-4.3: Custom Activity Sources

Add custom activity sources for business operations to enhance tracing granularity.

```csharp
public static class NovaTuneActivitySource
{
    public const string Name = "NovaTune.Api";
    public static readonly ActivitySource Source = new(Name, "1.0.0");
}

// Usage in services:
using var activity = NovaTuneActivitySource.Source.StartActivity("ProcessAudioUpload");
activity?.SetTag("track.id", trackId);
activity?.SetTag("track.format", format);
```

**Register in OpenTelemetry configuration:**
```csharp
.WithTracing(tracing =>
{
    tracing.AddSource(NovaTuneActivitySource.Name)  // Add custom source
           .AddSource(builder.Environment.ApplicationName)
           // ... existing instrumentation
});
```

### NF-4.4: Custom Metrics

Define business-specific metrics for monitoring application behavior.

```csharp
public static class NovaTuneMetrics
{
    private static readonly Meter Meter = new("NovaTune.Api", "1.0.0");

    public static readonly Counter<long> TracksUploaded = Meter.CreateCounter<long>(
        "novatune.tracks.uploaded",
        unit: "{tracks}",
        description: "Number of tracks uploaded");

    public static readonly Histogram<double> UploadDuration = Meter.CreateHistogram<double>(
        "novatune.upload.duration",
        unit: "ms",
        description: "Audio upload processing duration");

    public static readonly UpDownCounter<int> ActiveUploads = Meter.CreateUpDownCounter<int>(
        "novatune.uploads.active",
        unit: "{uploads}",
        description: "Currently processing uploads");
}

// Usage:
NovaTuneMetrics.TracksUploaded.Add(1, new("format", "mp3"), new("user.tier", "premium"));
```

**Register in OpenTelemetry configuration:**
```csharp
.WithMetrics(metrics =>
{
    metrics.AddMeter("NovaTune.Api")  // Add custom meter
           .AddAspNetCoreInstrumentation()
           // ... existing instrumentation
});
```

### NF-4.5: Log Redaction

Implement destructuring policy to prevent sensitive data from appearing in logs.

**Create `RedactedDestructuringPolicy.cs`:**
```csharp
public class RedactedDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "token", "secret", "key", "apikey", "api_key",
        "authorization", "bearer", "credential", "connectionstring",
        "presignedurl", "presigned_url", "objectkey", "object_key",
        "accesskey", "access_key", "secretkey", "secret_key"
    };

    public bool TryDestructure(object value, ILogEventPropertyValueFactory factory,
        out LogEventPropertyValue result)
    {
        if (value is string str && LooksLikeSensitiveValue(str))
        {
            result = new ScalarValue("[REDACTED]");
            return true;
        }

        result = null!;
        return false;
    }

    private static bool LooksLikeSensitiveValue(string value) =>
        value.Contains("://") && value.Contains("X-Amz-Signature") || // Presigned URLs
        value.Length > 20 && IsLikelyBase64OrToken(value);

    private static bool IsLikelyBase64OrToken(string value) =>
        value.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=' or '-' or '_');
}
```

**Serilog configuration with redaction:**
```csharp
Log.Logger = new LoggerConfiguration()
    .Destructure.With<RedactedDestructuringPolicy>()
    .Destructure.ByTransformingWhere<object>(
        type => type.GetProperties().Any(p => SensitiveProperties.Contains(p.Name)),
        obj => RedactSensitiveProperties(obj))
    // ... rest of configuration
```

**Alternative: Use log template sanitization:**
```csharp
// Avoid logging sensitive values directly
_logger.LogInformation("Generated presigned URL for track {TrackId}", trackId);
// NOT: _logger.LogInformation("Generated URL: {Url}", presignedUrl);
```

### NF-4.6: Kafka/Messaging Instrumentation

Add tracing for Kafka message production and consumption.

```csharp
// In KafkaFlow middleware or handler wrapper
public class TracingMiddleware : IMessageMiddleware
{
    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        using var activity = NovaTuneActivitySource.Source.StartActivity(
            $"kafka.{(context.ConsumerContext != null ? "consume" : "produce")}",
            ActivityKind.Consumer);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination", context.ConsumerContext?.Topic);
        activity?.SetTag("messaging.kafka.consumer_group", context.ConsumerContext?.GroupId);

        try
        {
            await next(context);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

---

## Configuration

### appsettings.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "KafkaFlow": "Information"
      }
    },
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    "Properties": {
      "Application": "NovaTuneApp.ApiService"
    }
  },
  "Observability": {
    "CorrelationIdHeader": "X-Correlation-Id",
    "RedactPatterns": [
      "password",
      "token",
      "X-Amz-Signature",
      "presigned"
    ]
  }
}
```

### Environment Variables

| Variable                        | Purpose                                      | Example                           |
|---------------------------------|----------------------------------------------|-----------------------------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT`   | OTLP collector endpoint                      | `http://localhost:4317`           |
| `OTEL_SERVICE_NAME`             | Service name for telemetry                   | `NovaTuneApp.ApiService`          |
| `OTEL_RESOURCE_ATTRIBUTES`      | Additional resource attributes               | `deployment.environment=staging`  |

---

## Aspire Dashboard Integration

When running locally via `dotnet run --project NovaTuneApp.AppHost`, the Aspire dashboard provides:

- **Traces**: Distributed traces across all services
- **Metrics**: Real-time metrics visualization
- **Logs**: Structured log aggregation
- **Resources**: Service health and status

Access at: `http://localhost:15000` (default Aspire dashboard port)

---

## Acceptance Criteria

- [ ] All API requests include `X-Correlation-Id` in response headers
- [ ] Correlation ID propagates to downstream HTTP calls and Kafka messages
- [ ] Structured JSON logs output to console in development
- [ ] OpenTelemetry traces visible in Aspire dashboard
- [ ] Custom metrics (`novatune.*`) appear in metrics dashboard
- [ ] Presigned URLs, passwords, and tokens are redacted from logs
- [ ] Health check endpoints (`/health`, `/ready`) excluded from tracing
- [ ] Kafka message processing creates child spans with topic/group metadata

---

## Files to Create/Modify

| File                                              | Action | Purpose                           |
|---------------------------------------------------|--------|-----------------------------------|
| `ApiService/Middleware/CorrelationIdMiddleware.cs`| Create | Correlation ID propagation        |
| `ApiService/Middleware/CorrelationIdHandler.cs`   | Create | Outgoing HTTP correlation         |
| `ApiService/Observability/NovaTuneActivitySource.cs`| Create | Custom tracing                  |
| `ApiService/Observability/NovaTuneMetrics.cs`     | Create | Custom metrics                    |
| `ApiService/Logging/RedactedDestructuringPolicy.cs`| Create | Log redaction                    |
| `ServiceDefaults/Extensions.cs`                   | Modify | Register custom sources/meters    |
| `ApiService/Program.cs`                           | Modify | Add Serilog, middleware           |
| `ApiService/NovaTuneApp.ApiService.csproj`        | Modify | Add Serilog packages              |

---

## References

- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Serilog](https://serilog.net/)
- [.NET Aspire Observability](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/telemetry)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)
