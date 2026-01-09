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

    /// <summary>
    /// Upload session management configuration.
    /// </summary>
    public UploadSessionOptions UploadSession { get; set; } = new();

    /// <summary>
    /// MinIO/S3 storage configuration.
    /// </summary>
    public MinioOptions Minio { get; set; } = new();

    /// <summary>
    /// Outbox processor configuration for reliable event publication.
    /// </summary>
    public OutboxProcessorOptions OutboxProcessor { get; set; } = new();
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

/// <summary>
/// Upload session management configuration options.
/// </summary>
public class UploadSessionOptions
{
    /// <summary>
    /// Default session TTL in minutes (aligned with presigned URL expiry).
    /// </summary>
    [Range(1, 60, ErrorMessage = "DefaultTtlMinutes must be between 1 and 60")]
    public int DefaultTtlMinutes { get; set; } = 15;

    /// <summary>
    /// Interval between cleanup runs in minutes.
    /// </summary>
    [Range(1, 60, ErrorMessage = "CleanupIntervalMinutes must be between 1 and 60")]
    public int CleanupIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Hours to retain expired sessions before deletion.
    /// </summary>
    [Range(1, 168, ErrorMessage = "RetentionHours must be between 1 and 168 (1 week)")]
    public int RetentionHours { get; set; } = 24;

    /// <summary>
    /// Maximum sessions to process per cleanup batch.
    /// </summary>
    [Range(10, 1000, ErrorMessage = "CleanupBatchSize must be between 10 and 1000")]
    public int CleanupBatchSize { get; set; } = 100;

    public TimeSpan DefaultTtl => TimeSpan.FromMinutes(DefaultTtlMinutes);
    public TimeSpan CleanupInterval => TimeSpan.FromMinutes(CleanupIntervalMinutes);
    public TimeSpan RetentionPeriod => TimeSpan.FromHours(RetentionHours);
}

/// <summary>
/// MinIO/S3 storage configuration options.
/// </summary>
public class MinioOptions
{
    /// <summary>
    /// Environment prefix for bucket naming (e.g., "dev", "staging", "prod").
    /// Bucket name will be "{EnvironmentPrefix}-audio-uploads".
    /// </summary>
    [Required(ErrorMessage = "EnvironmentPrefix is required")]
    [MinLength(1, ErrorMessage = "EnvironmentPrefix cannot be empty")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "EnvironmentPrefix must contain only lowercase letters, numbers, and hyphens")]
    public string EnvironmentPrefix { get; set; } = "dev";

    /// <summary>
    /// Audio uploads bucket name suffix.
    /// </summary>
    public string AudioBucketSuffix { get; set; } = "audio-uploads";

    /// <summary>
    /// Whether to enable bucket versioning for DR (NF-6.5).
    /// </summary>
    public bool EnableVersioning { get; set; } = true;

    /// <summary>
    /// Hours to retain incomplete multipart uploads before cleanup.
    /// </summary>
    [Range(1, 168, ErrorMessage = "IncompleteUploadRetentionHours must be between 1 and 168")]
    public int IncompleteUploadRetentionHours { get; set; } = 24;

    /// <summary>
    /// Gets the full audio bucket name.
    /// </summary>
    public string AudioBucketName => $"{EnvironmentPrefix}-{AudioBucketSuffix}";

    /// <summary>
    /// Kafka topic for MinIO bucket notifications.
    /// </summary>
    public string EventTopicName => $"{EnvironmentPrefix}-minio-events";
}

/// <summary>
/// Outbox processor configuration options (NF-5.2).
/// </summary>
public class OutboxProcessorOptions
{
    /// <summary>
    /// Interval between polling for pending messages in milliseconds.
    /// </summary>
    [Range(100, 60000, ErrorMessage = "PollingIntervalMs must be between 100 and 60000")]
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Maximum number of messages to process per batch.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "BatchSize must be between 1 and 1000")]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum retry attempts before marking a message as failed.
    /// </summary>
    [Range(1, 20, ErrorMessage = "MaxRetries must be between 1 and 20")]
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Initial delay in milliseconds for exponential backoff.
    /// </summary>
    [Range(100, 30000, ErrorMessage = "InitialBackoffMs must be between 100 and 30000")]
    public int InitialBackoffMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay in milliseconds for exponential backoff.
    /// </summary>
    [Range(1000, 300000, ErrorMessage = "MaxBackoffMs must be between 1000 and 300000")]
    public int MaxBackoffMs { get; set; } = 60000;

    public TimeSpan PollingInterval => TimeSpan.FromMilliseconds(PollingIntervalMs);
    public TimeSpan InitialBackoff => TimeSpan.FromMilliseconds(InitialBackoffMs);
    public TimeSpan MaxBackoff => TimeSpan.FromMilliseconds(MaxBackoffMs);
}
