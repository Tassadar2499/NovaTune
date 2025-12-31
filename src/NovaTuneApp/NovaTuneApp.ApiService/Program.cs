using KafkaFlow;
using KafkaFlow.Admin.Dashboard;
using KafkaFlow.Configuration;
using KafkaFlow.Serializer;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Messaging;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Handlers;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Redis/Garnet client via Aspire.
builder.AddRedisClient("cache");

// Register cache service.
builder.Services.AddSingleton<ICacheService, GarnetCacheService>();

// Add Redis health check.
builder.Services.AddHealthChecks()
    .AddRedis(
        builder.Configuration.GetConnectionString("cache") ?? "localhost:6379",
        name: "garnet",
        tags: ["ready"]);

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
                security.SecurityProtocol = SecurityProtocol.SaslSsl;
                security.SaslMechanism = SaslMechanism.ScramSha256;
                security.SaslUsername = builder.Configuration["Kafka:SaslUsername"];
                security.SaslPassword = builder.Configuration["Kafka:SaslPassword"];
            });
        }

        // Create topics if they don't exist
        cluster.CreateTopicIfNotExists($"{topicPrefix}-audio-events", 3, 1);
        cluster.CreateTopicIfNotExists($"{topicPrefix}-track-deletions", 3, 1);

        // Audio events producer
        cluster.AddProducer("audio-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-audio-events")
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        );

        // Track deletions producer
        cluster.AddProducer("deletion-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-track-deletions")
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        );

        // Audio events consumer
        cluster.AddConsumer(consumer => consumer
            .Topic($"{topicPrefix}-audio-events")
            .WithGroupId($"{topicPrefix}-audio-processor")
            .WithBufferSize(100)
            .WithWorkersCount(3)
            .AddMiddlewares(m => m
                .AddDeserializer<JsonCoreDeserializer>()
                .AddTypedHandlers(h => h.AddHandler<AudioUploadedHandler>())
            )
        );

        // Track deletions consumer
        cluster.AddConsumer(consumer => consumer
            .Topic($"{topicPrefix}-track-deletions")
            .WithGroupId($"{topicPrefix}-deletion-processor")
            .WithBufferSize(50)
            .WithWorkersCount(2)
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Optional: KafkaFlow dashboard for debugging
    app.UseKafkaFlowDashboard();
}

// Start KafkaFlow consumers.
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var kafkaBus = app.Services.CreateKafkaBus();
await kafkaBus.StartAsync(lifetime.ApplicationStopping);

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
