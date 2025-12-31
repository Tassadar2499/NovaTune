using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.Redis;

namespace NovaTuneApp.Tests.Fixtures;

/// <summary>
/// Test fixture that provides Redpanda and Garnet containers for integration tests.
/// </summary>
public class InfrastructureFixture : IAsyncLifetime
{
    private const int RedpandaKafkaPort = 9092;
    private const int GarnetPort = 6379;

    /// <summary>
    /// Redpanda container (Kafka-compatible).
    /// </summary>
    public IContainer Redpanda { get; private set; } = null!;

    /// <summary>
    /// Garnet container (Redis/RESP-compatible).
    /// </summary>
    public RedisContainer Garnet { get; private set; } = null!;

    /// <summary>
    /// Gets the Kafka bootstrap servers connection string.
    /// </summary>
    public string KafkaBootstrapServers => $"{Redpanda.Hostname}:{Redpanda.GetMappedPublicPort(RedpandaKafkaPort)}";

    /// <summary>
    /// Gets the Redis/Garnet connection string.
    /// </summary>
    public string GarnetConnectionString => Garnet.GetConnectionString();

    public async Task InitializeAsync()
    {
        Redpanda = new ContainerBuilder()
            .WithImage("redpandadata/redpanda:v24.2.4")
            .WithPortBinding(RedpandaKafkaPort, true)
            .WithCommand(
                "redpanda", "start",
                "--overprovisioned",
                "--smp", "1",
                "--memory", "512M",
                "--reserve-memory", "0M",
                "--node-id", "0",
                "--check=false",
                "--kafka-addr", "PLAINTEXT://0.0.0.0:9092",
                "--advertise-kafka-addr", "PLAINTEXT://localhost:9092"
            )
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Started Kafka API server"))
            .Build();

        Garnet = new RedisBuilder()
            .WithImage("ghcr.io/microsoft/garnet:1.0.44")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(GarnetPort))
            .Build();

        await Task.WhenAll(
            Redpanda.StartAsync(),
            Garnet.StartAsync()
        );

        // Get the actual mapped port for Redpanda and reconfigure advertised listeners
        var mappedPort = Redpanda.GetMappedPublicPort(RedpandaKafkaPort);
        await Redpanda.ExecAsync(new[]
        {
            "rpk", "redpanda", "config", "set",
            "--format", "json",
            "redpanda.advertise_kafka_api",
            $"[{{\"address\": \"{Redpanda.Hostname}\", \"port\": {mappedPort}}}]"
        });

        // Restart the Kafka API to apply the new advertised address
        // Note: This is needed because we can't know the mapped port before starting
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            Redpanda.DisposeAsync().AsTask(),
            Garnet.DisposeAsync().AsTask()
        );
    }
}
