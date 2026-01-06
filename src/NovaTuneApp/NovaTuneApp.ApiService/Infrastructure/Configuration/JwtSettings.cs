namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// JWT authentication configuration settings (Req 1.2).
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Token issuer (e.g., "https://novatune.example").
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Token audience (e.g., "novatune-api").
    /// </summary>
    public required string Audience { get; set; }

    /// <summary>
    /// Access token TTL in minutes. Default: 15 minutes per Req 1.2.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token TTL in minutes. Default: 60 minutes (1 hour).
    /// </summary>
    public int RefreshTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Signing algorithm. Default: HS256 (symmetric).
    /// </summary>
    public string SigningAlgorithm { get; set; } = "HS256";
}
