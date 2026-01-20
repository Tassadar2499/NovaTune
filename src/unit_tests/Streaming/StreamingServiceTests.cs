using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NovaTune.UnitTests.Fakes;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Streaming;

/// <summary>
/// Unit tests for StreamingService: cache hit/miss scenarios and TTL calculation.
/// Tests per 11-test-strategy.md requirements.
/// </summary>
public class StreamingServiceTests
{
    private readonly AsyncDocumentSessionFake _sessionFake;
    private readonly StorageServiceFake _storageServiceFake;
    private readonly EncryptedCacheServiceFake _encryptedCacheFake;
    private readonly StreamingOptions _options;
    private readonly StreamingService _service;

    public StreamingServiceTests()
    {
        _sessionFake = new AsyncDocumentSessionFake();
        _storageServiceFake = new StorageServiceFake();
        _encryptedCacheFake = new EncryptedCacheServiceFake();
        _options = new StreamingOptions
        {
            PresignExpiry = TimeSpan.FromMinutes(2),
            CacheTtlBuffer = TimeSpan.FromSeconds(30)
        };

        _service = new StreamingService(
            _sessionFake,
            _storageServiceFake,
            _encryptedCacheFake,
            Options.Create(_options),
            NullLogger<StreamingService>.Instance);
    }

    // ========================================================================
    // Cache Hit Scenarios
    // ========================================================================

    [Fact]
    public async Task GetStreamUrlAsync_Should_return_cached_url_when_cache_hit_and_not_expired()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        var cachedUrl = new CachedStreamUrl(
            "https://cached.example.com/audio.mp3",
            DateTimeOffset.UtcNow.AddMinutes(5), // Expires well after buffer
            "audio/mpeg",
            1024000);

        _encryptedCacheFake.Cache[$"stream:{userId}:{trackId}"] =
            (cachedUrl, DateTimeOffset.UtcNow.AddMinutes(5));

        // Act
        var result = await _service.GetStreamUrlAsync(trackId, userId);

        // Assert
        result.StreamUrl.ShouldBe("https://cached.example.com/audio.mp3");
        result.ContentType.ShouldBe("audio/mpeg");
        result.FileSizeBytes.ShouldBe(1024000);
        result.SupportsRangeRequests.ShouldBeTrue();
    }

    [Fact]
    public async Task GetStreamUrlAsync_Should_not_call_storage_on_cache_hit()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        var cachedUrl = new CachedStreamUrl(
            "https://cached.example.com/audio.mp3",
            DateTimeOffset.UtcNow.AddMinutes(5),
            "audio/mpeg",
            1024000);

        _encryptedCacheFake.Cache[$"stream:{userId}:{trackId}"] =
            (cachedUrl, DateTimeOffset.UtcNow.AddMinutes(5));

        var storageCalled = false;
        _storageServiceFake.ExceptionToThrow = new InvalidOperationException("Should not be called");

        // Act - should not throw because cache hit skips storage call
        try
        {
            await _service.GetStreamUrlAsync(trackId, userId);
        }
        catch (InvalidOperationException)
        {
            storageCalled = true;
        }

        // Assert
        storageCalled.ShouldBeFalse();
    }

    // ========================================================================
    // Cache Miss Scenarios
    // ========================================================================

    [Fact]
    public async Task GetStreamUrlAsync_Should_generate_new_url_on_cache_miss()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        // No cache entry exists

        // Act
        var result = await _service.GetStreamUrlAsync(trackId, userId);

        // Assert
        result.StreamUrl.ShouldContain("storage.example.com");
        result.StreamUrl.ShouldContain(track.ObjectKey);
        result.ContentType.ShouldBe(track.MimeType);
        result.FileSizeBytes.ShouldBe(track.FileSizeBytes);
    }

    [Fact]
    public async Task GetStreamUrlAsync_Should_cache_generated_url_on_miss()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        // Act
        await _service.GetStreamUrlAsync(trackId, userId);

        // Assert
        var cacheKey = $"stream:{userId}:{trackId}";
        _encryptedCacheFake.Cache.ShouldContainKey(cacheKey);
    }

    [Fact]
    public async Task GetStreamUrlAsync_Should_treat_expired_cache_as_miss()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        // Cache entry with URL that expires within the buffer period
        var cachedUrl = new CachedStreamUrl(
            "https://old-cached.example.com/audio.mp3",
            DateTimeOffset.UtcNow.AddSeconds(15), // Expires within 30s buffer
            "audio/mpeg",
            1024000);

        _encryptedCacheFake.Cache[$"stream:{userId}:{trackId}"] =
            (cachedUrl, DateTimeOffset.UtcNow.AddMinutes(5)); // Cache entry itself valid

        // Act
        var result = await _service.GetStreamUrlAsync(trackId, userId);

        // Assert - should get fresh URL, not the cached one
        result.StreamUrl.ShouldNotBe("https://old-cached.example.com/audio.mp3");
        result.StreamUrl.ShouldContain("storage.example.com");
    }

    [Fact]
    public async Task GetStreamUrlAsync_Should_throw_when_track_not_found()
    {
        // Arrange
        var trackId = "01HQNONEXISTENT123456789";
        var userId = "01HQUSER123456789ABCDEFG";

        // No track stored

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.GetStreamUrlAsync(trackId, userId));

        exception.Message.ShouldContain(trackId);
        exception.Message.ShouldContain("not found");
    }

    // ========================================================================
    // TTL Calculation Tests
    // ========================================================================

    [Fact]
    public async Task GetStreamUrlAsync_Should_set_cache_ttl_with_buffer_subtracted()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        TimeSpan? capturedTtl = null;
        _encryptedCacheFake.OnSetAsync = (key, value, ttl, ct) =>
        {
            capturedTtl = ttl;
            return Task.CompletedTask;
        };

        // Act
        await _service.GetStreamUrlAsync(trackId, userId);

        // Assert
        // PresignExpiry (2 min) - CacheTtlBuffer (30s) = 90s
        var expectedTtl = _options.PresignExpiry - _options.CacheTtlBuffer;
        capturedTtl.ShouldNotBeNull();
        capturedTtl.Value.ShouldBe(expectedTtl);
    }

    [Fact]
    public async Task GetStreamUrlAsync_Should_set_url_expiry_to_match_presign_expiry()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        var beforeCall = DateTimeOffset.UtcNow;

        // Act
        var result = await _service.GetStreamUrlAsync(trackId, userId);

        var afterCall = DateTimeOffset.UtcNow;

        // Assert - ExpiresAt should be roughly PresignExpiry from now
        var expectedMinExpiry = beforeCall.Add(_options.PresignExpiry);
        var expectedMaxExpiry = afterCall.Add(_options.PresignExpiry);

        result.ExpiresAt.ShouldBeGreaterThanOrEqualTo(expectedMinExpiry);
        result.ExpiresAt.ShouldBeLessThanOrEqualTo(expectedMaxExpiry);
    }

    [Fact]
    public async Task GetStreamUrlAsync_Should_use_buffer_to_determine_cache_validity()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        // Cache entry that expires just after the buffer period (should still be valid)
        var cachedUrl = new CachedStreamUrl(
            "https://still-valid.example.com/audio.mp3",
            DateTimeOffset.UtcNow.AddSeconds(35), // 35s > 30s buffer, so valid
            "audio/mpeg",
            1024000);

        _encryptedCacheFake.Cache[$"stream:{userId}:{trackId}"] =
            (cachedUrl, DateTimeOffset.UtcNow.AddMinutes(5));

        // Act
        var result = await _service.GetStreamUrlAsync(trackId, userId);

        // Assert - should return cached URL since it's still valid
        result.StreamUrl.ShouldBe("https://still-valid.example.com/audio.mp3");
    }

    // ========================================================================
    // Cache Invalidation Tests
    // ========================================================================

    [Fact]
    public async Task InvalidateCacheAsync_Should_remove_single_track_cache()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var cacheKey = $"stream:{userId}:{trackId}";

        var cachedUrl = new CachedStreamUrl(
            "https://cached.example.com/audio.mp3",
            DateTimeOffset.UtcNow.AddMinutes(5),
            "audio/mpeg",
            1024000);

        _encryptedCacheFake.Cache[cacheKey] = (cachedUrl, DateTimeOffset.UtcNow.AddMinutes(5));

        // Act
        await _service.InvalidateCacheAsync(trackId, userId);

        // Assert
        _encryptedCacheFake.RemovedKeys.ShouldContain(cacheKey);
    }

    [Fact]
    public async Task InvalidateAllUserCacheAsync_Should_remove_all_user_caches()
    {
        // Arrange
        var userId = "01HQUSER123456789ABCDEFG";

        // Add multiple cache entries for the user
        for (var i = 0; i < 3; i++)
        {
            var cacheKey = $"stream:{userId}:track{i}";
            var cachedUrl = new CachedStreamUrl(
                $"https://cached{i}.example.com/audio.mp3",
                DateTimeOffset.UtcNow.AddMinutes(5),
                "audio/mpeg",
                1024000);

            _encryptedCacheFake.Cache[cacheKey] = (cachedUrl, DateTimeOffset.UtcNow.AddMinutes(5));
        }

        // Act
        await _service.InvalidateAllUserCacheAsync(userId);

        // Assert
        _encryptedCacheFake.RemovedPatterns.ShouldContain($"stream:{userId}:*");
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static Track CreateReadyTrack(string trackId, string userId)
    {
        return new Track
        {
            Id = $"Tracks/{trackId}",
            TrackId = trackId,
            UserId = userId,
            Title = "Test Track",
            Artist = "Test Artist",
            Duration = TimeSpan.FromMinutes(3),
            ObjectKey = $"audio/{userId}/{trackId}/original.mp3",
            FileSizeBytes = 5_000_000,
            MimeType = "audio/mpeg",
            Status = TrackStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }
}

// Note: We use NovaTuneApp.ApiService.Services.CachedStreamUrl directly via InternalsVisibleTo
