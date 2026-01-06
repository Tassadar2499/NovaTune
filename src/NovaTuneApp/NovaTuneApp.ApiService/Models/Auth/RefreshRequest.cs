using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models.Auth;

/// <summary>
/// Request payload for token refresh.
/// </summary>
public record RefreshRequest(
    [Required] string RefreshToken);
