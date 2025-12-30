# Phase 6: Testing Updates

## 6.1 Update Testcontainers Configuration
```csharp
// Tests/Fixtures/InfrastructureFixture.cs
public class InfrastructureFixture : IAsyncLifetime
{
    public RedpandaContainer Redpanda { get; private set; } = null!;
    public GarnetContainer Garnet { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Redpanda = new RedpandaBuilder()
            .WithImage("redpandadata/redpanda:v24.2.4")
            .Build();

        // Garnet uses Redis container (RESP compatible)
        Garnet = new RedisBuilder()
            .WithImage("ghcr.io/microsoft/garnet:1.0.44")
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
```

## 6.2 Update Test Configuration
```json
// appsettings.Test.json
{
  "Kafka": {
    "BootstrapServers": "{{REDPANDA_HOST}}:{{REDPANDA_PORT}}",
    "TopicPrefix": "test"
  },
  "ConnectionStrings": {
    "cache": "{{GARNET_HOST}}:{{GARNET_PORT}}"
  }
}
```

## 6.3 Add Migration Verification Tests
```csharp
[Fact]
public async Task Redpanda_CanPublishAndConsume()
{
    // Verify Kafka protocol compatibility
}

[Fact]
public async Task Garnet_CanSetAndGet()
{
    // Verify Redis protocol compatibility
}

[Fact]
public async Task Garnet_TTLExpiration_Works()
{
    // Verify TTL behavior matches NCache expectations
}
```

---

## Verification
- [ ] `dotnet test` passes all tests
- [ ] Integration tests use Testcontainers correctly
- [ ] No NCache or RabbitMQ references in tests

**Exit Criteria:** All tests pass with Redpanda and Garnet test infrastructure.
