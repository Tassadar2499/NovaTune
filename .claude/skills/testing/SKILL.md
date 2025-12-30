# Testing Skill

Write and run tests for NovaTune.

## Test Projects

| Project | Type | Location |
|---------|------|----------|
| `NovaTune.UnitTests` | Unit tests | `src/unit_tests/` |
| `NovaTuneApp.Tests` | Integration tests | `src/NovaTuneApp/NovaTuneApp.Tests/` |

## Naming Conventions

- Unit tests: `{TargetClass}.Tests.cs`
- Integration tests: `{Feature}.IntegrationTests.cs`
- Test methods: `{Method}_{Scenario}_{ExpectedResult}`

## Running Tests

```bash
# All tests
dotnet test

# With coverage
dotnet test /p:CollectCoverage=true

# Specific project
dotnet test src/NovaTuneApp/NovaTuneApp.Tests
dotnet test src/unit_tests

# Filter tests
dotnet test --filter "FullyQualifiedName~TrackService"
dotnet test --filter "Category=Integration"
```

## Unit Test Pattern

```csharp
public class TrackServiceTests
{
    private readonly Mock<IDocumentSession> _sessionMock;
    private readonly TrackService _sut;

    public TrackServiceTests()
    {
        _sessionMock = new Mock<IDocumentSession>();
        _sut = new TrackService(_sessionMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsTrack()
    {
        // Arrange
        var request = new CreateTrackRequest("Test Track", null);

        // Act
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Track", result.Title);
        _sessionMock.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

## Integration Test Pattern (Aspire)

```csharp
public class TrackEndpointsIntegrationTests : IClassFixture<DistributedApplicationFixture>
{
    private readonly DistributedApplicationFixture _fixture;

    public TrackEndpointsIntegrationTests(DistributedApplicationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateTrack_ValidRequest_Returns201()
    {
        // Arrange
        var client = _fixture.CreateClient("apiservice");
        var request = new CreateTrackRequest("Test", null);

        // Act
        var response = await client.PostAsJsonAsync("/api/tracks", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

## Testcontainers Pattern

```csharp
public class InfrastructureFixture : IAsyncLifetime
{
    public RedpandaContainer Redpanda { get; private set; } = null!;
    public RedisContainer Garnet { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Redpanda = new RedpandaBuilder()
            .WithImage("redpandadata/redpanda:v24.2.4")
            .Build();

        Garnet = new RedisBuilder()
            .WithImage("ghcr.io/microsoft/garnet:1.0.44")
            .Build();

        await Task.WhenAll(
            Redpanda.StartAsync(),
            Garnet.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            Redpanda.DisposeAsync().AsTask(),
            Garnet.DisposeAsync().AsTask());
    }
}
```

## Test Configuration

File: `appsettings.Test.json`

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:19092",
    "TopicPrefix": "test"
  },
  "ConnectionStrings": {
    "cache": "localhost:6379"
  }
}
```

## Coverage Target

- Services: >= 80% line coverage
- Auth middleware: >= 80% line coverage
- Endpoints: Focus on integration tests

## Common Assertions

```csharp
// xUnit
Assert.Equal(expected, actual);
Assert.NotNull(result);
Assert.True(condition);
Assert.Throws<InvalidOperationException>(() => action());
await Assert.ThrowsAsync<NotFoundException>(() => asyncAction());

// Collections
Assert.Empty(collection);
Assert.Single(collection);
Assert.Contains(item, collection);
```
