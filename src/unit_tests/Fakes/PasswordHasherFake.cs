using Microsoft.AspNetCore.Identity;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTune.UnitTests.Fakes;

public class PasswordHasherFake : IPasswordHasher<ApplicationUser>
{
    public const string DefaultHashPrefix = "hashed:";

    public Func<ApplicationUser, string, string>? OnHashPassword { get; set; }
    public Func<ApplicationUser, string, string, PasswordVerificationResult>? OnVerifyHashedPassword { get; set; }

    public string HashPassword(ApplicationUser user, string password)
    {
        if (OnHashPassword != null)
        {
            return OnHashPassword(user, password);
        }

        return $"{DefaultHashPrefix}{password}";
    }

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (OnVerifyHashedPassword != null)
        {
            return OnVerifyHashedPassword(user, hashedPassword, providedPassword);
        }

        var expectedHash = $"{DefaultHashPrefix}{providedPassword}";
        return hashedPassword == expectedHash
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
    }
}