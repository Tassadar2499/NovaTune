namespace NovaTuneApp.ApiService.Models.Identity;

/// <summary>
/// Application user model for authentication backed by RavenDB.
/// </summary>
public class ApplicationUser
{
    /// <summary>
    /// RavenDB internal ID (format: "Users/{guid}").
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// External user identifier (ULID format).
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Normalized email for case-insensitive lookups.
    /// </summary>
    public string NormalizedEmail { get; set; } = null!;

    /// <summary>
    /// User's display name.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Argon2id password hash.
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// User account status.
    /// </summary>
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>
    /// User roles (e.g., "Listener", "Admin").
    /// </summary>
    public List<string> Roles { get; set; } = ["Listener"];

    /// <summary>
    /// Account creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last successful login timestamp.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
