namespace NovaTuneApp.ApiService.Infrastructure.Caching;

/// <summary>
/// Abstraction for distributed caching operations.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Sets a value in the cache with the specified TTL.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Removes all keys matching a pattern from the cache.
    /// Pattern supports wildcard (*) for matching multiple keys.
    /// </summary>
    /// <param name="pattern">Key pattern (e.g., "stream:userId:*").</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
