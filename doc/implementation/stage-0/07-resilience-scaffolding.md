# Resilience Scaffolding (`NF-1.4`)

Establish baseline resilience patterns (detailed tuning in later stages):

## Tasks

- [x] Add Polly or `Microsoft.Extensions.Http.Resilience` for HTTP clients.
- [x] Configure default timeouts per dependency class (cache: 500ms, DB: 5s, storage: 10s).
- [x] Scaffold circuit breaker policies (can be refined per-endpoint later).
- [x] Add bulkhead policies for concurrent dependency access.

## Implementation

### Package Dependencies

Added `Microsoft.Extensions.Resilience` (v10.0.0) to `NovaTuneApp.ServiceDefaults.csproj`, which provides Polly v8 integration with the Microsoft.Extensions ecosystem.

### Resilience Pipelines

Created `ResilienceExtensions.cs` in ServiceDefaults with three named resilience pipelines:

| Pipeline | Name | Timeout | Concurrency | Queue |
|----------|------|---------|-------------|-------|
| Cache | `cache-resilience` | 500ms | 100 | 50 |
| Database | `database-resilience` | 5s | 50 | 25 |
| Storage | `storage-resilience` | 10s | 20 | 10 |

Each pipeline includes (in order):
1. **Concurrency Limiter (Bulkhead)** - Limits concurrent operations to prevent resource exhaustion
2. **Circuit Breaker** - Fails fast when dependency is unhealthy (50% failure threshold, 30s break duration)
3. **Timeout** - Cancels operations that exceed the configured duration

### Usage

Pipelines are automatically registered when calling `AddServiceDefaults()`. Services can inject `ResiliencePipelineProvider<string>` and retrieve pipelines by name:

```csharp
public class MyService
{
    private readonly ResiliencePipeline _pipeline;

    public MyService(ResiliencePipelineProvider<string> provider)
    {
        _pipeline = provider.GetPipeline(ResilienceExtensions.CachePipeline);
    }

    public async Task<T> ExecuteWithResilience<T>(Func<Task<T>> operation)
    {
        return await _pipeline.ExecuteAsync(async _ => await operation());
    }
}
```

### Updated Services

- `GarnetCacheService` - Wrapped with cache resilience pipeline
- `TrackService` - Scaffolded with database resilience pipeline (stub implementation)
- `StorageService` - Scaffolded with storage resilience pipeline (stub implementation)
