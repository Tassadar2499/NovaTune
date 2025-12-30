# Phase 4: Caching Layer Migration (NCache → Garnet)

## 4.1 Update Cache Interfaces
If `ICacheService` or similar exists in `Infrastructure/`:
- Keep interface unchanged
- Replace NCache implementation with StackExchange.Redis

## 4.2 Implement Garnet-backed Cache Service
```csharp
// Infrastructure/Caching/GarnetCacheService.cs
public class GarnetCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;

    public GarnetCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(value);
        await db.StringSetAsync(key, json, ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
    }
}
```

## 4.3 Update DI Registration
In `Program.cs` or service registration:
```csharp
// Remove NCache registrations
// Add:
builder.AddRedisClient("cache");
builder.Services.AddSingleton<ICacheService, GarnetCacheService>();
```

## 4.4 Update appsettings*.json
```json
{
  "ConnectionStrings": {
    "cache": "localhost:6379"
  },
  "Cache": {
    "PresignedUrlTtlMinutes": 8,
    "SessionTtlMinutes": 30,
    "KeyPrefix": "novatune:dev:"
  }
}
```

## 4.5 Update Health Checks
Replace NCache health check with Redis:
```csharp
builder.Services.AddHealthChecks()
    .AddRedis(builder.Configuration.GetConnectionString("cache")!, name: "garnet");
```

---

## Verification
- [ ] Cache service can SET and GET values
- [ ] TTLs are respected (key expires after TTL)
- [ ] Health check endpoint reports cache as healthy
- [ ] Existing cache key patterns work (presigned URLs, sessions)

**Exit Criteria:** All cache operations work with Garnet, health checks pass.
