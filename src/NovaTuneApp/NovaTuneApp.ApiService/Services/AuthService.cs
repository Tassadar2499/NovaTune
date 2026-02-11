using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Identity;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Auth;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Authentication service implementation (Req 1.1, 1.2, 1.5).
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserStore<ApplicationUser> _userStore;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly SessionSettings _sessionSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserStore<ApplicationUser> userStore,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IOptions<SessionSettings> sessionSettings,
        ILogger<AuthService> logger)
    {
        _userStore = userStore;
        _passwordHasher = passwordHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _sessionSettings = sessionSettings.Value;
        _logger = logger;
    }

    public Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        return AuthInstrumentation.ExecuteWithInstrumentationAsync(
            "Register",
            () => RegisterCoreAsync(request, ct),
            activity => activity?.SetTag("user.email_domain", request.Email.Split('@').LastOrDefault()));
    }

    private async Task<UserResponse> RegisterCoreAsync(RegisterRequest request, CancellationToken ct)
    {
        // Check if email already exists
        var emailStore = (IUserEmailStore<ApplicationUser>)_userStore;
        var normalizedEmail = request.Email.ToUpperInvariant();
        var existingUser = await emailStore.FindByEmailAsync(normalizedEmail, ct);

        if (existingUser != null)
        {
            _logger.LogWarning("Registration attempt with existing email");
            NovaTuneMetrics.IncrementAuthOperation("register", "email_exists");
            throw new AuthException(AuthErrorType.EmailExists,
                "An account with this email already exists.", 409);
        }

        // Create new user
        var user = new ApplicationUser
        {
            Email = request.Email,
            NormalizedEmail = normalizedEmail,
            DisplayName = request.DisplayName,
            Status = UserStatus.Active,
            Roles = ["Listener"]
        };

        // Hash password
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        // Store user
        var result = await _userStore.CreateAsync(user, ct);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create user: {Errors}", errors);
            NovaTuneMetrics.IncrementAuthOperation("register", "validation_error");
            throw new AuthException(AuthErrorType.ValidationError, errors, 400);
        }

        _logger.LogInformation("User registered successfully: {UserId}", user.UserId);
        return new UserResponse(user.UserId, user.Email, user.DisplayName);
    }

    public Task<AuthResponse> LoginAsync(LoginRequest request, string? deviceId, CancellationToken ct)
    {
        return AuthInstrumentation.ExecuteWithInstrumentationAsync(
            "Login",
            () => LoginCoreAsync(request, deviceId, ct));
    }

    private async Task<AuthResponse> LoginCoreAsync(LoginRequest request, string? deviceId, CancellationToken ct)
    {
        // Find user by email
        var emailStore = (IUserEmailStore<ApplicationUser>)_userStore;
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await emailStore.FindByEmailAsync(normalizedEmail, ct);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent email");
            NovaTuneMetrics.IncrementAuthOperation("login", "invalid_credentials");
            throw new AuthException(AuthErrorType.InvalidCredentials,
                "The email or password provided is incorrect.", 401);
        }

        // Check user status (Req 1.3)
        if (user.Status == UserStatus.Disabled)
        {
            _logger.LogWarning("Login attempt for disabled account: {UserId}", user.UserId);
            NovaTuneMetrics.IncrementAuthOperation("login", "account_disabled");
            throw new AuthException(AuthErrorType.AccountDisabled,
                "This account has been disabled.", 403);
        }

        // Verify password
        var passwordResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Invalid password for user: {UserId}", user.UserId);
            NovaTuneMetrics.IncrementAuthOperation("login", "invalid_credentials");
            throw new AuthException(AuthErrorType.InvalidCredentials,
                "The email or password provided is incorrect.", 401);
        }

        // Check session limit and evict oldest if necessary
        var activeSessionCount = await _refreshTokenRepository.GetActiveCountForUserAsync(user.UserId, ct);
        if (activeSessionCount >= _sessionSettings.MaxConcurrentSessions)
        {
            _logger.LogInformation("Session limit reached for user {UserId}, evicting oldest session", user.UserId);
            await _refreshTokenRepository.RevokeOldestForUserAsync(user.UserId, ct);
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);

        // Store refresh token
        await _refreshTokenRepository.CreateAsync(
            user.UserId,
            refreshTokenHash,
            _tokenService.GetRefreshTokenExpiration(),
            deviceId,
            ct);

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userStore.UpdateAsync(user, ct);

        _logger.LogInformation("User logged in successfully: {UserId}", user.UserId);
        return new AuthResponse(
            accessToken,
            refreshToken,
            _tokenService.GetAccessTokenExpirationSeconds(),
            new AuthUserInfo(user.UserId, user.Email, user.DisplayName, user.Roles));
    }

    public Task<AuthResponse> RefreshAsync(RefreshRequest request, string? deviceId, CancellationToken ct)
    {
        return AuthInstrumentation.ExecuteWithInstrumentationAsync(
            "Refresh",
            () => RefreshCoreAsync(request, deviceId, ct));
    }

    private async Task<AuthResponse> RefreshCoreAsync(RefreshRequest request, string? deviceId, CancellationToken ct)
    {
        // Hash the provided token and find it
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.FindByHashAsync(tokenHash, ct);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh attempt with invalid or expired token");
            NovaTuneMetrics.IncrementAuthOperation("refresh", "invalid_token");
            throw new AuthException(AuthErrorType.InvalidToken,
                "The refresh token is invalid or has expired.", 401);
        }

        // Revoke the used token (one-time use rotation)
        await _refreshTokenRepository.RevokeAsync(storedToken.Id, ct);

        // Find user
        var user = await _userStore.FindByIdAsync(storedToken.UserId, ct);
        if (user == null)
        {
            _logger.LogError("Refresh token references non-existent user: {UserId}", storedToken.UserId);
            NovaTuneMetrics.IncrementAuthOperation("refresh", "invalid_token");
            throw new AuthException(AuthErrorType.InvalidToken,
                "The refresh token is invalid.", 401);
        }

        // Check user status
        if (user.Status == UserStatus.Disabled)
        {
            _logger.LogWarning("Refresh attempt for disabled account: {UserId}", user.UserId);
            NovaTuneMetrics.IncrementAuthOperation("refresh", "account_disabled");
            throw new AuthException(AuthErrorType.AccountDisabled,
                "This account has been disabled.", 403);
        }

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashRefreshToken(newRefreshToken);

        // Store new refresh token
        await _refreshTokenRepository.CreateAsync(
            user.UserId,
            newRefreshTokenHash,
            _tokenService.GetRefreshTokenExpiration(),
            deviceId ?? storedToken.DeviceIdentifier,
            ct);

        _logger.LogInformation("Tokens refreshed for user: {UserId}", user.UserId);
        return new AuthResponse(
            accessToken,
            newRefreshToken,
            _tokenService.GetAccessTokenExpirationSeconds(),
            new AuthUserInfo(user.UserId, user.Email, user.DisplayName, user.Roles));
    }

    public Task LogoutAsync(string userId, string refreshToken, CancellationToken ct)
    {
        return AuthInstrumentation.ExecuteWithInstrumentationAsync(
            "Logout",
            () => LogoutCoreAsync(userId, refreshToken, ct));
    }

    private async Task LogoutCoreAsync(string userId, string refreshToken, CancellationToken ct)
    {
        var tokenHash = _tokenService.HashRefreshToken(refreshToken);
        var storedToken = await _refreshTokenRepository.FindByHashAsync(tokenHash, ct);

        if (storedToken != null && storedToken.UserId == userId)
        {
            await _refreshTokenRepository.RevokeAsync(storedToken.Id, ct);
            _logger.LogInformation("User logged out: {UserId}", userId);
        }
    }

    public Task LogoutAllAsync(string userId, CancellationToken ct)
    {
        return AuthInstrumentation.ExecuteWithInstrumentationAsync(
            "LogoutAll",
            () => LogoutAllCoreAsync(userId, ct));
    }

    private async Task LogoutAllCoreAsync(string userId, CancellationToken ct)
    {
        await _refreshTokenRepository.RevokeAllForUserAsync(userId, ct);
        _logger.LogInformation("All sessions revoked for user: {UserId}", userId);
    }
}
