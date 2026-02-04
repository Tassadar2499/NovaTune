using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Identity;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// JWT token service implementation (Req 1.2).
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly IJwtKeyProvider _keyProvider;

    public TokenService(IOptions<JwtSettings> settings, IJwtKeyProvider keyProvider)
    {
        _settings = settings.Value;
        _keyProvider = keyProvider;
    }

    public string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("status", user.Status.ToString())
        };

        // Add role claims
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim("roles", role.ToLowerInvariant()));
        }

        // Add permission claims
        foreach (var permission in user.Permissions)
        {
            claims.Add(new Claim("permissions", permission));
        }

        var key = new SymmetricSecurityKey(_keyProvider.SigningKey);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32]; // 256 bits
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashRefreshToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public int GetAccessTokenExpirationSeconds() =>
        _settings.AccessTokenExpirationMinutes * 60;

    public DateTime GetRefreshTokenExpiration() =>
        DateTime.UtcNow.AddMinutes(_settings.RefreshTokenExpirationMinutes);
}
