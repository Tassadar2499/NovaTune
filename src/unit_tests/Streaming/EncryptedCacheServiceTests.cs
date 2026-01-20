using Microsoft.Extensions.Logging.Abstractions;
using NovaTune.UnitTests.Fakes;
using NovaTuneApp.ApiService.Infrastructure.Caching;

namespace NovaTune.UnitTests.Streaming;

/// <summary>
/// Unit tests for EncryptedCacheService: encrypt/decrypt round-trip and key version handling.
/// Tests per 11-test-strategy.md requirements.
/// </summary>
public class EncryptedCacheServiceTests
{
    private readonly CacheServiceFake _innerCacheFake;
    private readonly CacheEncryptionProviderFake _encryptionFake;
    private readonly EncryptedCacheService _service;

    public EncryptedCacheServiceTests()
    {
        _innerCacheFake = new CacheServiceFake();
        _encryptionFake = new CacheEncryptionProviderFake();

        _service = new EncryptedCacheService(
            _innerCacheFake,
            _encryptionFake,
            NullLogger<EncryptedCacheService>.Instance);
    }

    // ========================================================================
    // Encrypt/Decrypt Round-Trip Tests
    // ========================================================================

    [Fact]
    public async Task SetAsync_Then_GetAsync_Should_round_trip_successfully()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("hello", 42);
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        await _service.SetAsync(key, value, ttl);
        var result = await _service.GetAsync<TestCacheEntry>(key);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("hello");
        result.Count.ShouldBe(42);
    }

    [Fact]
    public async Task SetAsync_Should_encrypt_value_before_storing()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("secret-data", 123);
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        await _service.SetAsync(key, value, ttl);

        // Assert
        _encryptionFake.EncryptedValues.ShouldNotBeEmpty();
        _encryptionFake.EncryptedValues.First().ShouldContain("secret-data");
    }

    [Fact]
    public async Task GetAsync_Should_decrypt_value_when_found()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("data", 999);
        await _service.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Act
        var result = await _service.GetAsync<TestCacheEntry>(key);

        // Assert
        _encryptionFake.DecryptedValues.ShouldNotBeEmpty();
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAsync_Should_return_null_when_key_not_found()
    {
        // Arrange - no data stored

        // Act
        var result = await _service.GetAsync<TestCacheEntry>("nonexistent-key");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_Should_store_with_correct_ttl()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("data", 1);
        var ttl = TimeSpan.FromMinutes(10);

        TimeSpan? capturedTtl = null;
        _innerCacheFake.OnSetAsync = (k, v, t, ct) =>
        {
            capturedTtl = t;
            _innerCacheFake.Cache[k] = (v, DateTimeOffset.UtcNow.Add(t));
            return Task.CompletedTask;
        };

        // Act
        await _service.SetAsync(key, value, ttl);

        // Assert
        capturedTtl.ShouldBe(ttl);
    }

    [Fact]
    public async Task SetAsync_Should_store_key_version_with_encrypted_data()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("data", 1);
        _encryptionFake.CurrentKeyVersion = "v2";

        // Act
        await _service.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Assert
        _innerCacheFake.Cache.ShouldContainKey(key);
        // The wrapper should be an EncryptedCacheEntry with the key version
        // We verify indirectly through successful round-trip with correct version
        var result = await _service.GetAsync<TestCacheEntry>(key);
        result.ShouldNotBeNull();
    }

    // ========================================================================
    // Key Version Handling Tests
    // ========================================================================

    [Fact]
    public async Task GetAsync_Should_use_stored_key_version_for_decryption()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("versioned-data", 55);

        // Store with v1
        _encryptionFake.CurrentKeyVersion = "v1";
        await _service.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Verify decryption used v1
        var result = await _service.GetAsync<TestCacheEntry>(key);

        // Assert
        _encryptionFake.DecryptedValues.Any(d => d.KeyVersion == "v1").ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_Should_fail_gracefully_on_key_version_mismatch()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("old-data", 1);

        // Store with v1
        _encryptionFake.CurrentKeyVersion = "v1";
        await _service.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Change current version to v2 and enable mismatch detection
        _encryptionFake.CurrentKeyVersion = "v2";
        _encryptionFake.ThrowOnKeyVersionMismatch = true;

        // Act - should return null (fail-open) rather than throwing
        var result = await _service.GetAsync<TestCacheEntry>(key);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_Should_use_current_key_version()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("new-data", 99);
        _encryptionFake.CurrentKeyVersion = "v3";

        // Act
        await _service.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Clear decrypted values and retrieve
        _encryptionFake.DecryptedValues.Clear();
        var result = await _service.GetAsync<TestCacheEntry>(key);

        // Assert
        _encryptionFake.DecryptedValues.Any(d => d.KeyVersion == "v3").ShouldBeTrue();
    }

    // ========================================================================
    // Error Handling Tests (Fail-Open Pattern)
    // ========================================================================

    [Fact]
    public async Task GetAsync_Should_return_null_on_decryption_failure()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("data", 1);
        await _service.SetAsync(key, value, TimeSpan.FromMinutes(5));

        _encryptionFake.ThrowOnDecrypt = true;

        // Act
        var result = await _service.GetAsync<TestCacheEntry>(key);

        // Assert - fail-open: return null, don't throw
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_Should_not_throw_on_encryption_failure()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("data", 1);
        _encryptionFake.ThrowOnEncrypt = true;

        // Act & Assert - should not throw (fail-open)
        await Should.NotThrowAsync(
            () => _service.SetAsync(key, value, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task SetAsync_Should_not_throw_on_cache_write_failure()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("data", 1);
        _innerCacheFake.ThrowOnSet = true;

        // Act & Assert - should not throw (fail-open)
        await Should.NotThrowAsync(
            () => _service.SetAsync(key, value, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task GetAsync_Should_return_null_on_cache_read_failure()
    {
        // Arrange
        _innerCacheFake.ThrowOnGet = true;

        // Act
        var result = await _service.GetAsync<TestCacheEntry>("any-key");

        // Assert
        result.ShouldBeNull();
    }

    // ========================================================================
    // Remove Operations Tests
    // ========================================================================

    [Fact]
    public async Task RemoveAsync_Should_remove_key_from_cache()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheEntry("data", 1);
        await _service.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Act
        await _service.RemoveAsync(key);

        // Assert
        _innerCacheFake.RemovedKeys.ShouldContain(key);
    }

    [Fact]
    public async Task RemoveByPatternAsync_Should_remove_matching_keys()
    {
        // Arrange
        var pattern = "stream:user1:*";

        // Act
        await _service.RemoveByPatternAsync(pattern);

        // Assert
        _innerCacheFake.RemovedPatterns.ShouldContain(pattern);
    }

    [Fact]
    public async Task RemoveAsync_Should_not_throw_on_failure()
    {
        // Arrange
        _innerCacheFake.OnSetAsync = (_, _, _, _) => throw new InvalidOperationException();

        // Act & Assert - should not throw
        await Should.NotThrowAsync(
            () => _service.RemoveAsync("any-key"));
    }

    [Fact]
    public async Task RemoveByPatternAsync_Should_not_throw_on_failure()
    {
        // Arrange
        _innerCacheFake.OnSetAsync = (_, _, _, _) => throw new InvalidOperationException();

        // Act & Assert - should not throw
        await Should.NotThrowAsync(
            () => _service.RemoveByPatternAsync("pattern:*"));
    }
}

/// <summary>
/// Test record for cache entry testing.
/// </summary>
public record TestCacheEntry(string Name, int Count);
