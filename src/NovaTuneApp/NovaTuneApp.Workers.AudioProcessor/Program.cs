using System.Text.Json;
using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Producers;
using KafkaFlow.Retry;
using KafkaFlow.Serializer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Minio;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.Workers.AudioProcessor.Handlers;
using NovaTuneApp.Workers.AudioProcessor.HealthChecks;
using NovaTuneApp.Workers.AudioProcessor.Middleware;
using NovaTuneApp.Workers.AudioProcessor.Services;
using Raven.Client.Documents;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

// ============================================================================
// Serilog Bootstrap
// ============================================================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add service defaults (OpenTelemetry, health checks, etc.)
    builder.AddServiceDefaults();

    // ============================================================================
    // Serilog Full Configuration
    // ============================================================================
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("KafkaFlow", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .WriteTo.Console(new RenderedCompactJsonFormatter()));

    // ============================================================================
    // Configuration
    // ============================================================================
    builder.Services.Configure<NovaTuneOptions>(
        builder.Configuration.GetSection(NovaTuneOptions.SectionName));
    builder.Services.Configure<AudioProcessorOptions>(
        builder.Configuration.GetSection(AudioProcessorOptions.SectionName));
    builder.Services.Configure<WorkerKafkaOptions>(
        builder.Configuration.GetSection(WorkerKafkaOptions.SectionName));

    // Bind Kafka options for use during startup
    var kafkaOptions = builder.Configuration
        .GetSection(WorkerKafkaOptions.SectionName)
        .Get<WorkerKafkaOptions>() ?? new WorkerKafkaOptions();

    var bootstrapServers = builder.Configuration.GetConnectionString("messaging")
        ?? builder.Configuration["Kafka:BootstrapServers"]
        ?? "localhost:9092";

    // ============================================================================
    // RavenDB
    // ============================================================================
    var ravenConnectionString = builder.Configuration.GetConnectionString("novatune");
    string ravenDbUrl;
    string ravenDbDatabase;

    if (ravenConnectionString != null && ravenConnectionString.Contains(';'))
    {
        var parts = ravenConnectionString.Split(';')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1]);

        ravenDbUrl = parts.GetValueOrDefault("URL") ?? "http://localhost:8080";
        ravenDbDatabase = parts.GetValueOrDefault("Database") ?? "NovaTune";
    }
    else
    {
        ravenDbUrl = ravenConnectionString
            ?? builder.Configuration["RavenDb:Url"]
            ?? "http://localhost:8080";
        ravenDbDatabase = builder.Configuration["RavenDb:Database"] ?? "NovaTune";
    }

    builder.Services.AddSingleton<IDocumentStore>(sp =>
    {
        var store = new DocumentStore
        {
            Urls = [ravenDbUrl],
            Database = ravenDbDatabase
        };
        store.Initialize();
        return store;
    });

    // ============================================================================
    // MinIO
    // ============================================================================
    var minioEndpoint = builder.Configuration.GetConnectionString("storage")
        ?? builder.Configuration["MinIO:Endpoint"]
        ?? "http://localhost:9000";
    var minioAccessKey = builder.Configuration["MinIO:AccessKey"] ?? "minioadmin";
    var minioSecretKey = builder.Configuration["MinIO:SecretKey"] ?? "minioadmin";
    var minioHost = minioEndpoint.Replace("http://", "").Replace("https://", "");
    var useSSL = minioEndpoint.StartsWith("https://");

    builder.Services.AddSingleton<IMinioClient>(_ =>
        new MinioClient()
            .WithEndpoint(minioHost)
            .WithCredentials(minioAccessKey, minioSecretKey)
            .WithSSL(useSSL)
            .Build());

    // ============================================================================
    // Health Checks (NF-1.2, 08-health-checks.md)
    // ============================================================================
    builder.Services.AddHealthChecks()
        // Infrastructure connectivity
        .AddRavenDB(
            setup => setup.Urls = [ravenDbUrl],
            name: "ravendb",
            tags: [Extensions.ReadyTag],
            timeout: TimeSpan.FromSeconds(5))
        .AddKafka(
            new ProducerConfig { BootstrapServers = bootstrapServers },
            name: "kafka",
            tags: [Extensions.ReadyTag],
            timeout: TimeSpan.FromSeconds(5))
        .AddUrlGroup(
            new Uri($"{minioEndpoint}/minio/health/live"),
            name: "minio",
            tags: [Extensions.ReadyTag],
            timeout: TimeSpan.FromSeconds(5))
        // Local requirements
        .AddCheck<FfprobeHealthCheck>("ffprobe", tags: [Extensions.ReadyTag])
        .AddCheck<FfmpegHealthCheck>("ffmpeg", tags: [Extensions.ReadyTag])
        .AddCheck<TempDirectoryHealthCheck>("temp-directory", tags: [Extensions.ReadyTag]);

    // ============================================================================
    // AudioProcessor Configuration (from 01-event-consumption.md)
    // ============================================================================
    var audioProcessorOptions = builder.Configuration
        .GetSection(AudioProcessorOptions.SectionName)
        .Get<AudioProcessorOptions>() ?? new AudioProcessorOptions();

    // ============================================================================
    // KafkaFlow Consumer (Req 3.2 - Consume AudioUploadedEvent)
    // ============================================================================
    var audioEventsTopic = kafkaOptions.GetAudioEventsTopic();
    var dlqTopic = kafkaOptions.GetDlqTopic();

    builder.Services.AddKafka(kafka => kafka
        .UseMicrosoftLog()
        .AddCluster(cluster =>
        {
            cluster.WithBrokers([bootstrapServers]);

            // DLQ Producer (06-error-handling.md)
            cluster.CreateTopicIfNotExists(dlqTopic, numberOfPartitions: 1, replicationFactor: 1);
            cluster.AddProducer(
                "dlq-producer",
                producer => producer
                    .DefaultTopic(dlqTopic)
                    .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
            );

            // Audio events consumer (NF-2.1 - bounded concurrency)
            cluster.AddConsumer(consumer => consumer
                .Topic(audioEventsTopic)
                .WithGroupId(kafkaOptions.ConsumerGroup)
                .WithBufferSize(audioProcessorOptions.MaxConcurrency * 2)
                .WithWorkersCount(audioProcessorOptions.MaxConcurrency)
                .WithAutoOffsetReset(KafkaFlow.AutoOffsetReset.Earliest)
                .WithConsumerConfig(new ConsumerConfig
                {
                    // Consumer Configuration (NF-2.1)
                    SessionTimeoutMs = 30000,  // SessionTimeout: 30s
                    SocketTimeoutMs = 30000,
                    ReconnectBackoffMs = 1000,
                    EnableAutoCommit = false,
                    AutoCommitIntervalMs = 5000 // CommitInterval: 5s (when auto-commit enabled)
                })
                .AddMiddlewares(m => m
                    .AddDeserializer<JsonCoreDeserializer>()
                    // Retry middleware (06-error-handling.md): 3 retries with exponential backoff
                    .RetrySimple(
                        (config) => config
                            .Handle<Exception>()
                            .TryTimes(3)
                            .WithTimeBetweenTriesPlan(retryCount =>
                                TimeSpan.FromSeconds(Math.Pow(2, retryCount))) // 2s, 4s, 8s
                            .ShouldPauseConsumer(false)
                    )
                    // DLQ middleware - catches messages that failed all retries
                    .Add<DlqMiddleware>()
                    .AddTypedHandlers(h => h
                        .AddHandler<AudioUploadedHandler>()
                        .WithHandlerLifetime(InstanceLifetime.Scoped)
                    )
                )
            );
        })
    );

    // DLQ handler service for publishing failed messages
    builder.Services.AddSingleton<IDlqHandler, DlqHandler>();

    // ============================================================================
    // Services (02-processing-pipeline.md)
    // ============================================================================

    // Handler
    builder.Services.AddTransient<AudioUploadedHandler>();

    // Core processing services
    builder.Services.AddScoped<IAudioProcessorService, AudioProcessorService>();
    builder.Services.AddSingleton<ITempFileManager, TempFileManager>();
    builder.Services.AddSingleton<IFfprobeService, FfprobeService>();
    builder.Services.AddSingleton<IWaveformService, WaveformService>();

    // Storage service - requires resilience pipeline provider
    builder.Services.AddScoped<NovaTuneApp.ApiService.Services.IStorageService, NovaTuneApp.ApiService.Services.StorageService>();

    // ============================================================================
    // KafkaFlow Hosted Service
    // ============================================================================
    builder.Services.AddHostedService<KafkaFlowHostedService>();

    var app = builder.Build();

    // Map health endpoints (08-health-checks.md)
    app.MapDefaultEndpoints();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Audio Processor Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Background service that manages KafkaFlow bus lifecycle with graceful shutdown.
/// Implements 10-resilience.md graceful shutdown requirements:
/// 1. Stop accepting new messages
/// 2. Wait for in-flight processing to complete (timeout: 60s)
/// 3. Commit final offsets
/// 4. Clean up temp files
/// 5. Exit
/// </summary>
internal class KafkaFlowHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaFlowHostedService> _logger;
    private IKafkaBus? _kafkaBus;

    private const int MaxRetries = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan GracefulShutdownTimeout = TimeSpan.FromSeconds(60);

    public KafkaFlowHostedService(
        IServiceProvider serviceProvider,
        ILogger<KafkaFlowHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Audio Processor KafkaFlow bus...");

        // Clean up any orphaned temp directories from previous runs on startup
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var tempFileManager = scope.ServiceProvider.GetService<NovaTuneApp.Workers.AudioProcessor.Services.ITempFileManager>();
            tempFileManager?.CleanupOrphanedDirectories();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up orphaned directories on startup");
        }

        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _kafkaBus = _serviceProvider.CreateKafkaBus();
                await _kafkaBus.StartAsync(stoppingToken);
                _logger.LogInformation("KafkaFlow bus started successfully on attempt {Attempt}", attempt);

                await Task.Delay(Timeout.Infinite, stoppingToken);
                return;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("KafkaFlow bus stopping due to cancellation");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to start KafkaFlow bus (attempt {Attempt}/{MaxRetries})",
                    attempt,
                    MaxRetries);

                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                else
                {
                    _logger.LogError(ex, "Failed to start KafkaFlow bus after {MaxRetries} attempts", MaxRetries);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initiating graceful shutdown...");
        var shutdownStopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (_kafkaBus is not null)
        {
            // Step 1 & 2: Stop accepting new messages and wait for in-flight processing
            // KafkaFlow's StopAsync handles this gracefully - it stops consumers and waits for handlers
            _logger.LogInformation(
                "Stopping KafkaFlow bus, waiting up to {TimeoutSeconds}s for in-flight messages...",
                GracefulShutdownTimeout.TotalSeconds);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(GracefulShutdownTimeout);

            try
            {
                // KafkaFlow StopAsync commits final offsets (Step 3) as part of shutdown
                await _kafkaBus.StopAsync();
                _logger.LogInformation(
                    "KafkaFlow bus stopped gracefully in {ElapsedMs}ms",
                    shutdownStopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Graceful shutdown timed out after {TimeoutSeconds}s, forcing stop",
                    GracefulShutdownTimeout.TotalSeconds);
            }
        }

        // Step 4: Clean up temp files
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var tempFileManager = scope.ServiceProvider.GetService<NovaTuneApp.Workers.AudioProcessor.Services.ITempFileManager>();
            if (tempFileManager is not null)
            {
                _logger.LogInformation("Cleaning up temp directories...");
                tempFileManager.CleanupOrphanedDirectories();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temp directories during shutdown");
        }

        _logger.LogInformation(
            "Graceful shutdown completed in {ElapsedMs}ms",
            shutdownStopwatch.ElapsedMilliseconds);

        // Step 5: Exit
        await base.StopAsync(cancellationToken);
    }
}
