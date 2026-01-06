namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Rate limiting configuration settings (Req 8.2, NF-2.5).
/// </summary>
public class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public AuthRateLimits Auth { get; set; } = new();
}

public class AuthRateLimits
{
    public RateLimitPolicy LoginPerIp { get; set; } = new(10, 1);
    public RateLimitPolicy LoginPerAccount { get; set; } = new(5, 1);
    public RateLimitPolicy RegisterPerIp { get; set; } = new(10, 1);
    public RateLimitPolicy RefreshPerIp { get; set; } = new(20, 1);
}

public record RateLimitPolicy(int PermitLimit, int WindowMinutes);
