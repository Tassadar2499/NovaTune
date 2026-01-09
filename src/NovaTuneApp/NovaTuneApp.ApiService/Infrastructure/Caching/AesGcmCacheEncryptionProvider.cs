using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;

namespace NovaTuneApp.ApiService.Infrastructure.Caching;

/// <summary>
/// AES-256-GCM encryption provider for cache values (Req 10.3, NF-3.2).
/// </summary>
public class AesGcmCacheEncryptionProvider : ICacheEncryptionProvider
{
    private const int NonceSize = 12; // 96 bits for GCM
    private const int TagSize = 16; // 128 bits for GCM
    private const int KeySize = 32; // 256 bits

    private readonly byte[] _key;
    private readonly ILogger<AesGcmCacheEncryptionProvider> _logger;

    public string CurrentKeyVersion { get; }

    public AesGcmCacheEncryptionProvider(
        IOptions<CacheEncryptionOptions> options,
        ILogger<AesGcmCacheEncryptionProvider> logger)
    {
        _logger = logger;
        CurrentKeyVersion = options.Value.KeyVersion;

        // Derive key from configured secret
        var keySecret = options.Value.KeySecret;
        if (string.IsNullOrEmpty(keySecret))
        {
            // Generate a random key for development (not for production!)
            _logger.LogWarning(
                "Cache encryption key not configured, using random key. This is not suitable for production!");
            _key = RandomNumberGenerator.GetBytes(KeySize);
        }
        else
        {
            // Use HKDF to derive a proper key from the secret
            _key = DeriveKey(keySecret, CurrentKeyVersion);
        }
    }

    public Task<byte[]> EncryptAsync(string plaintext, CancellationToken ct = default)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Combine nonce + ciphertext + tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return Task.FromResult(result);
    }

    public Task<string> DecryptAsync(byte[] encryptedData, string keyVersion, CancellationToken ct = default)
    {
        if (encryptedData.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Invalid encrypted data: too short");
        }

        // Extract nonce, ciphertext, and tag
        var nonce = new byte[NonceSize];
        var ciphertextLength = encryptedData.Length - NonceSize - TagSize;
        var ciphertext = new byte[ciphertextLength];
        var tag = new byte[TagSize];

        Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(encryptedData, NonceSize, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(encryptedData, NonceSize + ciphertextLength, tag, 0, TagSize);

        // If key version doesn't match current, we'd need to look up the old key
        // For now, we only support the current key version
        if (keyVersion != CurrentKeyVersion)
        {
            _logger.LogWarning(
                "Attempted to decrypt with key version {RequestedVersion} but only {CurrentVersion} is available",
                keyVersion, CurrentKeyVersion);
            throw new CryptographicException($"Key version {keyVersion} not available");
        }

        var plaintextBytes = new byte[ciphertextLength];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return Task.FromResult(Encoding.UTF8.GetString(plaintextBytes));
    }

    private static byte[] DeriveKey(string secret, string keyVersion)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var salt = Encoding.UTF8.GetBytes($"novatune-cache-{keyVersion}");

        // Use HKDF to derive a proper key
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            secretBytes,
            KeySize,
            salt,
            info: Encoding.UTF8.GetBytes("cache-encryption"));
    }
}
