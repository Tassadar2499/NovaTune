# Phase 3: Aspire AppHost Configuration

## 3.1 Update AppHost.cs
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Cache (Garnet via Redis protocol)
var cache = builder.AddRedis("cache")
    .WithDataVolume("garnet-data");

// Messaging (Redpanda via Kafka protocol)
var messaging = builder.AddKafka("messaging")
    .WithDataVolume("redpanda-data");

var apiService = builder.AddProject<Projects.NovaTuneApp_ApiService>("apiservice")
    .WithReference(cache)
    .WithReference(messaging)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.NovaTuneApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithReference(cache)
    .WaitFor(apiService);

builder.Build().Run();
```

## 3.2 Update ServiceDefaults
Add Redis/Garnet configuration helper in `Extensions.cs`:
```csharp
public static IHostApplicationBuilder AddDefaultCaching(this IHostApplicationBuilder builder)
{
    builder.AddRedisClient("cache");
    return builder;
}

public static IHostApplicationBuilder AddDefaultMessaging(this IHostApplicationBuilder builder)
{
    // Kafka client configuration for Redpanda
    builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
    return builder;
}
```

---

## Verification
- [ ] `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost` starts
- [ ] Aspire dashboard shows cache and messaging resources

**Exit Criteria:** Aspire orchestration starts with Redpanda and Garnet resources.
