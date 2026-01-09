using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Fakes;

public class TokenServiceFake : ITokenService
{
    public const string DefaultAccessToken = "access-token";
    public const string DefaultRefreshToken = "refresh-token";
    public const string DefaultHashPrefix = "hashed:";
    public const int DefaultExpirationSeconds = 900;

    public Func<ApplicationUser, string>? OnGenerateAccessToken { get; set; }
    public Func<string>? OnGenerateRefreshToken { get; set; }
    public Func<string, string>? OnHashRefreshToken { get; set; }
    public Func<int>? OnGetAccessTokenExpirationSeconds { get; set; }
    public Func<DateTime>? OnGetRefreshTokenExpiration { get; set; }

    public string GenerateAccessToken(ApplicationUser user)
    {
        if (OnGenerateAccessToken != null)
        {
            return OnGenerateAccessToken(user);
        }

        return DefaultAccessToken;
    }

    public string GenerateRefreshToken()
    {
        if (OnGenerateRefreshToken != null)
        {
            return OnGenerateRefreshToken();
        }

        return DefaultRefreshToken;
    }

    public string HashRefreshToken(string token)
    {
        if (OnHashRefreshToken != null)
        {
            return OnHashRefreshToken(token);
        }

        return $"{DefaultHashPrefix}{token}";
    }

    public int GetAccessTokenExpirationSeconds()
    {
        if (OnGetAccessTokenExpirationSeconds != null)
        {
            return OnGetAccessTokenExpirationSeconds();
        }

        return DefaultExpirationSeconds;
    }

    public DateTime GetRefreshTokenExpiration()
    {
        if (OnGetRefreshTokenExpiration != null)
        {
            return OnGetRefreshTokenExpiration();
        }

        return DateTime.UtcNow.AddDays(7);
    }
}