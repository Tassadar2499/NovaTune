using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Configuration options for admin/moderation functionality (Stage 8).
/// </summary>
public class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// Maximum users per page in admin list.
    /// </summary>
    [Range(1, 100)]
    public int MaxUserPageSize { get; set; } = 100;

    /// <summary>
    /// Default users per page in admin list.
    /// </summary>
    [Range(1, 100)]
    public int DefaultUserPageSize { get; set; } = 50;

    /// <summary>
    /// Maximum tracks per page in admin list.
    /// </summary>
    [Range(1, 100)]
    public int MaxTrackPageSize { get; set; } = 100;

    /// <summary>
    /// Default tracks per page in admin list.
    /// </summary>
    [Range(1, 100)]
    public int DefaultTrackPageSize { get; set; } = 50;

    /// <summary>
    /// Maximum audit log entries per page.
    /// </summary>
    [Range(1, 100)]
    public int MaxAuditPageSize { get; set; } = 100;

    /// <summary>
    /// Default audit log entries per page.
    /// </summary>
    [Range(1, 100)]
    public int DefaultAuditPageSize { get; set; } = 50;

    /// <summary>
    /// Audit log retention period in days (default: 365).
    /// </summary>
    [Range(1, 3650)]
    public int AuditRetentionDays { get; set; } = 365;

    /// <summary>
    /// Enable audit log integrity verification.
    /// </summary>
    public bool EnableIntegrityVerification { get; set; } = true;

    /// <summary>
    /// Days of analytics data to include in overview.
    /// </summary>
    [Range(1, 365)]
    public int AnalyticsOverviewDays { get; set; } = 1;

    /// <summary>
    /// Default storage quota for users in bytes (10 GB).
    /// </summary>
    public long DefaultStorageQuotaBytes { get; set; } = 10L * 1024 * 1024 * 1024;
}
