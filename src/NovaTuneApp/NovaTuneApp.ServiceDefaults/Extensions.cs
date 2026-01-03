using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string LivenessEndpointPath = "/health";
    private const string ReadinessEndpointPath = "/ready";

    /// <summary>
    /// Tag for health checks that must pass for the app to be considered alive (liveness probe).
    /// </summary>
    public const string LiveTag = "live";

    /// <summary>
    /// Tag for health checks that must pass for the app to accept traffic (readiness probe).
    /// Required dependencies that must be healthy.
    /// </summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Tag for optional dependencies that can degrade gracefully.
    /// App remains ready even if these fail.
    /// </summary>
    public const string OptionalTag = "optional";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    // Custom activity source and meter names for NovaTune observability (NF-4.x)
    public const string NovaTuneActivitySourceName = "NovaTune.Api";
    public const string NovaTuneMeterName = "NovaTune.Api";

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
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
                    // Register custom NovaTune metrics (NF-4.4)
                    .AddMeter(NovaTuneMeterName);
            })
            .WithTracing(tracing =>
            {
                tracing
                    // Register custom NovaTune activity source (NF-4.3)
                    .AddSource(NovaTuneActivitySourceName)
                    .AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(options =>
                        // Exclude health check requests from tracing
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(LivenessEndpointPath)
                            && !context.Request.Path.StartsWithSegments(ReadinessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Liveness probe: Only checks tagged with "live" must pass.
        // Used by Kubernetes to determine if the container should be restarted.
        app.MapHealthChecks(LivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(LiveTag)
        });

        // Readiness probe: Checks tagged with "ready" must pass.
        // Optional checks (tagged "optional") are excluded - they can fail without affecting readiness.
        // Used by Kubernetes to determine if the container should receive traffic.
        app.MapHealthChecks(ReadinessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(ReadyTag) && !r.Tags.Contains(OptionalTag)
        });

        return app;
    }

    public static TBuilder AddDefaultCaching<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddRedisClient("cache");
        return builder;
    }

    public static TBuilder AddDefaultMessaging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Kafka client configuration for Redpanda
        builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
        return builder;
    }
}
