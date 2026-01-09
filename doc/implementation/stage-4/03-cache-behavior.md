# 3. Cache Behavior (Req 10.2, Req 10.3, NF-3.3)

## Cache Key Design

| Component | Format | Example |
|-----------|--------|---------|
| Prefix | `stream` | `stream` |
| User ID | ULID | `01HXK...` |
| Track ID | ULID | `01HYZ...` |
| Full Key | `stream:{userId}:{trackId}` | `stream:01HXK...:01HYZ...` |

## Cached Value Schema

```csharp
internal record CachedStreamUrl(
    string Url,          // Encrypted presigned URL
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes);
```

## TTL Strategy (NF-3.3)

| Environment | Presign Expiry | Cache TTL | Buffer |
|-------------|----------------|-----------|--------|
| `dev`/`staging` | 2 minutes | 90 seconds | 30s |
| `prod` | 60-120 seconds | 30-90 seconds | 30s |

The cache TTL is always **30 seconds shorter** than the presign expiry to prevent serving near-expired URLs.

## Encryption at Rest (Req 10.3, NF-3.2)

### Interface: `IEncryptedCacheService`

```csharp
namespace NovaTuneApp.ApiService.Infrastructure.Caching;

/// <summary>
/// Cache service that encrypts sensitive values at rest.
/// Used for presigned URLs per Req 10.3.
/// </summary>
public interface IEncryptedCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
```

### Encryption Implementation

```csharp
public class EncryptedCacheService : IEncryptedCacheService
{
    private readonly ICacheService _innerCache;
    private readonly ICacheEncryptionProvider _encryption;

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        var encrypted = await _encryption.EncryptAsync(json, ct);
        var wrapper = new EncryptedCacheEntry(encrypted, _encryption.CurrentKeyVersion);
        await _innerCache.SetAsync(key, wrapper, ttl, ct);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var wrapper = await _innerCache.GetAsync<EncryptedCacheEntry>(key, ct);
        if (wrapper is null) return default;

        var json = await _encryption.DecryptAsync(wrapper.Ciphertext, wrapper.KeyVersion, ct);
        return JsonSerializer.Deserialize<T>(json);
    }
}

internal record EncryptedCacheEntry(byte[] Ciphertext, string KeyVersion);
```

### Key Management (NF-3.2)

| Environment | Key Source | Rotation |
|-------------|------------|----------|
| `prod` | External KMS (AWS KMS, Azure Key Vault, etc.) | Quarterly or on incident |
| `dev`/`staging` | Kubernetes Secret or environment variable | Manual |

```csharp
public interface ICacheEncryptionProvider
{
    string CurrentKeyVersion { get; }
    Task<byte[]> EncryptAsync(string plaintext, CancellationToken ct = default);
    Task<string> DecryptAsync(byte[] ciphertext, string keyVersion, CancellationToken ct = default);
}
```

**Key rotation support:** The `keyVersion` field in cached entries allows decryption with previous keys during rotation windows.

## Cache Invalidation Triggers (Req 10.2)

| Event | Action | Implementation |
|-------|--------|----------------|
| Track deletion | Invalidate `stream:{userId}:{trackId}` | `TrackDeletedHandler` |
| User logout (single session) | No action (URLs expire naturally) | — |
| User logout (all sessions) | Invalidate `stream:{userId}:*` | `AuthService.LogoutAllAsync()` |
| Track status change to non-Ready | Invalidate `stream:{userId}:{trackId}` | `TrackService` |
