using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Configuration options for track management API (Stage 5).
/// </summary>
public class TrackManagementOptions
{
    public const string SectionName = "TrackManagement";

    /// <summary>
    /// Grace period before physical deletion.
    /// Default: 30 days.
    /// Must be at least 1 minute.
    /// </summary>
    public TimeSpan DeletionGracePeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Maximum tracks returned per page.
    /// Default: 100.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "MaxPageSize must be between 1 and 1000")]
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// Default tracks returned per page.
    /// Default: 20.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "DefaultPageSize must be between 1 and 1000")]
    public int DefaultPageSize { get; set; } = 20;
}
