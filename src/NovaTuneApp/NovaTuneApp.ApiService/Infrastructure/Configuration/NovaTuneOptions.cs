using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Core NovaTune application configuration options.
/// Validated at startup per NF-5.1.
/// </summary>
public class NovaTuneOptions
{
    public const string SectionName = "NovaTune";

    /// <summary>
    /// Environment-based topic prefix for Kafka/Redpanda topics.
    /// Must be non-empty and match the deployment environment.
    /// </summary>
    [Required(ErrorMessage = "TopicPrefix is required")]
    [MinLength(1, ErrorMessage = "TopicPrefix cannot be empty")]
    public string TopicPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Presigned URL TTL for S3/MinIO object access.
    /// Must be positive and not exceed 1 hour.
    /// </summary>
    public PresignedUrlOptions PresignedUrl { get; set; } = new();

    /// <summary>
    /// Cache encryption settings.
    /// Required for non-development environments.
    /// </summary>
    public CacheEncryptionOptions CacheEncryption { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration.
    /// </summary>
    public RateLimitOptions RateLimit { get; set; } = new();

    /// <summary>
    /// Quota limits for user resources.
    /// </summary>
    public QuotaOptions Quotas { get; set; } = new();
}

/// <summary>
/// Presigned URL configuration options.
/// </summary>
public class PresignedUrlOptions
{
    /// <summary>
    /// Time-to-live for presigned URLs.
    /// Must be positive and not exceed 1 hour.
    /// </summary>
    [Range(1, 3600, ErrorMessage = "PresignedUrl TTL must be between 1 and 3600 seconds")]
    public int TtlSeconds { get; set; } = 300;

    public TimeSpan Ttl => TimeSpan.FromSeconds(TtlSeconds);
}

/// <summary>
/// Cache encryption configuration options.
/// </summary>
public class CacheEncryptionOptions
{
    /// <summary>
    /// Encryption key for cached data.
    /// Required in non-development environments with minimum 32 characters.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Whether encryption is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Minimum required key length for entropy requirements.
    /// </summary>
    public const int MinimumKeyLength = 32;
}

/// <summary>
/// Rate limiting configuration options.
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Maximum requests per minute for authenticated users.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "RequestsPerMinute must be a positive number")]
    public int RequestsPerMinute { get; set; } = 100;

    /// <summary>
    /// Maximum requests per minute for anonymous users.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "AnonymousRequestsPerMinute must be a positive number")]
    public int AnonymousRequestsPerMinute { get; set; } = 20;

    /// <summary>
    /// Maximum upload requests per hour per user.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "UploadsPerHour must be a positive number")]
    public int UploadsPerHour { get; set; } = 10;
}

/// <summary>
/// Quota configuration options for user resources.
/// </summary>
public class QuotaOptions
{
    /// <summary>
    /// Maximum file size for track uploads in bytes.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "MaxUploadSizeBytes must be a positive number")]
    public long MaxUploadSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB default

    /// <summary>
    /// Maximum number of playlists per user.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "MaxPlaylistsPerUser must be a positive number")]
    public int MaxPlaylistsPerUser { get; set; } = 50;

    /// <summary>
    /// Maximum number of tracks per playlist.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "MaxTracksPerPlaylist must be a positive number")]
    public int MaxTracksPerPlaylist { get; set; } = 500;

    /// <summary>
    /// Maximum total storage per user in bytes.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "MaxStoragePerUserBytes must be a positive number")]
    public long MaxStoragePerUserBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10 GB default
}
