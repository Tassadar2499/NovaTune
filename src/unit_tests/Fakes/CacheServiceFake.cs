using NovaTuneApp.ApiService.Infrastructure.Caching;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Fake implementation of ICacheService for unit tests.
/// </summary>
public class CacheServiceFake : ICacheService
{
    public Dictionary<string, (object Value, DateTimeOffset ExpiresAt)> Cache { get; } = new();
    public List<string> RemovedKeys { get; } = [];
    public List<string> RemovedPatterns { get; } = [];

    public Func<string, CancellationToken, Task<object?>>? OnGetAsync { get; set; }
    public Func<string, object, TimeSpan, CancellationToken, Task>? OnSetAsync { get; set; }
    public Action? OnSetAsyncAction { get; set; }
    public bool ThrowOnSet { get; set; }
    public bool ThrowOnGet { get; set; }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (ThrowOnGet)
        {
            throw new InvalidOperationException("Cache get failed");
        }

        if (OnGetAsync is not null)
        {
            var result = OnGetAsync(key, ct).GetAwaiter().GetResult();
            return Task.FromResult((T?)result);
        }

        if (Cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return Task.FromResult((T?)entry.Value);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        if (ThrowOnSet)
        {
            throw new InvalidOperationException("Cache set failed");
        }

        if (OnSetAsync is not null)
        {
            return OnSetAsync(key, value!, ttl, ct);
        }

        OnSetAsyncAction?.Invoke();
        Cache[key] = (value!, DateTimeOffset.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        RemovedKeys.Add(key);
        Cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var exists = Cache.ContainsKey(key) && Cache[key].ExpiresAt > DateTimeOffset.UtcNow;
        return Task.FromResult(exists);
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        RemovedPatterns.Add(pattern);

        // Simple pattern matching for tests (supports suffix wildcard only)
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            var keysToRemove = Cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                Cache.Remove(key);
            }
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        Cache.Clear();
        RemovedKeys.Clear();
        RemovedPatterns.Clear();
    }
}
