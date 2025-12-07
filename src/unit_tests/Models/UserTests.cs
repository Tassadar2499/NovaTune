using System.ComponentModel.DataAnnotations;
using NovaTuneApp.ApiService.Models;

namespace NovaTune.UnitTests.Models;

public class UserTests
{
    [Fact]
    public void Should_pass_with_valid_data()
    {
        var user = new User
        {
            Id = "users/1",
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashed",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var results = ValidateModel(user);
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Should_fail_with_invalid_email()
    {
        var user = new User
        {
            Id = "users/1",
            Email = "invalid-email",
            DisplayName = "Test User",
            PasswordHash = "hashed"
        };

        var results = ValidateModel(user);
        results.ShouldContain(r => r.MemberNames.Contains(nameof(User.Email)));
    }

    [Fact]
    public void Should_fail_with_empty_display_name()
    {
        var user = new User
        {
            Id = "users/1",
            Email = "test@example.com",
            DisplayName = "",
            PasswordHash = "hashed"
        };

        var results = ValidateModel(user);
        results.ShouldContain(r => r.MemberNames.Contains(nameof(User.DisplayName)));
    }

    [Fact]
    public void Should_default_to_active_status()
    {
        var user = new User();
        user.Status.ShouldBe(UserStatus.Active);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }
}
