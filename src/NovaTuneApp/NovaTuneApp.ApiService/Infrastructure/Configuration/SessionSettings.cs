namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Session management configuration settings.
/// </summary>
public class SessionSettings
{
    public const string SectionName = "Session";

    /// <summary>
    /// Maximum number of concurrent sessions per user.
    /// When exceeded, the oldest session is automatically revoked.
    /// Default: 5
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 5;
}
