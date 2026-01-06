using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTuneApp.ApiService.Infrastructure.Identity;

/// <summary>
/// Interface for refresh token persistence operations.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Creates a new refresh token.
    /// </summary>
    Task<RefreshToken> CreateAsync(string userId, string tokenHash, DateTime expiresAt,
        string? deviceId, CancellationToken ct);

    /// <summary>
    /// Finds a valid (non-revoked, non-expired) token by its hash.
    /// </summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>
    /// Revokes a specific token by ID.
    /// </summary>
    Task RevokeAsync(string tokenId, CancellationToken ct);

    /// <summary>
    /// Revokes all active tokens for a user.
    /// </summary>
    Task RevokeAllForUserAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Gets the count of active (non-revoked, non-expired) tokens for a user.
    /// </summary>
    Task<int> GetActiveCountForUserAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Revokes the oldest active token for a user (FIFO eviction).
    /// </summary>
    Task RevokeOldestForUserAsync(string userId, CancellationToken ct);
}
