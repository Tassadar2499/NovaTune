using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models.Auth;

/// <summary>
/// Request payload for user login (Req 1.2).
/// </summary>
public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password);
