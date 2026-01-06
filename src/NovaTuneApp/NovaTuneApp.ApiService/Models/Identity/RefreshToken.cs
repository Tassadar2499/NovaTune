namespace NovaTuneApp.ApiService.Models.Identity;

/// <summary>
/// Refresh token model for session management backed by RavenDB.
/// Stores only hashed tokens per NF-3.2.
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// RavenDB internal ID (format: "RefreshTokens/{guid}").
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// References ApplicationUser.UserId.
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// SHA-256 hash of the token (never store plaintext).
    /// </summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// Optional device identifier for session management.
    /// </summary>
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Token creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Token expiration timestamp.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether this token has been revoked.
    /// </summary>
    public bool IsRevoked { get; set; }
}
