using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Interface for JWT token generation and validation.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    string GenerateAccessToken(ApplicationUser user);

    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Computes SHA-256 hash of a refresh token for storage.
    /// </summary>
    string HashRefreshToken(string token);

    /// <summary>
    /// Gets the access token expiration time in seconds.
    /// </summary>
    int GetAccessTokenExpirationSeconds();

    /// <summary>
    /// Gets the refresh token expiration time.
    /// </summary>
    DateTime GetRefreshTokenExpiration();
}
