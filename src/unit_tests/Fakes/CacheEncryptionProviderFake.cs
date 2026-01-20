using System.Text;
using NovaTuneApp.ApiService.Infrastructure.Caching;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Fake implementation of ICacheEncryptionProvider for unit tests.
/// Uses simple reversible encoding instead of real encryption.
/// </summary>
public class CacheEncryptionProviderFake : ICacheEncryptionProvider
{
    public const string DefaultKeyVersion = "test-v1";
    private const string EncryptedPrefix = "ENCRYPTED:";

    public string CurrentKeyVersion { get; set; } = DefaultKeyVersion;

    public bool ThrowOnEncrypt { get; set; }
    public bool ThrowOnDecrypt { get; set; }
    public bool ThrowOnKeyVersionMismatch { get; set; } = true;

    public List<string> EncryptedValues { get; } = [];
    public List<(byte[] Data, string KeyVersion)> DecryptedValues { get; } = [];

    public Task<byte[]> EncryptAsync(string plaintext, CancellationToken ct = default)
    {
        if (ThrowOnEncrypt)
        {
            throw new InvalidOperationException("Encryption failed");
        }

        EncryptedValues.Add(plaintext);

        // Simple "encryption" - just prefix and encode
        var encoded = $"{EncryptedPrefix}{plaintext}";
        return Task.FromResult(Encoding.UTF8.GetBytes(encoded));
    }

    public Task<string> DecryptAsync(byte[] ciphertext, string keyVersion, CancellationToken ct = default)
    {
        if (ThrowOnDecrypt)
        {
            throw new InvalidOperationException("Decryption failed");
        }

        if (ThrowOnKeyVersionMismatch && keyVersion != CurrentKeyVersion)
        {
            throw new System.Security.Cryptography.CryptographicException(
                $"Key version {keyVersion} not available");
        }

        DecryptedValues.Add((ciphertext, keyVersion));

        var encoded = Encoding.UTF8.GetString(ciphertext);
        if (!encoded.StartsWith(EncryptedPrefix))
        {
            throw new System.Security.Cryptography.CryptographicException("Invalid encrypted data");
        }

        return Task.FromResult(encoded[EncryptedPrefix.Length..]);
    }

    public void Clear()
    {
        EncryptedValues.Clear();
        DecryptedValues.Clear();
    }
}
