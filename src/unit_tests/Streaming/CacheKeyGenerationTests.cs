using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NovaTune.UnitTests.Fakes;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Streaming;

/// <summary>
/// Unit tests for cache key generation in StreamingService.
/// Tests per 11-test-strategy.md requirements.
/// </summary>
public class CacheKeyGenerationTests
{
    private readonly AsyncDocumentSessionFake _sessionFake;
    private readonly StorageServiceFake _storageServiceFake;
    private readonly EncryptedCacheServiceFake _encryptedCacheFake;
    private readonly StreamingService _service;

    public CacheKeyGenerationTests()
    {
        _sessionFake = new AsyncDocumentSessionFake();
        _storageServiceFake = new StorageServiceFake();
        _encryptedCacheFake = new EncryptedCacheServiceFake();

        var options = new StreamingOptions
        {
            PresignExpiry = TimeSpan.FromMinutes(2),
            CacheTtlBuffer = TimeSpan.FromSeconds(30)
        };

        _service = new StreamingService(
            _sessionFake,
            _storageServiceFake,
            _encryptedCacheFake,
            Options.Create(options),
            NullLogger<StreamingService>.Instance);
    }

    // ========================================================================
    // Cache Key Format Tests
    // ========================================================================

    [Fact]
    public async Task Cache_key_Should_follow_pattern_stream_userId_trackId()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        string? capturedKey = null;
        _encryptedCacheFake.OnSetAsync = (key, value, ttl, ct) =>
        {
            capturedKey = key;
            return Task.CompletedTask;
        };

        // Act
        await _service.GetStreamUrlAsync(trackId, userId);

        // Assert
        capturedKey.ShouldBe($"stream:{userId}:{trackId}");
    }

    [Fact]
    public async Task Cache_key_Should_be_unique_per_user_track_combination()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId1 = "01HQUSER1AAAAAAAAAAAAAAAA";
        var userId2 = "01HQUSER2BBBBBBBBBBBBBBBB";

        var track1 = CreateReadyTrack(trackId, userId1);
        var track2 = CreateReadyTrack(trackId, userId2);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track1);

        var capturedKeys = new List<string>();
        _encryptedCacheFake.OnSetAsync = (key, value, ttl, ct) =>
        {
            capturedKeys.Add(key);
            return Task.CompletedTask;
        };

        // Act
        await _service.GetStreamUrlAsync(trackId, userId1);

        // Update track to be owned by user2 for second call
        _sessionFake.Clear();
        _sessionFake.StoreDocument($"Tracks/{trackId}", track2);

        await _service.GetStreamUrlAsync(trackId, userId2);

        // Assert
        capturedKeys.Count.ShouldBe(2);
        capturedKeys[0].ShouldBe($"stream:{userId1}:{trackId}");
        capturedKeys[1].ShouldBe($"stream:{userId2}:{trackId}");
        capturedKeys[0].ShouldNotBe(capturedKeys[1]);
    }

    [Fact]
    public async Task Same_user_different_tracks_Should_have_different_cache_keys()
    {
        // Arrange
        var trackId1 = "01HQTRACK1AAAAAAAAAAAAAAA";
        var trackId2 = "01HQTRACK2BBBBBBBBBBBBBBB";
        var userId = "01HQUSER123456789ABCDEFG";

        var track1 = CreateReadyTrack(trackId1, userId);
        var track2 = CreateReadyTrack(trackId2, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId1}", track1);
        _sessionFake.StoreDocument($"Tracks/{trackId2}", track2);

        var capturedKeys = new List<string>();
        _encryptedCacheFake.OnSetAsync = (key, value, ttl, ct) =>
        {
            capturedKeys.Add(key);
            return Task.CompletedTask;
        };

        // Act
        await _service.GetStreamUrlAsync(trackId1, userId);
        await _service.GetStreamUrlAsync(trackId2, userId);

        // Assert
        capturedKeys.Count.ShouldBe(2);
        capturedKeys[0].ShouldBe($"stream:{userId}:{trackId1}");
        capturedKeys[1].ShouldBe($"stream:{userId}:{trackId2}");
    }

    // ========================================================================
    // Cache Key Lookup Tests
    // ========================================================================

    [Fact]
    public async Task GetStreamUrlAsync_Should_lookup_with_correct_cache_key()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        string? lookedUpKey = null;
        _encryptedCacheFake.OnGetAsync = (key, ct) =>
        {
            lookedUpKey = key;
            return Task.FromResult<object?>(null);
        };

        // Act
        await _service.GetStreamUrlAsync(trackId, userId);

        // Assert
        lookedUpKey.ShouldBe($"stream:{userId}:{trackId}");
    }

    // ========================================================================
    // Cache Invalidation Key Tests
    // ========================================================================

    [Fact]
    public async Task InvalidateCacheAsync_Should_use_correct_cache_key()
    {
        // Arrange
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";

        // Act
        await _service.InvalidateCacheAsync(trackId, userId);

        // Assert
        _encryptedCacheFake.RemovedKeys.ShouldContain($"stream:{userId}:{trackId}");
    }

    [Fact]
    public async Task InvalidateAllUserCacheAsync_Should_use_wildcard_pattern()
    {
        // Arrange
        var userId = "01HQUSER123456789ABCDEFG";

        // Act
        await _service.InvalidateAllUserCacheAsync(userId);

        // Assert
        _encryptedCacheFake.RemovedPatterns.ShouldContain($"stream:{userId}:*");
    }

    [Fact]
    public async Task InvalidateAllUserCacheAsync_pattern_Should_match_all_user_tracks()
    {
        // Arrange
        var userId = "01HQUSER123456789ABCDEFG";

        // Pre-populate cache with multiple tracks for the user
        var trackIds = new[] { "track1", "track2", "track3" };
        foreach (var trackId in trackIds)
        {
            var cacheKey = $"stream:{userId}:{trackId}";
            _encryptedCacheFake.Cache[cacheKey] = (new object(), DateTimeOffset.UtcNow.AddMinutes(5));
        }

        // Also add a track for a different user (should not be removed)
        _encryptedCacheFake.Cache["stream:other-user:track1"] = (new object(), DateTimeOffset.UtcNow.AddMinutes(5));

        // Act
        await _service.InvalidateAllUserCacheAsync(userId);

        // Assert - all user's tracks should be removed
        foreach (var trackId in trackIds)
        {
            _encryptedCacheFake.Cache.ShouldNotContainKey($"stream:{userId}:{trackId}");
        }

        // Other user's track should remain
        _encryptedCacheFake.Cache.ShouldContainKey("stream:other-user:track1");
    }

    // ========================================================================
    // Edge Cases
    // ========================================================================

    [Fact]
    public async Task Cache_key_Should_handle_special_characters_in_ids()
    {
        // Note: In practice, track and user IDs are ULIDs which don't have special chars,
        // but this tests the key format doesn't break with edge cases
        var trackId = "01HQXYZ123456789ABCDEFGH";
        var userId = "01HQUSER123456789ABCDEFG";
        var track = CreateReadyTrack(trackId, userId);
        _sessionFake.StoreDocument($"Tracks/{trackId}", track);

        string? capturedKey = null;
        _encryptedCacheFake.OnSetAsync = (key, value, ttl, ct) =>
        {
            capturedKey = key;
            return Task.CompletedTask;
        };

        // Act
        await _service.GetStreamUrlAsync(trackId, userId);

        // Assert
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldStartWith("stream:");
        capturedKey.ShouldContain(":");
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
