using DotNet.Testcontainers.Builders;
using Testcontainers.Redis;
using Testcontainers.Redpanda;

namespace NovaTuneApp.Tests.Fixtures;

/// <summary>
/// Test fixture that provides Redpanda and Garnet containers for integration tests.
/// </summary>
public class InfrastructureFixture : IAsyncLifetime
{
    private const int GarnetPort = 6379;

    /// <summary>
    /// Redpanda container (Kafka-compatible).
    /// </summary>
    public RedpandaContainer Redpanda { get; private set; } = null!;

    /// <summary>
    /// Garnet container (Redis/RESP-compatible).
    /// </summary>
    public RedisContainer Garnet { get; private set; } = null!;

    /// <summary>
    /// Gets the Kafka bootstrap servers connection string.
    /// </summary>
    public string KafkaBootstrapServers => Redpanda.GetBootstrapAddress();

    /// <summary>
    /// Gets the Redis/Garnet connection string.
    /// </summary>
    public string GarnetConnectionString => Garnet.GetConnectionString();

    public async Task InitializeAsync()
    {
        Redpanda = new RedpandaBuilder()
            .WithImage("redpandadata/redpanda:v24.2.4")
            .Build();

        Garnet = new RedisBuilder()
            .WithImage("ghcr.io/microsoft/garnet:1.0.44")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(GarnetPort))
            .Build();

        await Task.WhenAll(
            Redpanda.StartAsync(),
            Garnet.StartAsync()
        );
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            Redpanda.DisposeAsync().AsTask(),
            Garnet.DisposeAsync().AsTask()
        );
    }
}
