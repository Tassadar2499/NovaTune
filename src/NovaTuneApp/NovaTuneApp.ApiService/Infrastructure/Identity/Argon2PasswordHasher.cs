using Isopoh.Cryptography.Argon2;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTuneApp.ApiService.Infrastructure.Identity;

/// <summary>
/// Argon2id password hasher for ASP.NET Identity.
/// Uses constant-time comparison for timing attack resistance.
/// </summary>
public class Argon2PasswordHasher : IPasswordHasher<ApplicationUser>
{
    private readonly Argon2Settings _settings;

    public Argon2PasswordHasher(IOptions<Argon2Settings> settings)
    {
        _settings = settings.Value;
    }

    public string HashPassword(ApplicationUser user, string password)
    {
        var config = new Argon2Config
        {
            Type = Argon2Type.DataIndependentAddressing, // Argon2id
            Version = Argon2Version.Nineteen,
            MemoryCost = _settings.MemoryCostKb,
            TimeCost = _settings.Iterations,
            Lanes = _settings.Parallelism,
            Threads = _settings.Parallelism,
            HashLength = _settings.HashLength,
            Password = System.Text.Encoding.UTF8.GetBytes(password),
            Salt = GenerateSalt()
        };

        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user, string hashedPassword, string providedPassword)
    {
        try
        {
            // Argon2.Verify uses constant-time comparison
            if (Argon2.Verify(hashedPassword, providedPassword))
            {
                return PasswordVerificationResult.Success;
            }
            return PasswordVerificationResult.Failed;
        }
        catch
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private static byte[] GenerateSalt()
    {
        var salt = new byte[16]; // 128 bits
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }
}
