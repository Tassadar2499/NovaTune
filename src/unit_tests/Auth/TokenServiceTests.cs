using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using NovaTune.UnitTests.Fakes;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Auth;

/// <summary>
/// Unit tests for JWT token generation and refresh token handling (Req 1.2).
/// Tests claim generation, expiry, and cryptographic security.
/// </summary>
public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly ApplicationUser _testUser;

    public TokenServiceTests()
    {
        _jwtSettings = new JwtSettings
        {
            Issuer = "https://test.novatune.example",
            Audience = "novatune-test-api",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationMinutes = 60
        };

        var keyProvider = new JwtKeyProviderFake();

        _tokenService = new TokenService(Options.Create(_jwtSettings), keyProvider);

        _testUser = new ApplicationUser
        {
            UserId = "01HQ3K1234567890ABCDEF",
            Email = "test@example.com",
            DisplayName = "Test User",
            Status = UserStatus.Active,
            Roles = ["Listener"]
        };
    }

    // ========================================================================
    // Access Token Tests
    // ========================================================================

    [Fact]
    public void GenerateAccessToken_Should_return_valid_jwt()
    {
        var token = _tokenService.GenerateAccessToken(_testUser);

        token.ShouldNotBeNullOrWhiteSpace();

        // Should be a valid JWT format (header.payload.signature)
        var parts = token.Split('.');
        parts.Length.ShouldBe(3);
    }

    [Fact]
    public void GenerateAccessToken_Should_contain_sub_claim_with_userId()
    {
        var token = _tokenService.GenerateAccessToken(_testUser);
        var claims = DecodeToken(token);

        var subClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        subClaim.ShouldNotBeNull();
        subClaim.Value.ShouldBe(_testUser.UserId);
    }

    [Fact]
    public void GenerateAccessToken_Should_contain_email_claim()
    {
        var token = _tokenService.GenerateAccessToken(_testUser);
        var claims = DecodeToken(token);

        var emailClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        emailClaim.ShouldNotBeNull();
        emailClaim.Value.ShouldBe(_testUser.Email);
    }

    [Fact]
    public void GenerateAccessToken_Should_contain_jti_claim()
    {
        var token = _tokenService.GenerateAccessToken(_testUser);
        var claims = DecodeToken(token);

        var jtiClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        jtiClaim.ShouldNotBeNull();
        jtiClaim.Value.ShouldNotBeNullOrWhiteSpace();

        // jti should be a valid GUID
        Guid.TryParse(jtiClaim.Value, out _).ShouldBeTrue();
    }

    [Fact]
    public void GenerateAccessToken_Should_contain_status_claim()
    {
        var token = _tokenService.GenerateAccessToken(_testUser);
        var claims = DecodeToken(token);

        var statusClaim = claims.FirstOrDefault(c => c.Type == "status");
        statusClaim.ShouldNotBeNull();
        statusClaim.Value.ShouldBe("Active");
    }

    [Fact]
    public void GenerateAccessToken_Should_contain_roles_claim()
    {
        var token = _tokenService.GenerateAccessToken(_testUser);
        var claims = DecodeToken(token);

        var rolesClaims = claims.Where(c => c.Type == "roles").ToList();
        rolesClaims.ShouldNotBeEmpty();
        rolesClaims.ShouldContain(c => c.Value == "listener");
    }

    [Fact]
    public void GenerateAccessToken_Should_contain_multiple_roles()
    {
        var adminUser = new ApplicationUser
        {
            UserId = "admin-user-id",
            Email = "admin@example.com",
            DisplayName = "Admin User",
            Status = UserStatus.Active,
            Roles = ["Listener", "Admin"]
        };

        var token = _tokenService.GenerateAccessToken(adminUser);
        var claims = DecodeToken(token);

        var rolesClaims = claims.Where(c => c.Type == "roles").ToList();
        rolesClaims.Count.ShouldBe(2);
        rolesClaims.ShouldContain(c => c.Value == "listener");
        rolesClaims.ShouldContain(c => c.Value == "admin");
    }

    [Fact]
    public void GenerateAccessToken_Should_have_correct_issuer()
    {
        var token = _tokenService.GenerateAccessToken(_testUser);
        var claims = DecodeToken(token);

        var issClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iss);
        issClaim.ShouldNotBeNull();
        issClaim.Value.ShouldBe(_jwtSettings.Issuer);
    }

    [Fact]
    public void GenerateAccessToken_Should_have_correct_audience()
    {
        var token = _tokenService.GenerateAccessToken(_testUser);
        var claims = DecodeToken(token);

        var audClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Aud);
        audClaim.ShouldNotBeNull();
        audClaim.Value.ShouldBe(_jwtSettings.Audience);
    }

    [Fact]
    public void GenerateAccessToken_Should_have_correct_expiration()
    {
        var beforeGeneration = DateTime.UtcNow;
        var token = _tokenService.GenerateAccessToken(_testUser);
        var afterGeneration = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var expectedExpMin = beforeGeneration.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        var expectedExpMax = afterGeneration.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        jwtToken.ValidTo.ShouldBeGreaterThanOrEqualTo(expectedExpMin.AddSeconds(-1));
        jwtToken.ValidTo.ShouldBeLessThanOrEqualTo(expectedExpMax.AddSeconds(1));
    }

    [Fact]
    public void GenerateAccessToken_Should_produce_unique_jti_each_time()
    {
        var token1 = _tokenService.GenerateAccessToken(_testUser);
        var token2 = _tokenService.GenerateAccessToken(_testUser);

        var jti1 = DecodeToken(token1).First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = DecodeToken(token2).First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.ShouldNotBe(jti2);
    }

    // ========================================================================
    // Refresh Token Tests
    // ========================================================================

    [Fact]
    public void GenerateRefreshToken_Should_return_non_empty_string()
    {
        var token = _tokenService.GenerateRefreshToken();

        token.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateRefreshToken_Should_be_base64_encoded()
    {
        var token = _tokenService.GenerateRefreshToken();

        // Should be valid Base64
        var action = () => Convert.FromBase64String(token);
        action.ShouldNotThrow();
    }

    [Fact]
    public void GenerateRefreshToken_Should_be_256_bits()
    {
        var token = _tokenService.GenerateRefreshToken();
        var bytes = Convert.FromBase64String(token);

        bytes.Length.ShouldBe(32); // 256 bits = 32 bytes
    }

    [Fact]
    public void GenerateRefreshToken_Should_produce_unique_tokens()
    {
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        token1.ShouldNotBe(token2);
    }

    [Fact]
    public void GenerateRefreshToken_Should_be_cryptographically_random()
    {
        // Generate multiple tokens and verify they're all unique
        var tokens = Enumerable.Range(0, 100)
            .Select(_ => _tokenService.GenerateRefreshToken())
            .ToHashSet();

        tokens.Count.ShouldBe(100);
    }

    // ========================================================================
    // Token Hash Tests
    // ========================================================================

    [Fact]
    public void HashRefreshToken_Should_return_non_empty_hash()
    {
        var token = _tokenService.GenerateRefreshToken();

        var hash = _tokenService.HashRefreshToken(token);

        hash.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void HashRefreshToken_Should_be_deterministic()
    {
        var token = _tokenService.GenerateRefreshToken();

        var hash1 = _tokenService.HashRefreshToken(token);
        var hash2 = _tokenService.HashRefreshToken(token);

        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void HashRefreshToken_Should_produce_different_hashes_for_different_tokens()
    {
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        var hash1 = _tokenService.HashRefreshToken(token1);
        var hash2 = _tokenService.HashRefreshToken(token2);

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void HashRefreshToken_Should_return_base64_sha256()
    {
        var token = _tokenService.GenerateRefreshToken();

        var hash = _tokenService.HashRefreshToken(token);
        var hashBytes = Convert.FromBase64String(hash);

        // SHA-256 produces 32 bytes
        hashBytes.Length.ShouldBe(32);
    }

    // ========================================================================
    // Expiration Helper Tests
    // ========================================================================

    [Fact]
    public void GetAccessTokenExpirationSeconds_Should_return_correct_value()
    {
        var seconds = _tokenService.GetAccessTokenExpirationSeconds();

        seconds.ShouldBe(_jwtSettings.AccessTokenExpirationMinutes * 60);
    }

    [Fact]
    public void GetRefreshTokenExpiration_Should_return_future_datetime()
    {
        var before = DateTime.UtcNow;
        var expiration = _tokenService.GetRefreshTokenExpiration();
        var after = DateTime.UtcNow;

        var expectedMin = before.AddMinutes(_jwtSettings.RefreshTokenExpirationMinutes);
        var expectedMax = after.AddMinutes(_jwtSettings.RefreshTokenExpirationMinutes);

        expiration.ShouldBeGreaterThanOrEqualTo(expectedMin.AddSeconds(-1));
        expiration.ShouldBeLessThanOrEqualTo(expectedMax.AddSeconds(1));
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static IEnumerable<Claim> DecodeToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.Claims;
    }
}
