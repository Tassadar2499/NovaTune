namespace NovaTuneApp.ApiService.Infrastructure.Caching;

/// <summary>
/// Cache service that encrypts sensitive values at rest (Req 10.3, NF-3.2).
/// Used for presigned URLs per Req 10.3.
/// </summary>
public interface IEncryptedCacheService
{
    /// <summary>
    /// Gets a value from the encrypted cache.
    /// </summary>
    /// <typeparam name="T">Type of the cached value.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached value or null if not found.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Sets a value in the encrypted cache.
    /// </summary>
    /// <typeparam name="T">Type of the value to cache.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="ttl">Time-to-live for the cache entry.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Removes all values matching a pattern from the cache.
    /// Pattern supports wildcard (*) for matching multiple keys.
    /// </summary>
    /// <param name="pattern">Key pattern (e.g., "stream:userId:*").</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
