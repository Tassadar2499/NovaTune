using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Configuration;

namespace NovaTune.UnitTests.Streaming;

/// <summary>
/// Unit tests for AesGcmCacheEncryptionProvider: encrypt/decrypt and key version handling.
/// Tests per 11-test-strategy.md requirements.
/// </summary>
public class AesGcmCacheEncryptionProviderTests
{
    private const string TestKey = "this-is-a-very-secure-test-key-for-aes-gcm-encryption-32chars";

    // ========================================================================
    // Encrypt/Decrypt Round-Trip Tests
    // ========================================================================

    [Fact]
    public async Task EncryptAsync_Then_DecryptAsync_Should_round_trip_successfully()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = "Hello, World! This is a test message.";

        // Act
        var encrypted = await provider.EncryptAsync(plaintext);
        var decrypted = await provider.DecryptAsync(encrypted, "v1");

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public async Task EncryptAsync_Should_produce_different_ciphertext_each_time()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = "Same message";

        // Act
        var encrypted1 = await provider.EncryptAsync(plaintext);
        var encrypted2 = await provider.EncryptAsync(plaintext);

        // Assert - ciphertexts should differ due to random nonces
        encrypted1.ShouldNotBe(encrypted2);
    }

    [Fact]
    public async Task EncryptAsync_Should_handle_empty_string()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = "";

        // Act
        var encrypted = await provider.EncryptAsync(plaintext);
        var decrypted = await provider.DecryptAsync(encrypted, "v1");

        // Assert
        decrypted.ShouldBe("");
    }

    [Fact]
    public async Task EncryptAsync_Should_handle_unicode_content()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = "Привет мир! 你好世界! 🎵🎶";

        // Act
        var encrypted = await provider.EncryptAsync(plaintext);
        var decrypted = await provider.DecryptAsync(encrypted, "v1");

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public async Task EncryptAsync_Should_handle_large_content()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = new string('X', 100_000); // 100KB of data

        // Act
        var encrypted = await provider.EncryptAsync(plaintext);
        var decrypted = await provider.DecryptAsync(encrypted, "v1");

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public async Task EncryptAsync_Should_produce_ciphertext_with_expected_structure()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = "Test";

        // Act
        var encrypted = await provider.EncryptAsync(plaintext);

        // Assert - encrypted data should contain: nonce (12 bytes) + ciphertext + tag (16 bytes)
        // Minimum size: 12 + 0 + 16 = 28 bytes for empty plaintext
        // For "Test" (4 bytes UTF-8): 12 + 4 + 16 = 32 bytes
        encrypted.Length.ShouldBeGreaterThanOrEqualTo(28);
    }

    // ========================================================================
    // Key Version Handling Tests
    // ========================================================================

    [Fact]
    public void CurrentKeyVersion_Should_return_configured_version()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v2");

        // Act & Assert
        provider.CurrentKeyVersion.ShouldBe("v2");
    }

    [Fact]
    public async Task DecryptAsync_Should_throw_for_mismatched_key_version()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = "Secret data";
        var encrypted = await provider.EncryptAsync(plaintext);

        // Act & Assert
        // The provider only supports the current key version, so mismatched version throws
        var exception = await Should.ThrowAsync<CryptographicException>(
            () => provider.DecryptAsync(encrypted, "v2")); // Wrong version

        exception.Message.ShouldContain("v2");
    }

    [Fact]
    public async Task DecryptAsync_Should_succeed_with_matching_key_version()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v3");
        var plaintext = "Version 3 data";
        var encrypted = await provider.EncryptAsync(plaintext);

        // Act
        var decrypted = await provider.DecryptAsync(encrypted, "v3");

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    [InlineData("production-key-2024")]
    [InlineData("rotated-key-v42")]
    public async Task Provider_Should_work_with_various_key_version_formats(string keyVersion)
    {
        // Arrange
        var provider = CreateProvider(TestKey, keyVersion);
        var plaintext = "Test data";

        // Act
        var encrypted = await provider.EncryptAsync(plaintext);
        var decrypted = await provider.DecryptAsync(encrypted, keyVersion);

        // Assert
        provider.CurrentKeyVersion.ShouldBe(keyVersion);
        decrypted.ShouldBe(plaintext);
    }

    // ========================================================================
    // Key Derivation Tests
    // ========================================================================

    [Fact]
    public async Task Different_keys_Should_produce_different_ciphertexts()
    {
        // Arrange
        var provider1 = CreateProvider("key-one-that-is-at-least-32-characters-long", "v1");
        var provider2 = CreateProvider("key-two-that-is-at-least-32-characters-long", "v1");
        var plaintext = "Same message";

        // Act
        var encrypted1 = await provider1.EncryptAsync(plaintext);
        var encrypted2 = await provider2.EncryptAsync(plaintext);

        // Assert - different keys should produce different ciphertexts
        // (technically they could collide, but that's astronomically unlikely)
        encrypted1.SequenceEqual(encrypted2).ShouldBeFalse();
    }

    [Fact]
    public async Task Same_key_different_versions_Should_produce_incompatible_ciphertexts()
    {
        // Arrange
        var provider1 = CreateProvider(TestKey, "v1");
        var provider2 = CreateProvider(TestKey, "v2");
        var plaintext = "Test data";

        // Act
        var encrypted = await provider1.EncryptAsync(plaintext);

        // Assert - decrypting with different version's provider should fail
        // because HKDF uses version in salt, producing different derived keys
        // The decryption will fail due to authentication tag mismatch
        await Should.ThrowAsync<CryptographicException>(
            () => provider2.DecryptAsync(encrypted, "v2"));
    }

    // ========================================================================
    // Error Handling Tests
    // ========================================================================

    [Fact]
    public async Task DecryptAsync_Should_throw_for_invalid_ciphertext()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var invalidCiphertext = new byte[] { 1, 2, 3, 4, 5 }; // Too short

        // Act & Assert
        await Should.ThrowAsync<CryptographicException>(
            () => provider.DecryptAsync(invalidCiphertext, "v1"));
    }

    [Fact]
    public async Task DecryptAsync_Should_throw_for_tampered_ciphertext()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = "Original message";
        var encrypted = await provider.EncryptAsync(plaintext);

        // Tamper with the ciphertext
        encrypted[20] ^= 0xFF;

        // Act & Assert
        await Should.ThrowAsync<CryptographicException>(
            () => provider.DecryptAsync(encrypted, "v1"));
    }

    [Fact]
    public async Task DecryptAsync_Should_throw_for_truncated_ciphertext()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var plaintext = "Original message";
        var encrypted = await provider.EncryptAsync(plaintext);

        // Truncate the ciphertext
        var truncated = encrypted[..^5];

        // Act & Assert
        await Should.ThrowAsync<CryptographicException>(
            () => provider.DecryptAsync(truncated, "v1"));
    }

    // ========================================================================
    // Development Mode Tests
    // ========================================================================

    [Fact]
    public void Provider_Should_generate_random_key_when_no_key_configured()
    {
        // Arrange & Act
        var provider = CreateProvider(null, "v1");

        // Assert - provider should be created (uses random key)
        provider.ShouldNotBeNull();
        provider.CurrentKeyVersion.ShouldBe("v1");
    }

    [Fact]
    public async Task Provider_with_random_key_Should_still_encrypt_decrypt()
    {
        // Arrange
        var provider = CreateProvider(null, "v1");
        var plaintext = "Test with random key";

        // Act
        var encrypted = await provider.EncryptAsync(plaintext);
        var decrypted = await provider.DecryptAsync(encrypted, "v1");

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    // ========================================================================
    // JSON Serialization Round-Trip (as used by EncryptedCacheService)
    // ========================================================================

    [Fact]
    public async Task Provider_Should_handle_json_serialized_content()
    {
        // Arrange
        var provider = CreateProvider(TestKey, "v1");
        var jsonContent = """{"url":"https://storage.example.com/audio.mp3","expiresAt":"2024-01-15T12:00:00Z","contentType":"audio/mpeg","fileSizeBytes":5000000}""";

        // Act
        var encrypted = await provider.EncryptAsync(jsonContent);
        var decrypted = await provider.DecryptAsync(encrypted, "v1");

        // Assert
        decrypted.ShouldBe(jsonContent);
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static AesGcmCacheEncryptionProvider CreateProvider(string? keySecret, string keyVersion)
    {
        var options = new CacheEncryptionOptions
        {
            KeySecret = keySecret,
            KeyVersion = keyVersion,
            Enabled = true
        };

        return new AesGcmCacheEncryptionProvider(
            Options.Create(options),
            NullLogger<AesGcmCacheEncryptionProvider>.Instance);
    }
}
