using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Identity;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Auth;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Auth;

/// <summary>
/// Unit tests for AuthService business logic.
/// Tests registration, login, refresh token rotation, and session management.
/// </summary>
public class AuthServiceTests
{
    private readonly IUserStore<ApplicationUser> _userStore;
    private readonly IUserEmailStore<ApplicationUser> _emailStore;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        // Create a substitute that implements both interfaces
        _userStore = Substitute.For<IUserStore<ApplicationUser>, IUserEmailStore<ApplicationUser>>();
        _emailStore = (IUserEmailStore<ApplicationUser>)_userStore;
        _passwordHasher = Substitute.For<IPasswordHasher<ApplicationUser>>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _tokenService = Substitute.For<ITokenService>();
        _logger = Substitute.For<ILogger<AuthService>>();

        var sessionSettings = Options.Create(new SessionSettings
        {
            MaxConcurrentSessions = 5
        });

        _authService = new AuthService(
            _userStore,
            _passwordHasher,
            _refreshTokenRepository,
            _tokenService,
            sessionSettings,
            _logger);
    }

    // ========================================================================
    // Registration Tests
    // ========================================================================

    [Fact]
    public async Task RegisterAsync_Should_create_user_with_active_status()
    {
        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");
        ApplicationUser? capturedUser = null;

        _emailStore.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ApplicationUser?)null);

        _passwordHasher.HashPassword(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns("hashed-password");

        _userStore.CreateAsync(Arg.Do<ApplicationUser>(u => capturedUser = u), Arg.Any<CancellationToken>())
            .Returns(IdentityResult.Success);

        await _authService.RegisterAsync(request, CancellationToken.None);

        capturedUser.ShouldNotBeNull();
        capturedUser.Status.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task RegisterAsync_Should_assign_listener_role()
    {
        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");
        ApplicationUser? capturedUser = null;

        _emailStore.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ApplicationUser?)null);

        _passwordHasher.HashPassword(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns("hashed-password");

        _userStore.CreateAsync(Arg.Do<ApplicationUser>(u => capturedUser = u), Arg.Any<CancellationToken>())
            .Returns(IdentityResult.Success);

        await _authService.RegisterAsync(request, CancellationToken.None);

        capturedUser.ShouldNotBeNull();
        capturedUser.Roles.ShouldContain("Listener");
    }

    [Fact]
    public async Task RegisterAsync_Should_hash_password()
    {
        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");

        _emailStore.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ApplicationUser?)null);

        _passwordHasher.HashPassword(Arg.Any<ApplicationUser>(), "SecurePassword123!")
            .Returns("hashed-password");

        _userStore.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(IdentityResult.Success);

        await _authService.RegisterAsync(request, CancellationToken.None);

        _passwordHasher.Received(1).HashPassword(Arg.Any<ApplicationUser>(), "SecurePassword123!");
    }

    [Fact]
    public async Task RegisterAsync_Should_throw_when_email_exists()
    {
        var request = new RegisterRequest("existing@example.com", "Test User", "SecurePassword123!");
        var existingUser = new ApplicationUser { UserId = "existing-id", Email = "existing@example.com" };

        _emailStore.FindByEmailAsync("EXISTING@EXAMPLE.COM", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.RegisterAsync(request, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.EmailExists);
        exception.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task RegisterAsync_Should_return_user_response()
    {
        var request = new RegisterRequest("test@example.com", "Test User", "SecurePassword123!");

        _emailStore.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ApplicationUser?)null);

        _passwordHasher.HashPassword(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns("hashed-password");

        _userStore.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(IdentityResult.Success);

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
        var request = new LoginRequest("test@example.com", "SecurePassword123!");
        var user = CreateActiveUser();

        SetupSuccessfulLogin(user);

        var result = await _authService.LoginAsync(request, "device-1", CancellationToken.None);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-token");
        result.RefreshToken.ShouldBe("refresh-token");
    }

    [Fact]
    public async Task LoginAsync_Should_throw_for_nonexistent_user()
    {
        var request = new LoginRequest("nonexistent@example.com", "password");

        _emailStore.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ApplicationUser?)null);

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.LoginAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.InvalidCredentials);
        exception.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task LoginAsync_Should_throw_for_invalid_password()
    {
        var request = new LoginRequest("test@example.com", "wrong-password");
        var user = CreateActiveUser();

        _emailStore.FindByEmailAsync("TEST@EXAMPLE.COM", Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "wrong-password")
            .Returns(PasswordVerificationResult.Failed);

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.LoginAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.InvalidCredentials);
        exception.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task LoginAsync_Should_throw_for_disabled_user()
    {
        var request = new LoginRequest("test@example.com", "SecurePassword123!");
        var user = CreateActiveUser();
        user.Status = UserStatus.Disabled;

        _emailStore.FindByEmailAsync("TEST@EXAMPLE.COM", Arg.Any<CancellationToken>())
            .Returns(user);

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.LoginAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.AccountDisabled);
        exception.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task LoginAsync_Should_store_refresh_token_hash()
    {
        var request = new LoginRequest("test@example.com", "SecurePassword123!");
        var user = CreateActiveUser();

        SetupSuccessfulLogin(user);
        _tokenService.HashRefreshToken("refresh-token").Returns("hashed-refresh-token");

        await _authService.LoginAsync(request, "device-1", CancellationToken.None);

        await _refreshTokenRepository.Received(1).CreateAsync(
            user.UserId,
            "hashed-refresh-token",
            Arg.Any<DateTime>(),
            "device-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_Should_update_last_login_timestamp()
    {
        var request = new LoginRequest("test@example.com", "SecurePassword123!");
        var user = CreateActiveUser();
        user.LastLoginAt = null;

        SetupSuccessfulLogin(user);

        await _authService.LoginAsync(request, null, CancellationToken.None);

        user.LastLoginAt.ShouldNotBeNull();
        await _userStore.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_Should_evict_oldest_session_when_limit_reached()
    {
        var request = new LoginRequest("test@example.com", "SecurePassword123!");
        var user = CreateActiveUser();

        SetupSuccessfulLogin(user);
        _refreshTokenRepository.GetActiveCountForUserAsync(user.UserId, Arg.Any<CancellationToken>())
            .Returns(5); // At limit

        await _authService.LoginAsync(request, null, CancellationToken.None);

        await _refreshTokenRepository.Received(1).RevokeOldestForUserAsync(user.UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_Should_not_evict_session_when_under_limit()
    {
        var request = new LoginRequest("test@example.com", "SecurePassword123!");
        var user = CreateActiveUser();

        SetupSuccessfulLogin(user);
        _refreshTokenRepository.GetActiveCountForUserAsync(user.UserId, Arg.Any<CancellationToken>())
            .Returns(3); // Under limit

        await _authService.LoginAsync(request, null, CancellationToken.None);

        await _refreshTokenRepository.DidNotReceive().RevokeOldestForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ========================================================================
    // Refresh Token Tests
    // ========================================================================

    [Fact]
    public async Task RefreshAsync_Should_rotate_tokens()
    {
        var request = new RefreshRequest("old-refresh-token");
        var user = CreateActiveUser();
        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = user.UserId,
            TokenHash = "hashed-old-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _tokenService.HashRefreshToken("old-refresh-token").Returns("hashed-old-token");
        _refreshTokenRepository.FindByHashAsync("hashed-old-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userStore.FindByIdAsync(user.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tokenService.GenerateAccessToken(user).Returns("new-access-token");
        _tokenService.GenerateRefreshToken().Returns("new-refresh-token");
        _tokenService.HashRefreshToken("new-refresh-token").Returns("hashed-new-token");
        _tokenService.GetRefreshTokenExpiration().Returns(DateTime.UtcNow.AddHours(1));
        _tokenService.GetAccessTokenExpirationSeconds().Returns(900);

        var result = await _authService.RefreshAsync(request, null, CancellationToken.None);

        result.AccessToken.ShouldBe("new-access-token");
        result.RefreshToken.ShouldBe("new-refresh-token");
    }

    [Fact]
    public async Task RefreshAsync_Should_revoke_old_token()
    {
        var request = new RefreshRequest("old-refresh-token");
        var user = CreateActiveUser();
        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = user.UserId,
            TokenHash = "hashed-old-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        SetupSuccessfulRefresh(user, storedToken);

        await _authService.RefreshAsync(request, null, CancellationToken.None);

        await _refreshTokenRepository.Received(1).RevokeAsync("token-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_Should_throw_for_invalid_token()
    {
        var request = new RefreshRequest("invalid-token");

        _tokenService.HashRefreshToken("invalid-token").Returns("hashed-invalid");
        _refreshTokenRepository.FindByHashAsync("hashed-invalid", Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var exception = await Should.ThrowAsync<AuthException>(
            () => _authService.RefreshAsync(request, null, CancellationToken.None));

        exception.ErrorType.ShouldBe(AuthErrorType.InvalidToken);
        exception.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task RefreshAsync_Should_throw_for_disabled_user()
    {
        var request = new RefreshRequest("refresh-token");
        var user = CreateActiveUser();
        user.Status = UserStatus.Disabled;
        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = user.UserId,
            TokenHash = "hashed-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _tokenService.HashRefreshToken("refresh-token").Returns("hashed-token");
        _refreshTokenRepository.FindByHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userStore.FindByIdAsync(user.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

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
        var refreshToken = "refresh-token";
        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = userId,
            TokenHash = "hashed-token"
        };

        _tokenService.HashRefreshToken(refreshToken).Returns("hashed-token");
        _refreshTokenRepository.FindByHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);

        await _authService.LogoutAsync(userId, refreshToken, CancellationToken.None);

        await _refreshTokenRepository.Received(1).RevokeAsync("token-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_Should_not_revoke_if_token_not_found()
    {
        var userId = "user-id";
        var refreshToken = "invalid-token";

        _tokenService.HashRefreshToken(refreshToken).Returns("hashed-invalid");
        _refreshTokenRepository.FindByHashAsync("hashed-invalid", Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        await _authService.LogoutAsync(userId, refreshToken, CancellationToken.None);

        await _refreshTokenRepository.DidNotReceive().RevokeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_Should_not_revoke_if_userId_mismatch()
    {
        var userId = "user-id";
        var refreshToken = "refresh-token";
        var storedToken = new RefreshToken
        {
            Id = "token-id",
            UserId = "different-user-id",
            TokenHash = "hashed-token"
        };

        _tokenService.HashRefreshToken(refreshToken).Returns("hashed-token");
        _refreshTokenRepository.FindByHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(storedToken);

        await _authService.LogoutAsync(userId, refreshToken, CancellationToken.None);

        await _refreshTokenRepository.DidNotReceive().RevokeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAllAsync_Should_revoke_all_tokens_for_user()
    {
        var userId = "user-id";

        await _authService.LogoutAllAsync(userId, CancellationToken.None);

        await _refreshTokenRepository.Received(1).RevokeAllForUserAsync(userId, Arg.Any<CancellationToken>());
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
            PasswordHash = "hashed-password",
            Status = UserStatus.Active,
            Roles = ["Listener"]
        };
    }

    private void SetupSuccessfulLogin(ApplicationUser user)
    {
        _emailStore.FindByEmailAsync("TEST@EXAMPLE.COM", Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Arg.Any<string>())
            .Returns(PasswordVerificationResult.Success);

        _refreshTokenRepository.GetActiveCountForUserAsync(user.UserId, Arg.Any<CancellationToken>())
            .Returns(0);

        _tokenService.GenerateAccessToken(user).Returns("access-token");
        _tokenService.GenerateRefreshToken().Returns("refresh-token");
        _tokenService.HashRefreshToken("refresh-token").Returns("hashed-refresh-token");
        _tokenService.GetRefreshTokenExpiration().Returns(DateTime.UtcNow.AddHours(1));
        _tokenService.GetAccessTokenExpirationSeconds().Returns(900);

        _userStore.UpdateAsync(user, Arg.Any<CancellationToken>())
            .Returns(IdentityResult.Success);
    }

    private void SetupSuccessfulRefresh(ApplicationUser user, RefreshToken storedToken)
    {
        _tokenService.HashRefreshToken(Arg.Any<string>()).Returns(storedToken.TokenHash);
        _refreshTokenRepository.FindByHashAsync(storedToken.TokenHash, Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userStore.FindByIdAsync(user.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tokenService.GenerateAccessToken(user).Returns("new-access-token");
        _tokenService.GenerateRefreshToken().Returns("new-refresh-token");
        _tokenService.HashRefreshToken("new-refresh-token").Returns("hashed-new-token");
        _tokenService.GetRefreshTokenExpiration().Returns(DateTime.UtcNow.AddHours(1));
        _tokenService.GetAccessTokenExpirationSeconds().Returns(900);
    }
}
