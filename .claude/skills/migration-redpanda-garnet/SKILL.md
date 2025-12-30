# Redpanda & Garnet Migration Skill

Guide for the Kafka/RabbitMQ to Redpanda and NCache to Garnet migration.

## Migration Overview

- **Redpanda** replaces Apache Kafka + RabbitMQ (Kafka-compatible API)
- **Garnet** replaces NCache (Redis-compatible API)
- **KafkaFlow** replaces raw Confluent.Kafka client

## Implementation Plan Reference

Full plan: `src/task/main.md`

## Phase Checklist

### Phase 1: Infrastructure (Docker Compose)
- [ ] Update docker-compose.yml with Redpanda service
- [ ] Update docker-compose.yml with Garnet service
- [ ] Update volumes (redpanda-data, garnet-data)
- [ ] Update docker-compose.override.yml (Redpanda Console)
- [ ] Update .env.example

### Phase 2: NuGet Packages
Remove:
```xml
<PackageReference Include="Alachisoft.NCache.SDK" />
<PackageReference Include="RabbitMQ.Client" />
```

Add:
```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.16" />
<PackageReference Include="KafkaFlow" Version="3.0.10" />
<PackageReference Include="KafkaFlow.Microsoft.DependencyInjection" Version="3.0.10" />
<PackageReference Include="KafkaFlow.Serializer.JsonCore" Version="3.0.10" />
```

### Phase 3: Aspire AppHost
```csharp
var cache = builder.AddRedis("cache").WithDataVolume("garnet-data");
var messaging = builder.AddKafka("messaging").WithDataVolume("redpanda-data");
```

### Phase 4: Caching (Garnet)
- Implement `GarnetCacheService` using `IConnectionMultiplexer`
- Replace `NCacheException` with `RedisException`
- Register: `builder.AddRedisClient("cache")`

### Phase 5: Messaging (KafkaFlow)
- Define message contracts (records)
- Configure KafkaFlow producers/consumers
- Implement `IMessageHandler<T>` handlers
- Remove RabbitMQ code

### Phase 6: Testing
- Use Testcontainers for Redpanda and Garnet
- Update test fixtures

### Phase 7: Documentation
- Update all docs in `doc/` directory
- Create ADR-0006 (Redpanda) and ADR-0007 (Garnet)

## Key Patterns

### Garnet Cache Service
```csharp
public class GarnetCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }
}
```

### KafkaFlow Message Handler
```csharp
public class AudioUploadedHandler : IMessageHandler<AudioUploadedEvent>
{
    public async Task Handle(IMessageContext context, AudioUploadedEvent message)
    {
        // Process message
    }
}
```

### KafkaFlow Producer
```csharp
await _producer.ProduceAsync(
    messageKey: evt.TrackId.ToString(),
    messageValue: evt
);
```

## Topics Configuration

| Topic | Partitions | Retention | Cleanup |
|-------|------------|-----------|---------|
| `{env}-audio-events` | 3 | 7 days | Delete |
| `{env}-track-deletions` | 3 | 30 days | Compact |

## Verification Commands

```bash
# Check Redpanda
docker exec novatune-redpanda rpk cluster info
docker exec novatune-redpanda rpk topic list

# Check Garnet
docker exec novatune-garnet redis-cli ping

# Verify no legacy references
grep -r "NCache" --include="*.cs" src/
grep -r "RabbitMQ" --include="*.cs" src/
grep -r "Alachisoft" --include="*.csproj" src/
```
