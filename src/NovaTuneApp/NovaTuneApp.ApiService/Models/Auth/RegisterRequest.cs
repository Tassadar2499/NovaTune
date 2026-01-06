using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models.Auth;

/// <summary>
/// Request payload for user registration (Req 1.1).
/// </summary>
public record RegisterRequest(
    [Required][EmailAddress][MaxLength(255)] string Email,
    [Required][MinLength(2)][MaxLength(100)] string DisplayName,
    [Required][MinLength(1)] string Password);
