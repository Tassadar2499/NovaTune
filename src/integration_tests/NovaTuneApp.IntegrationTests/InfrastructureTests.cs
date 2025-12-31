using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using NovaTuneApp.Tests.Fixtures;
using StackExchange.Redis;

namespace NovaTuneApp.Tests;

/// <summary>
/// Migration verification tests for Redpanda and Garnet infrastructure.
/// </summary>
public class InfrastructureTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public InfrastructureTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Redpanda_CanPublishAndConsume()
    {
        // Arrange
        var topic = $"test-topic-{Guid.NewGuid():N}";
        var testMessage = new { Id = Guid.NewGuid(), Message = "Hello Redpanda!" };
        var messageJson = JsonSerializer.Serialize(testMessage);

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _fixture.KafkaBootstrapServers
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _fixture.KafkaBootstrapServers,
            GroupId = $"test-group-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        // Act - Produce message
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        var deliveryResult = await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = testMessage.Id.ToString(),
            Value = messageJson
        });

        // Assert - Delivery succeeded
        Assert.Equal(PersistenceStatus.Persisted, deliveryResult.Status);
        Assert.Equal(topic, deliveryResult.Topic);

        // Act - Consume message
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumeResult = consumer.Consume(cts.Token);

        // Assert - Consumed message matches
        Assert.NotNull(consumeResult);
        Assert.Equal(testMessage.Id.ToString(), consumeResult.Message.Key);
        Assert.Equal(messageJson, consumeResult.Message.Value);
    }

    [Fact]
    public async Task Garnet_CanSetAndGet()
    {
        // Arrange
        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.GarnetConnectionString);
        var db = redis.GetDatabase();
        var key = $"test:key:{Guid.NewGuid():N}";
        var testValue = new { Name = "TestItem", Value = 42 };
        var valueJson = JsonSerializer.Serialize(testValue);

        // Act - Set value
        var setResult = await db.StringSetAsync(key, valueJson);

        // Assert - Set succeeded
        Assert.True(setResult);

        // Act - Get value
        var retrievedValue = await db.StringGetAsync(key);

        // Assert - Retrieved value matches
        Assert.True(retrievedValue.HasValue);
        Assert.Equal(valueJson, retrievedValue.ToString());

        // Cleanup
        await db.KeyDeleteAsync(key);
    }

    [Fact]
    public async Task Garnet_TTLExpiration_Works()
    {
        // Arrange
        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.GarnetConnectionString);
        var db = redis.GetDatabase();
        var key = $"test:ttl:{Guid.NewGuid():N}";
        var testValue = "expiring-value";
        var ttl = TimeSpan.FromSeconds(2);

        // Act - Set value with TTL
        var setResult = await db.StringSetAsync(key, testValue, ttl);
        Assert.True(setResult);

        // Assert - Value exists immediately
        var existsBeforeExpiry = await db.KeyExistsAsync(key);
        Assert.True(existsBeforeExpiry);

        // Assert - TTL is set correctly
        var remainingTtl = await db.KeyTimeToLiveAsync(key);
        Assert.NotNull(remainingTtl);
        Assert.True(remainingTtl.Value.TotalSeconds > 0);
        Assert.True(remainingTtl.Value.TotalSeconds <= 2);

        // Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert - Value no longer exists
        var existsAfterExpiry = await db.KeyExistsAsync(key);
        Assert.False(existsAfterExpiry);

        var valueAfterExpiry = await db.StringGetAsync(key);
        Assert.False(valueAfterExpiry.HasValue);
    }

    [Fact]
    public async Task Garnet_CanRemoveKey()
    {
        // Arrange
        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.GarnetConnectionString);
        var db = redis.GetDatabase();
        var key = $"test:remove:{Guid.NewGuid():N}";

        // Act - Set and then remove
        await db.StringSetAsync(key, "to-be-deleted");
        var existsBefore = await db.KeyExistsAsync(key);
        var deleteResult = await db.KeyDeleteAsync(key);
        var existsAfter = await db.KeyExistsAsync(key);

        // Assert
        Assert.True(existsBefore);
        Assert.True(deleteResult);
        Assert.False(existsAfter);
    }

    [Fact]
    public async Task Redpanda_MultiplePartitions_Work()
    {
        // Arrange
        var topic = $"test-partitioned-{Guid.NewGuid():N}";

        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = _fixture.KafkaBootstrapServers
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _fixture.KafkaBootstrapServers
        };

        // Act - Create topic with multiple partitions
        using var adminClient = new AdminClientBuilder(adminConfig).Build();
        await adminClient.CreateTopicsAsync(new[]
        {
            new TopicSpecification
            {
                Name = topic,
                NumPartitions = 3,
                ReplicationFactor = 1
            }
        });

        // Produce messages to different partitions
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        var tasks = Enumerable.Range(0, 9).Select(i =>
            producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = $"key-{i}",
                Value = $"message-{i}"
            })
        ).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All messages persisted
        Assert.All(results, r => Assert.Equal(PersistenceStatus.Persisted, r.Status));

        // Verify messages spread across partitions
        var partitions = results.Select(r => r.Partition.Value).Distinct().ToList();
        Assert.True(partitions.Count > 1, "Messages should be distributed across multiple partitions");
    }

    [Fact]
    public async Task Garnet_JsonSerialization_Works()
    {
        // Arrange - Mimics GarnetCacheService pattern
        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.GarnetConnectionString);
        var db = redis.GetDatabase();
        var key = $"test:json:{Guid.NewGuid():N}";
        var testObject = new TestCacheItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            CreatedAt = DateTimeOffset.UtcNow,
            Tags = new[] { "tag1", "tag2", "tag3" }
        };

        // Act - Serialize and store
        var json = JsonSerializer.Serialize(testObject);
        await db.StringSetAsync(key, json, TimeSpan.FromMinutes(5));

        // Act - Retrieve and deserialize
        var retrieved = await db.StringGetAsync(key);
        var deserialized = JsonSerializer.Deserialize<TestCacheItem>(retrieved!);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(testObject.Id, deserialized.Id);
        Assert.Equal(testObject.Name, deserialized.Name);
        Assert.Equal(testObject.Tags, deserialized.Tags);

        // Cleanup
        await db.KeyDeleteAsync(key);
    }

    private record TestCacheItem
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public string[] Tags { get; init; } = Array.Empty<string>();
    }
}
