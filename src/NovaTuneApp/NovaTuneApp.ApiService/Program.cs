using System.Diagnostics;
using Confluent.Kafka;
using KafkaFlow.Admin.Dashboard;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Endpoints;
using NovaTuneApp.ApiService.Extensions;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Logging;
using NovaTuneApp.ApiService.Infrastructure.Middleware;
using NovaTuneApp.ApiService.Infrastructure.RateLimiting;
using NovaTuneApp.ApiService.Services;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

// ============================================================================
// Serilog Bootstrap (NF-4.1)
// ============================================================================
// Configure Serilog early for startup logging before host is built.
// Full configuration is applied after builder is created.
// ============================================================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add service defaults & Aspire client integrations.
    builder.AddServiceDefaults();

    // ============================================================================
    // Serilog Full Configuration (NF-4.1)
    // ============================================================================
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("KafkaFlow", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .Enrich.With<SensitiveDataMaskingEnricher>()
        .Destructure.With<RedactedDestructuringPolicy>()
        .WriteTo.Console(new RenderedCompactJsonFormatter()));

    // Add HttpContextAccessor for correlation ID propagation
    builder.Services.AddHttpContextAccessor();

    // Register CorrelationIdDelegatingHandler for outgoing HTTP calls (NF-4.2)
    builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

    // Add Redis/Garnet client via Aspire.
    builder.AddRedisClient("cache");

    // Register cache service.
    builder.Services.AddSingleton<ICacheService, GarnetCacheService>();

    // ============================================================================
    // Authentication, Authorization & Rate Limiting (Stage 1)
    // ============================================================================
    builder.AddRavenDb();
    builder.AddNovaTuneAuthentication();
    builder.AddNovaTuneRateLimiting();

    // ============================================================================
    // Configuration Validation (NF-5.1)
    // ============================================================================
    // Validates required configuration at startup; fails fast on misconfiguration.
    // Settings validated: TopicPrefix, PresignedUrl TTL, Cache encryption, Rate limits, Quotas
    // ============================================================================
    builder.Services.Configure<NovaTuneOptions>(
        builder.Configuration.GetSection(NovaTuneOptions.SectionName));
    builder.Services.AddHostedService<ConfigurationValidationService>();

    // ============================================================================
    // Health Checks Configuration (NF-1.2)
    // ============================================================================
    // Required dependencies: RavenDB, Redpanda/Kafka, MinIO
    // Optional (degraded): Garnet/Redis cache
    // ============================================================================

    var kafkaBootstrap = builder.Configuration.GetConnectionString("messaging")
        ?? builder.Configuration["Kafka:BootstrapServers"]
        ?? "localhost:9092";

    var ravenDbUrl = builder.Configuration.GetConnectionString("novatune")
        ?? builder.Configuration["RavenDb:Url"]
        ?? "http://localhost:8080";

    var minioEndpoint = builder.Configuration.GetConnectionString("storage")
        ?? builder.Configuration["MinIO:Endpoint"]
        ?? "http://localhost:9000";

    builder.Services.AddHealthChecks()
        // RavenDB - Required for readiness
        .AddRavenDB(
            setup => setup.Urls = [ravenDbUrl],
            name: "ravendb",
            tags: [Extensions.ReadyTag])
        // Kafka/Redpanda - Required for readiness
        .AddKafka(
            new ProducerConfig { BootstrapServers = kafkaBootstrap },
            name: "kafka",
            tags: [Extensions.ReadyTag])
        // MinIO/S3 - Required for readiness (custom URI check)
        .AddUrlGroup(
            new Uri($"{minioEndpoint}/minio/health/live"),
            name: "minio",
            tags: [Extensions.ReadyTag])
        // Redis/Garnet - Optional, app degrades gracefully if unavailable
        .AddRedis(
            builder.Configuration.GetConnectionString("cache") ?? "localhost:6379",
            name: "redis",
            tags: [Extensions.ReadyTag, Extensions.OptionalTag]);

    // ============================================================================
    // Kafka/Redpanda Messaging Configuration
    // ============================================================================
    builder.AddNovaTuneMessaging();

    // Register stub services for handler dependencies.
    builder.Services.AddSingleton<ITrackService, TrackService>();
    builder.Services.AddSingleton<IStorageService, StorageService>();

    // Add services to the container.
    builder.Services.AddProblemDetails();

    // Configure model validation to return RFC 7807 Problem Details (Req 8.1)
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            var problemDetails = new ProblemDetails
            {
                Type = "https://novatune.example/errors/validation-error",
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "One or more validation errors occurred.",
                Instance = context.HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier,
                    ["errors"] = errors
                }
            };

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    app.UseExceptionHandler();

    // Add correlation ID middleware early in pipeline (NF-4.2)
    app.UseCorrelationId();

    // Add Serilog request logging (NF-4.1)
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            if (httpContext.Items[CorrelationIdMiddleware.ItemsKey] is string correlationId)
            {
                diagnosticContext.Set("CorrelationId", correlationId);
            }
        };
    });

    // Add login rate limit middleware (extracts email for per-account limiting)
    app.UseLoginRateLimitMiddleware();

    // Add rate limiting (Req 8.2, NF-2.5)
    app.UseRateLimiter();

    // Add authentication and authorization (Stage 1)
    app.UseAuthentication();
    app.UseAuthorization();

    // ============================================================================
    // OpenAPI & Documentation
    // ============================================================================
    // OpenAPI spec is available in all environments at /openapi/v1.json
    // Scalar UI is available in all environments at /scalar/v1
    // Production environments should add authentication middleware if needed
    // ============================================================================
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("NovaTune API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    if (app.Environment.IsDevelopment())
    {
        // Optional: KafkaFlow dashboard for debugging
        app.UseKafkaFlowDashboard();
        // Debug config endpoint for development only (NF-5.1)
        app.MapDebugConfigEndpoint();
    }

    // KafkaFlow will be started by its IHostedService automatically

    string[] summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

    // Map authentication endpoints (Stage 1)
    app.MapAuthEndpoints();

    app.MapGet("/weatherforecast", () =>
        {
            var forecast = Enumerable.Range(1, 5).Select(index =>
                    new WeatherForecast
                    (
                        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        Random.Shared.Next(-20, 55),
                        summaries[Random.Shared.Next(summaries.Length)]
                    ))
                .ToArray();
            return forecast;
        })
        .WithName("GetWeatherForecast");

    app.MapDefaultEndpoints();

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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Make Program class accessible for WebApplicationFactory in integration tests
public partial class Program { }
