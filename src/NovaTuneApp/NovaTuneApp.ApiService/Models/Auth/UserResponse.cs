namespace NovaTuneApp.ApiService.Models.Auth;

/// <summary>
/// User information response after registration.
/// </summary>
public record UserResponse(
    string UserId,
    string Email,
    string DisplayName);
