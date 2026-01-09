using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Serializer;
using Microsoft.Extensions.Options;
using Minio;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.Workers.AudioProcessor.Handlers;
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
    var builder = Host.CreateApplicationBuilder(args);

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

    var topicPrefix = builder.Configuration["NovaTune:TopicPrefix"]
        ?? builder.Configuration["Kafka:TopicPrefix"]
        ?? "dev";
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
    // Health Checks (NF-1.2)
    // ============================================================================
    builder.Services.AddHealthChecks()
        .AddRavenDB(
            setup => setup.Urls = [ravenDbUrl],
            name: "ravendb",
            timeout: TimeSpan.FromSeconds(5))
        .AddKafka(
            new ProducerConfig { BootstrapServers = bootstrapServers },
            name: "kafka",
            timeout: TimeSpan.FromSeconds(5))
        .AddUrlGroup(
            new Uri($"{minioEndpoint}/minio/health/live"),
            name: "minio",
            timeout: TimeSpan.FromSeconds(5));

    // ============================================================================
    // AudioProcessor Configuration (from 01-event-consumption.md)
    // ============================================================================
    var audioProcessorOptions = builder.Configuration
        .GetSection(AudioProcessorOptions.SectionName)
        .Get<AudioProcessorOptions>() ?? new AudioProcessorOptions();

    // ============================================================================
    // KafkaFlow Consumer (Req 3.2 - Consume AudioUploadedEvent)
    // ============================================================================
    builder.Services.AddKafka(kafka => kafka
        .UseMicrosoftLog()
        .AddCluster(cluster =>
        {
            cluster.WithBrokers([bootstrapServers]);

            // Audio events consumer (NF-2.1 - bounded concurrency)
            cluster.AddConsumer(consumer => consumer
                .Topic($"{topicPrefix}-audio-events")
                .WithGroupId("audio-processor-worker")
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
                    .AddTypedHandlers(h => h.AddHandler<AudioUploadedHandler>())
                )
            );
        })
    );

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

    var host = builder.Build();
    await host.RunAsync();
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
/// Background service that manages KafkaFlow bus lifecycle.
/// </summary>
internal class KafkaFlowHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaFlowHostedService> _logger;
    private IKafkaBus? _kafkaBus;

    private const int MaxRetries = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

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
        if (_kafkaBus is not null)
        {
            _logger.LogInformation("Stopping KafkaFlow bus...");
            await _kafkaBus.StopAsync();
            _logger.LogInformation("KafkaFlow bus stopped");
        }

        await base.StopAsync(cancellationToken);
    }
}
