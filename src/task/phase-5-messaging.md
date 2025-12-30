# Phase 5: Messaging Layer Migration with KafkaFlow

## 5.1 Define Message Contracts
```csharp
// Infrastructure/Messaging/Messages/AudioUploadedEvent.cs
public record AudioUploadedEvent
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid TrackId { get; init; }
    public required Guid UserId { get; init; }
    public required string ObjectKey { get; init; }
    public required string MimeType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

// Infrastructure/Messaging/Messages/TrackDeletedEvent.cs
public record TrackDeletedEvent
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid TrackId { get; init; }
    public required Guid UserId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
```

## 5.2 Configure KafkaFlow in Program.cs
```csharp
// Program.cs or ServiceRegistration.cs
var topicPrefix = builder.Configuration["Kafka:TopicPrefix"] ?? "dev";

builder.Services.AddKafka(kafka => kafka
    .UseMicrosoftLog()
    .AddCluster(cluster => cluster
        .WithBrokers(new[] { builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:19092" })
        .WithSecurityInformation(security =>
        {
            if (builder.Configuration.GetValue<bool>("Kafka:SaslEnabled"))
            {
                security.SecurityProtocol = Confluent.Kafka.SecurityProtocol.SaslSsl;
                security.SaslMechanism = Confluent.Kafka.SaslMechanism.ScramSha256;
                security.SaslUsername = builder.Configuration["Kafka:SaslUsername"];
                security.SaslPassword = builder.Configuration["Kafka:SaslPassword"];
            }
        })
        .CreateTopicIfNotExists($"{topicPrefix}-audio-events", 3, 1)
        .CreateTopicIfNotExists($"{topicPrefix}-track-deletions", 3, 1)
        .AddProducer("audio-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-audio-events")
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        )
        .AddProducer("deletion-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-track-deletions")
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        )
        .AddConsumer(consumer => consumer
            .Topic($"{topicPrefix}-audio-events")
            .WithGroupId($"{topicPrefix}-audio-processor")
            .WithBufferSize(100)
            .WithWorkersCount(3)
            .AddMiddlewares(m => m
                .AddDeserializer<JsonCoreDeserializer>()
                .AddTypedHandlers(h => h.AddHandler<AudioUploadedHandler>())
            )
        )
        .AddConsumer(consumer => consumer
            .Topic($"{topicPrefix}-track-deletions")
            .WithGroupId($"{topicPrefix}-deletion-processor")
            .WithBufferSize(50)
            .WithWorkersCount(2)
            .AddMiddlewares(m => m
                .AddDeserializer<JsonCoreDeserializer>()
                .AddTypedHandlers(h => h.AddHandler<TrackDeletedHandler>())
            )
        )
    )
);
```

## 5.3 Implement Message Handlers
```csharp
// Infrastructure/Messaging/Handlers/AudioUploadedHandler.cs
public class AudioUploadedHandler : IMessageHandler<AudioUploadedEvent>
{
    private readonly ILogger<AudioUploadedHandler> _logger;
    private readonly ITrackService _trackService;

    public AudioUploadedHandler(ILogger<AudioUploadedHandler> logger, ITrackService trackService)
    {
        _logger = logger;
        _trackService = trackService;
    }

    public async Task Handle(IMessageContext context, AudioUploadedEvent message)
    {
        _logger.LogInformation(
            "Processing audio upload for track {TrackId}, correlation: {CorrelationId}",
            message.TrackId, message.CorrelationId);

        await _trackService.ProcessUploadedTrackAsync(message.TrackId);

        _logger.LogInformation("Completed processing for track {TrackId}", message.TrackId);
    }
}

// Infrastructure/Messaging/Handlers/TrackDeletedHandler.cs
public class TrackDeletedHandler : IMessageHandler<TrackDeletedEvent>
{
    private readonly ILogger<TrackDeletedHandler> _logger;
    private readonly IStorageService _storageService;
    private readonly ICacheService _cacheService;

    public TrackDeletedHandler(
        ILogger<TrackDeletedHandler> logger,
        IStorageService storageService,
        ICacheService cacheService)
    {
        _logger = logger;
        _storageService = storageService;
        _cacheService = cacheService;
    }

    public async Task Handle(IMessageContext context, TrackDeletedEvent message)
    {
        _logger.LogInformation("Processing deletion for track {TrackId}", message.TrackId);

        // Invalidate cache
        await _cacheService.RemoveAsync($"presigned:{message.UserId}:{message.TrackId}");

        // Schedule storage cleanup (with grace period)
        await _storageService.ScheduleDeletionAsync(message.TrackId, TimeSpan.FromHours(24));

        _logger.LogInformation("Completed deletion processing for track {TrackId}", message.TrackId);
    }
}
```

## 5.4 Create Producer Wrapper Service
```csharp
// Infrastructure/Messaging/MessageProducerService.cs
public interface IMessageProducerService
{
    Task PublishAudioUploadedAsync(AudioUploadedEvent evt, CancellationToken ct = default);
    Task PublishTrackDeletedAsync(TrackDeletedEvent evt, CancellationToken ct = default);
}

public class MessageProducerService : IMessageProducerService
{
    private readonly IMessageProducer _audioProducer;
    private readonly IMessageProducer _deletionProducer;

    public MessageProducerService(IProducerAccessor producerAccessor)
    {
        _audioProducer = producerAccessor.GetProducer("audio-producer");
        _deletionProducer = producerAccessor.GetProducer("deletion-producer");
    }

    public async Task PublishAudioUploadedAsync(AudioUploadedEvent evt, CancellationToken ct = default)
    {
        await _audioProducer.ProduceAsync(
            messageKey: evt.TrackId.ToString(),
            messageValue: evt,
            headers: new MessageHeaders { { "schema-version", "1"u8.ToArray() } }
        );
    }

    public async Task PublishTrackDeletedAsync(TrackDeletedEvent evt, CancellationToken ct = default)
    {
        await _deletionProducer.ProduceAsync(
            messageKey: evt.TrackId.ToString(),
            messageValue: evt
        );
    }
}
```

## 5.5 Register Services
```csharp
// Program.cs
builder.Services.AddSingleton<IMessageProducerService, MessageProducerService>();
builder.Services.AddTransient<AudioUploadedHandler>();
builder.Services.AddTransient<TrackDeletedHandler>();
```

## 5.6 Add KafkaFlow Hosted Service
```csharp
// Start KafkaFlow consumers
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var kafkaBus = app.Services.CreateKafkaBus();
await kafkaBus.StartAsync(lifetime.ApplicationStopping);
```

## 5.7 Remove RabbitMQ Code
- Delete any `RabbitMQ` related files in `Infrastructure/`
- Remove RabbitMQ-based background job implementations
- Migrate waveform-jobs queue to Redpanda topic if needed

## 5.8 Update appsettings*.json
```json
{
  "Kafka": {
    "BootstrapServers": "localhost:19092",
    "TopicPrefix": "dev",
    "SaslEnabled": false,
    "SaslUsername": "",
    "SaslPassword": ""
  }
}
```

## 5.9 Optional: Add KafkaFlow Admin Dashboard
```csharp
// For debugging and monitoring in development
app.UseKafkaFlowDashboard();
```
Access at `/kafkaflow` to view consumer lag, pause/resume consumers, and inspect messages.

---

## Verification
- [ ] Producer can publish to `dev-audio-events` topic
- [ ] Consumer handlers receive and process messages
- [ ] Messages appear in Redpanda Console
- [ ] KafkaFlow dashboard shows consumer status (optional)
- [ ] Environment prefixing works correctly
- [ ] Handlers have proper DI injection

**Exit Criteria:** All messaging operations work with KafkaFlow + Redpanda, no raw Confluent.Kafka or RabbitMQ code remains.
