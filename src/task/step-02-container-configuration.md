# Step 2: Container Configuration

## Objective

Ensure the AppHost provides all necessary infrastructure for tests and configure test-specific settings.

## Existing AppHost Configuration

The AppHost already configures all necessary infrastructure:

```csharp
// Cache (Garnet via Redis protocol)
var cache = builder.AddRedis("cache")
    .WithDataVolume("garnet-data")
    .WithPersistence(TimeSpan.FromSeconds(10), 5);

// Messaging (Redpanda via Kafka protocol)
var messaging = builder.AddKafka("messaging")
    .WithDataVolume("redpanda-data");

// Database (RavenDB - system of record)
var ravenServer = builder.AddRavenDB("ravendb")
    .WithDataVolume("ravendb-data");
var database = ravenServer.AddDatabase("novatune");
```

## Test Requirements

### Per-Test Database Isolation

Options:
1. **Clean data between tests** (Recommended) - Use `ClearDataAsync()` in `InitializeAsync`
2. **Unique database per test class** - More complex, requires AppHost modification
3. **Unique database per test** - Slowest, most isolated

### Wait Strategies

Aspire.Hosting.Testing handles container readiness automatically via:
- Health checks defined in AppHost
- `WaitFor()` dependencies between resources

### Data Volumes in Tests

For tests, data volumes may cause state persistence between runs. Options:
- Use `WithDataVolume()` with unique names per test run
- Skip data volumes in test configuration
- Clean data explicitly in test setup

## Implementation Notes

The `DistributedApplicationTestingBuilder` will:
1. Start all containers defined in AppHost
2. Wait for health checks to pass
3. Provide connection strings for resources

No AppHost modifications needed - test isolation handled via data cleanup.

## Acceptance Criteria

- [ ] All containers start successfully during test initialization
- [ ] Health checks pass before tests run
- [ ] Connection strings are accessible for RavenDB, Redis, Kafka
