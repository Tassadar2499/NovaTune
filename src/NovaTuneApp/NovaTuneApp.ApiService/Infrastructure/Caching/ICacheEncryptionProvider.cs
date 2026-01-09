namespace NovaTuneApp.ApiService.Infrastructure.Caching;

/// <summary>
/// Provides encryption/decryption for cache values (Req 10.3, NF-3.2).
/// Supports key versioning for rotation.
/// </summary>
public interface ICacheEncryptionProvider
{
    /// <summary>
    /// Gets the current key version identifier.
    /// </summary>
    string CurrentKeyVersion { get; }

    /// <summary>
    /// Encrypts plaintext using the current key.
    /// </summary>
    /// <param name="plaintext">Text to encrypt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Encrypted ciphertext.</returns>
    Task<byte[]> EncryptAsync(string plaintext, CancellationToken ct = default);

    /// <summary>
    /// Decrypts ciphertext using the specified key version.
    /// </summary>
    /// <param name="ciphertext">Encrypted data.</param>
    /// <param name="keyVersion">Key version used for encryption.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Decrypted plaintext.</returns>
    Task<string> DecryptAsync(byte[] ciphertext, string keyVersion, CancellationToken ct = default);
}
