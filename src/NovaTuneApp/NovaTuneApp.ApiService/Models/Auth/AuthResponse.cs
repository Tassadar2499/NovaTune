namespace NovaTuneApp.ApiService.Models.Auth;

/// <summary>
/// Successful authentication response containing tokens and user info (Req 1.2).
/// </summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    AuthUserInfo User,
    string TokenType = "Bearer");

/// <summary>
/// User information included in authentication responses.
/// </summary>
public record AuthUserInfo(
    string Id,
    string Email,
    string DisplayName,
    List<string> Roles);
