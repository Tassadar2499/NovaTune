using System.Diagnostics;
using Confluent.Kafka;
using KafkaFlow.Admin.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Minio;
using NovaTuneApp.ApiService.Endpoints;
using NovaTuneApp.ApiService.Extensions;
using NovaTuneApp.ApiService.Infrastructure.BackgroundServices;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Logging;
using NovaTuneApp.ApiService.Infrastructure.Messaging;
using NovaTuneApp.ApiService.Infrastructure.Middleware;
using NovaTuneApp.ApiService.Infrastructure.RateLimiting;
using NovaTuneApp.ApiService.Services;
using Polly.Registry;
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
    // Track Management Configuration (Stage 5)
    // ============================================================================
    builder.Services.AddOptions<TrackManagementOptions>()
        .Bind(builder.Configuration.GetSection(TrackManagementOptions.SectionName))
        .ValidateDataAnnotations()
        .Validate(options =>
        {
            if (options.DeletionGracePeriod < TimeSpan.FromMinutes(1))
                return false;
            if (options.DefaultPageSize > options.MaxPageSize)
                return false;
            return true;
        }, "Invalid TrackManagementOptions: DeletionGracePeriod must be >= 1 minute, DefaultPageSize must be <= MaxPageSize");

    // ============================================================================
    // Playlist Management Configuration (Stage 6)
    // ============================================================================
    builder.Services.AddOptions<PlaylistOptions>()
        .Bind(builder.Configuration.GetSection(PlaylistOptions.SectionName))
        .ValidateDataAnnotations()
        .Validate(options =>
        {
            if (options.DefaultPageSize > options.MaxPageSize)
                return false;
            if (options.MaxTracksPerAddRequest > options.MaxTracksPerPlaylist)
                return false;
            return true;
        }, "Invalid PlaylistOptions: DefaultPageSize must be <= MaxPageSize, MaxTracksPerAddRequest must be <= MaxTracksPerPlaylist");

    // ============================================================================
    // Health Checks Configuration (NF-1.2)
    // ============================================================================
    // Required dependencies: RavenDB
    // Optional by configuration: Redpanda/Kafka, MinIO
    // Optional (degraded): Garnet/Redis cache
    // ============================================================================

    var messagingEnabled = builder.Configuration.GetValue(
        "Features:MessagingEnabled",
        !builder.Environment.IsEnvironment("Testing"));
    var storageEnabled = builder.Configuration.GetValue(
        "Features:StorageEnabled",
        !builder.Environment.IsEnvironment("Testing"));

    // Parse RavenDB connection string (format: "URL=http://host:port;Database=dbname")
    var ravenConnectionString = builder.Configuration.GetConnectionString("novatune");
    string ravenDbUrl;
    if (ravenConnectionString != null && ravenConnectionString.Contains(';'))
    {
        var parts = ravenConnectionString.Split(';')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1]);
        ravenDbUrl = parts.GetValueOrDefault("URL") ?? "http://localhost:8080";
    }
    else
    {
        ravenDbUrl = ravenConnectionString
            ?? builder.Configuration["RavenDb:Url"]
            ?? "http://localhost:8080";
    }

    var healthChecks = builder.Services.AddHealthChecks()
        // RavenDB - Required for readiness
        .AddRavenDB(
            setup => setup.Urls = [ravenDbUrl],
            name: "ravendb",
            timeout: TimeSpan.FromSeconds(5),
            tags: [Extensions.ReadyTag]);

    if (messagingEnabled)
    {
        var kafkaBootstrap = builder.Configuration.GetConnectionString("messaging")
            ?? builder.Configuration["Kafka:BootstrapServers"]
            ?? "localhost:9092";

        // Kafka/Redpanda - Required for readiness when messaging is enabled
        healthChecks.AddKafka(
            new ProducerConfig { BootstrapServers = kafkaBootstrap },
            name: "kafka",
            timeout: TimeSpan.FromSeconds(5),
            tags: [Extensions.ReadyTag]);
    }

    if (storageEnabled)
    {
        var minioEndpoint = builder.Configuration.GetConnectionString("storage")
            ?? builder.Configuration["MinIO:Endpoint"]
            ?? "http://localhost:9000";

        // MinIO/S3 - Required for readiness when storage is enabled (custom URI check)
        healthChecks.AddUrlGroup(
            new Uri($"{minioEndpoint}/minio/health/live"),
            name: "minio",
            timeout: TimeSpan.FromSeconds(5),
            tags: [Extensions.ReadyTag]);

        // Register MinIO client for storage operations
        var minioAccessKey = builder.Configuration["MinIO:AccessKey"] ?? "minioadmin";
        var minioSecretKey = builder.Configuration["MinIO:SecretKey"] ?? "minioadmin";

        // Parse endpoint (remove protocol for MinIO client)
        var minioHost = minioEndpoint.Replace("http://", "").Replace("https://", "");
        var useSSL = minioEndpoint.StartsWith("https://");

        builder.Services.AddSingleton<IMinioClient>(_ =>
            new MinioClient()
                .WithEndpoint(minioHost)
                .WithCredentials(minioAccessKey, minioSecretKey)
                .WithSSL(useSSL)
                .Build());
    }
    else
    {
        // Register a no-op MinIO client for testing environments
        builder.Services.AddSingleton<IMinioClient>(_ =>
            new MinioClient()
                .WithEndpoint("localhost:9000")
                .WithCredentials("minioadmin", "minioadmin")
                .Build());
    }

    // Redis/Garnet - Optional, app degrades gracefully if unavailable
    healthChecks.AddRedis(
        builder.Configuration.GetConnectionString("cache") ?? "localhost:6379",
        name: "redis",
        timeout: TimeSpan.FromSeconds(5),
        tags: [Extensions.ReadyTag, Extensions.OptionalTag]);

    // ============================================================================
    // Kafka/Redpanda Messaging Configuration
    // ============================================================================
    if (messagingEnabled)
    {
        builder.AddNovaTuneMessaging();
    }
    else
    {
        builder.Services.AddSingleton<IMessageProducerService, NoOpMessageProducerService>();
    }

    // Register services for handler dependencies.
    builder.Services.AddSingleton<ITrackService, TrackService>();
    builder.Services.AddSingleton<IStorageService, StorageService>();
    builder.Services.AddScoped<IUploadService, UploadService>();
    // Decorator pattern: wrap TrackManagementService with resilience policies (NF-1.4)
    builder.Services.AddScoped<TrackManagementService>();
    builder.Services.AddScoped<ITrackManagementService>(sp =>
        new ResilientTrackManagementService(
            sp.GetRequiredService<TrackManagementService>(),
            sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
            sp.GetRequiredService<ILogger<ResilientTrackManagementService>>()));

    // Playlist Management Service (Stage 6)
    builder.Services.AddScoped<IPlaylistService, PlaylistService>();

    // ============================================================================
    // Streaming Services (Stage 4)
    // ============================================================================
    // Presigned URL generation with encrypted cache support.
    // ============================================================================
    builder.Services.Configure<StreamingOptions>(
        builder.Configuration.GetSection(StreamingOptions.SectionName));
    builder.Services.Configure<CacheEncryptionOptions>(
        builder.Configuration.GetSection(CacheEncryptionOptions.SectionName));
    builder.Services.AddSingleton<ICacheEncryptionProvider, AesGcmCacheEncryptionProvider>();
    builder.Services.AddSingleton<IEncryptedCacheService, EncryptedCacheService>();
    builder.Services.AddScoped<IStreamingService, StreamingService>();

    // ============================================================================
    // MinIO Bucket Initialization (Stage 2)
    // ============================================================================
    // Ensures audio bucket exists with versioning enabled.
    // ============================================================================
    if (storageEnabled)
    {
        builder.Services.AddHostedService<MinioInitializationService>();
    }

    // ============================================================================
    // Upload Session Cleanup (Stage 2)
    // ============================================================================
    // Background service that marks expired sessions and cleans up old records.
    // ============================================================================
    builder.Services.AddHostedService<UploadSessionCleanupService>();

    // ============================================================================
    // Outbox Processor (NF-5.2)
    // ============================================================================
    // Background service that polls for pending outbox messages and publishes
    // them to Kafka/Redpanda with retry and exponential backoff.
    // ============================================================================
    if (messagingEnabled)
    {
        builder.Services.AddHostedService<OutboxProcessorService>();
    }

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

    if (app.Environment.IsDevelopment() && messagingEnabled)
    {
        // Optional: KafkaFlow dashboard for debugging
        app.UseKafkaFlowDashboard();
        // Debug config endpoint for development only (NF-5.1)
        app.MapDebugConfigEndpoint();
    }

    string[] summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

    // Map authentication endpoints (Stage 1)
    app.MapAuthEndpoints();

    // Map upload endpoints (Stage 2)
    app.MapUploadEndpoints();

    // Map streaming endpoints (Stage 4)
    app.MapStreamEndpoints();

    // Map track management endpoints (Stage 5)
    app.MapTrackEndpoints();

    // Map playlist management endpoints (Stage 6)
    app.MapPlaylistEndpoints();

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
