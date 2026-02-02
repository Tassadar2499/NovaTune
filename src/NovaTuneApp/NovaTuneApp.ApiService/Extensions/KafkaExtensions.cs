using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Configuration;
using KafkaFlow.Serializer;
using NovaTuneApp.ApiService.Infrastructure.Messaging;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Handlers;
using NovaTuneApp.ApiService.Services;

namespace NovaTuneApp.ApiService.Extensions;

/// <summary>
/// Extension methods for configuring Kafka/Redpanda messaging.
/// </summary>
public static class KafkaExtensions
{
    /// <summary>
    /// Adds NovaTune Kafka messaging services including producers, consumers, and handlers.
    /// </summary>
    public static IHostApplicationBuilder AddNovaTuneMessaging(this IHostApplicationBuilder builder)
    {
        var topicPrefix = builder.Configuration["Kafka:TopicPrefix"] ?? "dev";
        var bootstrapServers = builder.Configuration.GetConnectionString("messaging")
            ?? builder.Configuration["Kafka:BootstrapServers"]
            ?? "localhost:9092";

        builder.Services.AddKafka(kafka => kafka
            .UseMicrosoftLog()
            .AddCluster(cluster =>
            {
                cluster.WithBrokers(new[] { bootstrapServers });

                ConfigureSaslIfEnabled(builder.Configuration, cluster);
                ConfigureProducers(cluster, topicPrefix);
                ConfigureConsumers(cluster, topicPrefix);
            })
        );

        // Register messaging services
        builder.Services.AddSingleton<IMessageProducerService, MessageProducerService>();
        builder.Services.AddTransient<AudioUploadedHandler>();
        builder.Services.AddTransient<TrackDeletedHandler>();
        builder.Services.AddTransient<TracingMiddleware>();

        // Register KafkaFlow as a hosted service for background startup
        builder.Services.AddHostedService<KafkaFlowHostedService>();

        return builder;
    }

    private static void ConfigureSaslIfEnabled(IConfiguration configuration, IClusterConfigurationBuilder cluster)
    {
        if (!configuration.GetValue<bool>("Kafka:SaslEnabled"))
            return;

        cluster.WithSecurityInformation(security =>
        {
            security.SecurityProtocol = KafkaFlow.Configuration.SecurityProtocol.SaslSsl;
            security.SaslMechanism = KafkaFlow.Configuration.SaslMechanism.ScramSha256;
            security.SaslUsername = configuration["Kafka:SaslUsername"];
            security.SaslPassword = configuration["Kafka:SaslPassword"];
        });
    }

    private static void ConfigureProducers(IClusterConfigurationBuilder cluster, string topicPrefix)
    {
        var producerConfig = new ProducerConfig
        {
            MessageTimeoutMs = 60000,
            SocketTimeoutMs = 30000,
            RetryBackoffMs = 1000
        };

        // Audio events producer
        cluster.AddProducer("audio-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-audio-events")
            .WithProducerConfig(producerConfig)
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        );

        // Track deletions producer
        cluster.AddProducer("deletion-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-track-deletions")
            .WithProducerConfig(producerConfig)
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        );

        // Telemetry events producer (Stage 7)
        cluster.AddProducer("telemetry-producer", producer => producer
            .DefaultTopic($"{topicPrefix}-telemetry-events")
            .WithProducerConfig(producerConfig)
            .AddMiddlewares(m => m.AddSerializer<JsonCoreSerializer>())
        );
    }

    private static void ConfigureConsumers(IClusterConfigurationBuilder cluster, string topicPrefix)
    {
        var consumerConfig = new ConsumerConfig
        {
            SessionTimeoutMs = 45000,
            SocketTimeoutMs = 30000,
            ReconnectBackoffMs = 1000
        };

        // Audio events consumer
        cluster.AddConsumer(consumer => consumer
            .Topic($"{topicPrefix}-audio-events")
            .WithGroupId($"{topicPrefix}-audio-processor")
            .WithBufferSize(100)
            .WithWorkersCount(3)
            .WithConsumerConfig(consumerConfig)
            .AddMiddlewares(m => m
                .AddDeserializer<JsonCoreDeserializer>()
                .Add<TracingMiddleware>()
                .AddTypedHandlers(h => h.AddHandler<AudioUploadedHandler>())
            )
        );

        // Track deletions consumer
        cluster.AddConsumer(consumer => consumer
            .Topic($"{topicPrefix}-track-deletions")
            .WithGroupId($"{topicPrefix}-deletion-processor")
            .WithBufferSize(50)
            .WithWorkersCount(2)
            .WithConsumerConfig(consumerConfig)
            .AddMiddlewares(m => m
                .AddDeserializer<JsonCoreDeserializer>()
                .Add<TracingMiddleware>()
                .AddTypedHandlers(h => h.AddHandler<TrackDeletedHandler>())
            )
        );
    }
}
