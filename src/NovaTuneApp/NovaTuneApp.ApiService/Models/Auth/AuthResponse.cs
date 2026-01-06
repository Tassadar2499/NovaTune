namespace NovaTuneApp.ApiService.Models.Auth;

/// <summary>
/// Successful authentication response containing tokens (Req 1.2).
/// </summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType = "Bearer");
