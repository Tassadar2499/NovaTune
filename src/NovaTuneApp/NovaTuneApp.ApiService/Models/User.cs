using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models;

public sealed class User
{
    public string Id { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
}
