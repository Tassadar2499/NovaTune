using NovaTuneApp.ApiService.Infrastructure.Caching;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Fake implementation of IEncryptedCacheService for unit tests.
/// </summary>
public class EncryptedCacheServiceFake : IEncryptedCacheService
{
    public Dictionary<string, (object Value, DateTimeOffset ExpiresAt)> Cache { get; } = new();
    public List<string> RemovedKeys { get; } = [];
    public List<string> RemovedPatterns { get; } = [];

    public Func<string, CancellationToken, Task<object?>>? OnGetAsync { get; set; }
    public Func<string, object, TimeSpan, CancellationToken, Task>? OnSetAsync { get; set; }
    public Func<string, CancellationToken, Task>? OnRemoveAsync { get; set; }
    public Func<string, CancellationToken, Task>? OnRemoveByPatternAsync { get; set; }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (OnGetAsync is not null)
        {
            var result = OnGetAsync(key, ct).GetAwaiter().GetResult();
            return Task.FromResult((T?)result);
        }

        if (Cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return Task.FromResult((T?)entry.Value);
        }

        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        if (OnSetAsync is not null)
        {
            return OnSetAsync(key, value, ttl, ct);
        }

        Cache[key] = (value, DateTimeOffset.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (OnRemoveAsync is not null)
        {
            return OnRemoveAsync(key, ct);
        }

        RemovedKeys.Add(key);
        Cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        if (OnRemoveByPatternAsync is not null)
        {
            return OnRemoveByPatternAsync(pattern, ct);
        }

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
