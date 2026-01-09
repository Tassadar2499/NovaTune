using System.Text.Json;
using NovaTuneApp.ApiService.Infrastructure.Observability;

namespace NovaTuneApp.ApiService.Infrastructure.Caching;

/// <summary>
/// Cache service wrapper that encrypts values at rest (Req 10.3, NF-3.2).
/// Wraps an underlying ICacheService and encrypts/decrypts values using ICacheEncryptionProvider.
/// </summary>
public class EncryptedCacheService : IEncryptedCacheService
{
    private readonly ICacheService _innerCache;
    private readonly ICacheEncryptionProvider _encryption;
    private readonly ILogger<EncryptedCacheService> _logger;

    public EncryptedCacheService(
        ICacheService innerCache,
        ICacheEncryptionProvider encryption,
        ILogger<EncryptedCacheService> logger)
    {
        _innerCache = innerCache;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var wrapper = await _innerCache.GetAsync<EncryptedCacheEntry>(key, ct);
            if (wrapper is null)
            {
                NovaTuneMetrics.RecordCacheAccess("encrypted_stream_url", isHit: false);
                return default;
            }

            NovaTuneMetrics.RecordCacheAccess("encrypted_stream_url", isHit: true);

            var json = await _encryption.DecryptAsync(wrapper.Ciphertext, wrapper.KeyVersion, ct);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve encrypted cache entry for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var encrypted = await _encryption.EncryptAsync(json, ct);
            var wrapper = new EncryptedCacheEntry(encrypted, _encryption.CurrentKeyVersion);

            await _innerCache.SetAsync(key, wrapper, ttl, ct);

            _logger.LogDebug("Cached encrypted value for key {Key} with TTL {Ttl}", key, ttl);
        }
        catch (Exception ex)
        {
            // Cache write failures should not fail the request (fail-open per 09-resilience.md)
            _logger.LogWarning(ex, "Failed to set encrypted cache entry for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _innerCache.RemoveAsync(key, ct);
            _logger.LogDebug("Removed encrypted cache entry for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove encrypted cache entry for key {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        try
        {
            await _innerCache.RemoveByPatternAsync(pattern, ct);
            _logger.LogDebug("Removed encrypted cache entries matching pattern {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove encrypted cache entries matching pattern {Pattern}", pattern);
        }
    }
}

/// <summary>
/// Wrapper for encrypted cache entries with key version tracking.
/// </summary>
internal record EncryptedCacheEntry(byte[] Ciphertext, string KeyVersion);
