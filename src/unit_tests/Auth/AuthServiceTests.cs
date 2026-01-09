using Microsoft.Extensions.DependencyInjection;
using NovaTune.UnitTests.Fakes;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Auth;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Auth;

/// <summary>
/// Unit tests for AuthService business logic.
/// Tests registration, login, refresh token rotation, and session management.
/// </summary>
public class AuthServiceTests : BaseTest
{
    private readonly UserStoreFake _userStoreFake;
    private readonly PasswordHasherFake _passwordHasherFake;
    private readonly RefreshTokenRepositoryFake _refreshTokenRepositoryFake;
    private readonly TokenServiceFake _tokenServiceFake;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userStoreFake = ServiceProvider.GetRequiredService<UserStoreFake>();
        _passwordHasherFake = ServiceProvider.GetRequiredService<PasswordHasherFake>();
        _refreshTokenRepositoryFake = ServiceProvider.GetRequiredService<RefreshTokenRepositoryFake>();
        _tokenServiceFake = ServiceProvider.GetRequiredService<TokenServiceFake>();
        _authService = ServiceProvider.GetRequiredService<AuthService>();
    }

    // ========================================================================
    // Registration Tests
    // ========================================================================

    [Fact]
    public async Task RegisterAsync_Should_create_user_with_active_status()
    {
        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");

        await _authService.RegisterAsync(request, CancellationToken.None);

        var storedUser = _userStoreFake.Users.Values.FirstOrDefault(u => u.Email == "test@example.com");
        storedUser.ShouldNotBeNull();
        storedUser.Status.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task RegisterAsync_Should_assign_listener_role()
    {
        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");

        await _authService.RegisterAsync(request, CancellationToken.None);

        var storedUser = _userStoreFake.Users.Values.FirstOrDefault(u => u.Email == "test@example.com");
        storedUser.ShouldNotBeNull();
        storedUser.Roles.ShouldContain("Listener");
    }

    [Fact]
    public async Task RegisterAsync_Should_hash_password()
    {
        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");

        await _authService.RegisterAsync(request, CancellationToken.None);

        var storedUser = _userStoreFake.Users.Values.FirstOrDefault(u => u.Email == "test@example.com");
        storedUser.ShouldNotBeNull();
        storedUser.PasswordHash.ShouldBe($"{PasswordHasherFake.DefaultHashPrefix}SecurePassword123!");
    }

    [Fact]
    public async Task RegisterAsync_Should_throw_when_email_exists()
    {
        var existingUser = CreateActiveUser();
        _userStoreFake.Users[existingUser.UserId] = existingUser;

        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.RegisterAsync(request, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.EmailExists);
        exception.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task RegisterAsync_Should_return_user_response()
    {
        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");

        var result = await _authService.RegisterAsync(request, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Email.ShouldBe("test@example.com");
        result.DisplayName.ShouldBe("Test User");
    }

    // ========================================================================
    // Login Tests
    // ========================================================================

    [Fact]
    public async Task LoginAsync_Should_return_tokens_for_valid_credentials()
    {
        var user = CreateActiveUser();
        _userStoreFake.Users[user.UserId] = user;

        var request = new LoginRequest("test@example.com", "password");

        var result = await _authService.LoginAsync(request, "device-1", CancellationToken.None);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe(TokenServiceFake.DefaultAccessToken);
        result.RefreshToken.ShouldBe(TokenServiceFake.DefaultRefreshToken);
    }

    [Fact]
    public async Task LoginAsync_Should_throw_for_nonexistent_user()
    {
        var request = new LoginRequest("nonexistent@example.com", "password");

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.LoginAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.InvalidCredentials);
        exception.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task LoginAsync_Should_throw_for_invalid_password()
    {
        var user = CreateActiveUser();
        _userStoreFake.Users[user.UserId] = user;

        _passwordHasherFake.OnVerifyHashedPassword = (_, _, _) => Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;

        var request = new LoginRequest("test@example.com", "wrong-password");

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.LoginAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.InvalidCredentials);
        exception.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task LoginAsync_Should_throw_for_disabled_user()
    {
        var user = CreateActiveUser();
        user.Status = UserStatus.Disabled;
        _userStoreFake.Users[user.UserId] = user;

        var request = new LoginRequest("test@example.com", "password");

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.LoginAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.AccountDisabled);
        exception.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task LoginAsync_Should_store_refresh_token_hash()
    {
        var user = CreateActiveUser();
        _userStoreFake.Users[user.UserId] = user;

        var request = new LoginRequest("test@example.com", "password");

        await _authService.LoginAsync(request, "device-1", CancellationToken.None);

        var storedToken = _refreshTokenRepositoryFake.Tokens.FirstOrDefault(t => t.UserId == user.UserId);
        storedToken.ShouldNotBeNull();
        storedToken.TokenHash.ShouldBe($"{TokenServiceFake.DefaultHashPrefix}{TokenServiceFake.DefaultRefreshToken}");
        storedToken.DeviceIdentifier.ShouldBe("device-1");
    }

    [Fact]
    public async Task LoginAsync_Should_update_last_login_timestamp()
    {
        var user = CreateActiveUser();
        user.LastLoginAt = null;
        _userStoreFake.Users[user.UserId] = user;

        var request = new LoginRequest("test@example.com", "password");

        await _authService.LoginAsync(request, null, CancellationToken.None);

        var updatedUser = _userStoreFake.Users[user.UserId];
        updatedUser.LastLoginAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task LoginAsync_Should_evict_oldest_session_when_limit_reached()
    {
        var user = CreateActiveUser();
        _userStoreFake.Users[user.UserId] = user;

        // Add 5 existing tokens (at limit)
        for (var i = 0; i < 5; i++)
        {
            _refreshTokenRepositoryFake.Tokens.Add(new RefreshToken
            {
                Id = $"token-{i}",
                UserId = user.UserId,
                TokenHash = $"hash-{i}",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow.AddMinutes(-i) // Older tokens created earlier
            });
        }

        var request = new LoginRequest("test@example.com", "password");

        await _authService.LoginAsync(request, null, CancellationToken.None);

        // The oldest token (token-4) should be revoked
        var oldestToken = _refreshTokenRepositoryFake.Tokens.First(t => t.Id == "token-4");
        oldestToken.IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public async Task LoginAsync_Should_not_evict_session_when_under_limit()
    {
        var user = CreateActiveUser();
        _userStoreFake.Users[user.UserId] = user;

        // Add 3 existing tokens (under limit)
        for (var i = 0; i < 3; i++)
        {
            _refreshTokenRepositoryFake.Tokens.Add(new RefreshToken
            {
                Id = $"token-{i}",
                UserId = user.UserId,
                TokenHash = $"hash-{i}",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        var request = new LoginRequest("test@example.com", "password");

        await _authService.LoginAsync(request, null, CancellationToken.None);

        // No existing tokens should be revoked (only the newly created one is tracked)
        _refreshTokenRepositoryFake.Tokens
            .Where(t => t.UserId == user.UserId && !t.TokenHash.Contains(TokenServiceFake.DefaultRefreshToken))
            .All(t => !t.IsRevoked)
            .ShouldBeTrue();
    }

    // ========================================================================
    // Refresh Token Tests
    // ========================================================================

    [Fact]
    public async Task RefreshAsync_Should_rotate_tokens()
    {
        var user = CreateActiveUser();
        _userStoreFake.Users[user.UserId] = user;

        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = user.UserId,
            TokenHash = $"{TokenServiceFake.DefaultHashPrefix}old-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        _refreshTokenRepositoryFake.Tokens.Add(storedToken);

        var request = new RefreshRequest("old-refresh-token");

        var result = await _authService.RefreshAsync(request, null, CancellationToken.None);

        result.AccessToken.ShouldBe(TokenServiceFake.DefaultAccessToken);
        result.RefreshToken.ShouldBe(TokenServiceFake.DefaultRefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_Should_revoke_old_token()
    {
        var user = CreateActiveUser();
        _userStoreFake.Users[user.UserId] = user;

        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = user.UserId,
            TokenHash = $"{TokenServiceFake.DefaultHashPrefix}old-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        _refreshTokenRepositoryFake.Tokens.Add(storedToken);

        var request = new RefreshRequest("old-refresh-token");

        await _authService.RefreshAsync(request, null, CancellationToken.None);

        storedToken.IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_Should_throw_for_invalid_token()
    {
        var request = new RefreshRequest("invalid-token");

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.RefreshAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.InvalidToken);
        exception.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task RefreshAsync_Should_throw_for_disabled_user()
    {
        var user = CreateActiveUser();
        user.Status = UserStatus.Disabled;
        _userStoreFake.Users[user.UserId] = user;

        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = user.UserId,
            TokenHash = $"{TokenServiceFake.DefaultHashPrefix}refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        _refreshTokenRepositoryFake.Tokens.Add(storedToken);

        var request = new RefreshRequest("refresh-token");

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.RefreshAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.AccountDisabled);
        exception.StatusCode.ShouldBe(403);
    }

    // ========================================================================
    // Logout Tests
    // ========================================================================

    [Fact]
    public async Task LogoutAsync_Should_revoke_token()
    {
        var userId = "user-id";
        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = userId,
            TokenHash = $"{TokenServiceFake.DefaultHashPrefix}refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        _refreshTokenRepositoryFake.Tokens.Add(storedToken);

        await _authService.LogoutAsync(userId, "refresh-token", CancellationToken.None);

        storedToken.IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public async Task LogoutAsync_Should_not_revoke_if_token_not_found()
    {
        var userId = "user-id";
        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = userId,
            TokenHash = "different-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        _refreshTokenRepositoryFake.Tokens.Add(storedToken);

        await _authService.LogoutAsync(userId, "invalid-token", CancellationToken.None);

        storedToken.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public async Task LogoutAsync_Should_not_revoke_if_userId_mismatch()
    {
        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = "different-user-id",
            TokenHash = $"{TokenServiceFake.DefaultHashPrefix}refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        _refreshTokenRepositoryFake.Tokens.Add(storedToken);

        await _authService.LogoutAsync("user-id", "refresh-token", CancellationToken.None);

        storedToken.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public async Task LogoutAllAsync_Should_revoke_all_tokens_for_user()
    {
        var userId = "user-id";
        for (var i = 0; i < 3; i++)
        {
            _refreshTokenRepositoryFake.Tokens.Add(new RefreshToken
            {
                Id = $"token-{i}",
                UserId = userId,
                TokenHash = $"hash-{i}",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
        }

        // Add token for different user (should not be revoked)
        _refreshTokenRepositoryFake.Tokens.Add(new RefreshToken
        {
            Id = "other-token",
            UserId = "other-user",
            TokenHash = "other-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });

        await _authService.LogoutAllAsync(userId, CancellationToken.None);

        _refreshTokenRepositoryFake.Tokens
            .Where(t => t.UserId == userId)
            .All(t => t.IsRevoked)
            .ShouldBeTrue();

        _refreshTokenRepositoryFake.Tokens
            .First(t => t.UserId == "other-user")
            .IsRevoked.ShouldBeFalse();
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static ApplicationUser CreateActiveUser()
    {
        return new ApplicationUser
        {
            UserId = "test-user-id",
            Email = "test@example.com",
            NormalizedEmail = "TEST@EXAMPLE.COM",
            DisplayName = "Test User",
            PasswordHash = $"{PasswordHasherFake.DefaultHashPrefix}password",
            Status = UserStatus.Active,
            Roles = ["Listener"]
        };
    }
}