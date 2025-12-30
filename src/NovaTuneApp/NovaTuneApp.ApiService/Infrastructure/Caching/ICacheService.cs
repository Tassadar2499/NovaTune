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
}
