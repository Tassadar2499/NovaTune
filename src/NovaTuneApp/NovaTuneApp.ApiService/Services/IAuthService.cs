using NovaTuneApp.ApiService.Models.Auth;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Interface for authentication operations.
/// </summary>
public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? deviceId, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, string? deviceId, CancellationToken ct);
    Task LogoutAsync(string userId, string refreshToken, CancellationToken ct);
    Task LogoutAllAsync(string userId, CancellationToken ct);
}
