using System.Diagnostics;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.ApiService.Models;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Implementation of IStreamingService with caching and presigned URL generation (Req 5.1, 5.2).
/// </summary>
public class StreamingService : IStreamingService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IStorageService _storageService;
    private readonly IEncryptedCacheService _encryptedCache;
    private readonly StreamingOptions _options;
    private readonly ILogger<StreamingService> _logger;

    public StreamingService(
        IAsyncDocumentSession session,
        IStorageService storageService,
        IEncryptedCacheService encryptedCache,
        IOptions<StreamingOptions> options,
        ILogger<StreamingService> logger)
    {
        _session = session;
        _storageService = storageService;
        _encryptedCache = encryptedCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StreamUrlResult> GetStreamUrlAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. Check cache first
        var cacheKey = BuildCacheKey(userId, trackId);
        var cached = await _encryptedCache.GetAsync<CachedStreamUrl>(cacheKey, ct);

        if (cached is not null && cached.ExpiresAt > DateTimeOffset.UtcNow.Add(_options.CacheTtlBuffer))
        {
            stopwatch.Stop();
            _logger.LogDebug(
                "Cache hit for streaming URL {TrackId}, duration={DurationMs}ms",
                trackId, stopwatch.Elapsed.TotalMilliseconds);

            NovaTuneMetrics.RecordStreamUrlRequest("cache_hit", stopwatch.Elapsed.TotalMilliseconds);

            return new StreamUrlResult(
                cached.Url,
                cached.ExpiresAt,
                cached.ContentType,
                cached.FileSizeBytes,
                SupportsRangeRequests: true);
        }

        // 2. Load track from RavenDB
        var track = await _session.LoadAsync<Track>($"Tracks/{trackId}", ct);

        // Validation happens in endpoint; service assumes valid input
        // but we double-check for safety
        if (track is null)
        {
            _logger.LogWarning("Track {TrackId} not found during stream URL generation", trackId);
            throw new InvalidOperationException($"Track {trackId} not found");
        }

        // 3. Generate presigned GET URL
        var expiry = _options.PresignExpiry;
        var url = await _storageService.GeneratePresignedDownloadUrlAsync(
            track.ObjectKey,
            expiry,
            ct);

        // 4. Cache with encryption (TTL slightly shorter than presign)
        var cacheTtl = expiry - _options.CacheTtlBuffer;
        var cacheEntry = new CachedStreamUrl(
            url.Url,
            url.ExpiresAt,
            track.MimeType,
            track.FileSizeBytes);

        // Cache write is fire-and-forget (fail-open per 09-resilience.md)
        await _encryptedCache.SetAsync(cacheKey, cacheEntry, cacheTtl, ct);

        stopwatch.Stop();
        _logger.LogDebug(
            "Generated and cached streaming URL for {TrackId}, expires at {ExpiresAt}, duration={DurationMs}ms",
            trackId, url.ExpiresAt, stopwatch.Elapsed.TotalMilliseconds);

        NovaTuneMetrics.RecordStreamUrlRequest("cache_miss", stopwatch.Elapsed.TotalMilliseconds);

        return new StreamUrlResult(
            url.Url,
            url.ExpiresAt,
            track.MimeType,
            track.FileSizeBytes,
            SupportsRangeRequests: true);
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

/// <summary>
/// Cached stream URL entry (Req 10.2).
/// </summary>
internal record CachedStreamUrl(
    string Url,
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes);
