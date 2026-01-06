using System.Text;

namespace NovaTuneApp.ApiService.Infrastructure.Identity;

/// <summary>
/// Interface for accessing the validated JWT signing key.
/// </summary>
public interface IJwtKeyProvider
{
    /// <summary>
    /// Gets the validated JWT signing key bytes.
    /// </summary>
    byte[] SigningKey { get; }
}

/// <summary>
/// Provides centralized JWT signing key management.
/// Validates key requirements at startup and provides a single source of truth.
/// </summary>
public class JwtKeyProvider : IJwtKeyProvider
{
    private const int MinKeyLengthBytes = 32; // 256 bits

    public byte[] SigningKey { get; }

    public JwtKeyProvider(IConfiguration configuration)
    {
        var keyString = configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY environment variable not configured");

        if (keyString.Length < MinKeyLengthBytes)
            throw new InvalidOperationException($"JWT_SIGNING_KEY must be at least {MinKeyLengthBytes} characters (256 bits)");

        SigningKey = Encoding.UTF8.GetBytes(keyString);
    }
}
