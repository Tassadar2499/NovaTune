using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Admin.Dashboard;
using KafkaFlow.Configuration;
using KafkaFlow.Serializer;
using Microsoft.Extensions.Hosting;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Messaging;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Handlers;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Services;
using Raven.Client.Documents;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Redis/Garnet client via Aspire.
builder.AddRedisClient("cache");

// Register cache service.
builder.Services.AddSingleton<ICacheService, GarnetCacheService>();

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

// Configure KafkaFlow for Redpanda messaging.
var topicPrefix = builder.Configuration["Kafka:TopicPrefix"] ?? "dev";
// Use Aspire's connection string if available, otherwise fall back to config
var bootstrapServers = builder.Configuration.GetConnectionString("messaging")
    ?? builder.Configuration["Kafka:BootstrapServers"]
    ?? "localhost:9092";

builder.Services.AddKafka(kafka => kafka
    .UseMicrosoftLog()
    .AddCluster(cluster =>
    {
        cluster.WithBrokers(new[] { bootstrapServers });

        // Configure SASL if enabled
        if (builder.Configuration.GetValue<bool>("Kafka:SaslEnabled"))
        {
            cluster.WithSecurityInformation(security =>
            {
                security.SecurityProtocol = KafkaFlow.Configuration.SecurityProtocol.SaslSsl;
                security.SaslMechanism = KafkaFlow.Configuration.SaslMechanism.ScramSha256;
                security.SaslUsername = builder.Configuration["Kafka:SaslUsername"];
                security.SaslPassword = builder.Configuration["Kafka:SaslPassword"];
            });
        }

        // Audio events producer with retry configuration for delayed broker availability
        cluster.AddProducer("audio-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-audio-events")
            .WithProducerConfig(new Confluent.Kafka.ProducerConfig
            {
                MessageTimeoutMs = 60000,
                SocketTimeoutMs = 30000,
                RetryBackoffMs = 1000
            })
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        );

        // Track deletions producer with retry configuration
        cluster.AddProducer("deletion-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-track-deletions")
            .WithProducerConfig(new Confluent.Kafka.ProducerConfig
            {
                MessageTimeoutMs = 60000,
                SocketTimeoutMs = 30000,
                RetryBackoffMs = 1000
            })
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        );

        // Audio events consumer with retry configuration
        cluster.AddConsumer(consumer => consumer
            .Topic($"{topicPrefix}-audio-events")
            .WithGroupId($"{topicPrefix}-audio-processor")
            .WithBufferSize(100)
            .WithWorkersCount(3)
            .WithConsumerConfig(new Confluent.Kafka.ConsumerConfig
            {
                SessionTimeoutMs = 45000,
                SocketTimeoutMs = 30000,
                ReconnectBackoffMs = 1000
            })
            .AddMiddlewares(m => m
                .AddDeserializer<JsonCoreDeserializer>()
                .AddTypedHandlers(h => h.AddHandler<AudioUploadedHandler>())
            )
        );

        // Track deletions consumer with retry configuration
        cluster.AddConsumer(consumer => consumer
            .Topic($"{topicPrefix}-track-deletions")
            .WithGroupId($"{topicPrefix}-deletion-processor")
            .WithBufferSize(50)
            .WithWorkersCount(2)
            .WithConsumerConfig(new Confluent.Kafka.ConsumerConfig
            {
                SessionTimeoutMs = 45000,
                SocketTimeoutMs = 30000,
                ReconnectBackoffMs = 1000
            })
            .AddMiddlewares(m => m
                .AddDeserializer<JsonCoreDeserializer>()
                .AddTypedHandlers(h => h.AddHandler<TrackDeletedHandler>())
            )
        );
    })
);

// Register messaging services.
builder.Services.AddSingleton<IMessageProducerService, MessageProducerService>();
builder.Services.AddTransient<AudioUploadedHandler>();
builder.Services.AddTransient<TrackDeletedHandler>();

// Register KafkaFlow as a hosted service for background startup
builder.Services.AddHostedService<KafkaFlowHostedService>();

// Register stub services for handler dependencies.
builder.Services.AddSingleton<ITrackService, TrackService>();
builder.Services.AddSingleton<IStorageService, StorageService>();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
