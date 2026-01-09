# 2. Streaming Service

## Interface: `IStreamingService`

```csharp
namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for generating and caching presigned streaming URLs.
/// </summary>
public interface IStreamingService
{
    /// <summary>
    /// Gets or generates a presigned streaming URL for a track.
    /// </summary>
    /// <param name="trackId">Track identifier (ULID).</param>
    /// <param name="userId">Requesting user identifier (ULID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Streaming URL response with expiry.</returns>
    Task<StreamUrlResult> GetStreamUrlAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates cached streaming URL for a track.
    /// Called on track deletion or user logout.
    /// </summary>
    Task InvalidateCacheAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates all cached streaming URLs for a user.
    /// Called on user logout (all sessions).
    /// </summary>
    Task InvalidateAllUserCacheAsync(
        string userId,
        CancellationToken ct = default);
}

/// <summary>
/// Result of streaming URL generation.
/// </summary>
public record StreamUrlResult(
    string StreamUrl,
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes,
    bool SupportsRangeRequests);
```

## Implementation: `StreamingService`

```csharp
public class StreamingService : IStreamingService
{
    private readonly IStorageService _storageService;
    private readonly IEncryptedCacheService _encryptedCache;
    private readonly IDocumentSession _session;
    private readonly IOptions<StreamingOptions> _options;
    private readonly ILogger<StreamingService> _logger;

    public async Task<StreamUrlResult> GetStreamUrlAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        // 1. Check cache first
        var cacheKey = BuildCacheKey(userId, trackId);
        var cached = await _encryptedCache.GetAsync<CachedStreamUrl>(cacheKey, ct);

        if (cached is not null && cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            _logger.LogDebug("Cache hit for streaming URL {TrackId}", trackId);
            return new StreamUrlResult(
                cached.Url,
                cached.ExpiresAt,
                cached.ContentType,
                cached.FileSizeBytes,
                true);
        }

        // 2. Load track from RavenDB
        var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);
        // Validation happens in endpoint; service assumes valid input

        // 3. Generate presigned GET URL
        var expiry = _options.Value.PresignExpiry;
        var url = await _storageService.GeneratePresignedDownloadUrlAsync(
            track.ObjectKey,
            expiry,
            ct);

        // 4. Cache with encryption (TTL slightly shorter than presign)
        var cacheTtl = expiry - TimeSpan.FromSeconds(30);
        var cacheEntry = new CachedStreamUrl(
            url.Url,
            url.ExpiresAt,
            track.MimeType,
            track.FileSizeBytes);

        await _encryptedCache.SetAsync(cacheKey, cacheEntry, cacheTtl, ct);

        _logger.LogDebug(
            "Generated and cached streaming URL for {TrackId}, expires at {ExpiresAt}",
            trackId, url.ExpiresAt);

        return new StreamUrlResult(
            url.Url,
            url.ExpiresAt,
            track.MimeType,
            track.FileSizeBytes,
            true);
    }

    public async Task InvalidateCacheAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(userId, trackId);
        await _encryptedCache.RemoveAsync(cacheKey, ct);
        _logger.LogDebug("Invalidated streaming URL cache for {TrackId}", trackId);
    }

    public async Task InvalidateAllUserCacheAsync(
        string userId,
        CancellationToken ct = default)
    {
        // Use pattern-based deletion: stream:{userId}:*
        var pattern = $"stream:{userId}:*";
        await _encryptedCache.RemoveByPatternAsync(pattern, ct);
        _logger.LogDebug("Invalidated all streaming URL caches for user {UserId}", userId);
    }

    private static string BuildCacheKey(string userId, string trackId)
        => $"stream:{userId}:{trackId}";
}
```
