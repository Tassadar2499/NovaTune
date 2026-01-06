using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Identity;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTune.UnitTests.Auth;

/// <summary>
/// Unit tests for Argon2id password hashing (Req 1.x).
/// Tests hash generation, verification, and security properties.
/// </summary>
public class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher;
    private readonly ApplicationUser _testUser;

    public Argon2PasswordHasherTests()
    {
        // Use reduced settings for faster test execution
        var settings = Options.Create(new Argon2Settings
        {
            MemoryCostKb = 1024,  // 1 MB for tests (faster)
            Iterations = 1,
            Parallelism = 1,
            HashLength = 32
        });

        _hasher = new Argon2PasswordHasher(settings);
        _testUser = new ApplicationUser
        {
            UserId = "test-user-id",
            Email = "test@example.com",
            DisplayName = "Test User"
        };
    }

    [Fact]
    public void HashPassword_Should_return_non_empty_hash()
    {
        var password = "SecurePassword123!";

        var hash = _hasher.HashPassword(_testUser, password);

        hash.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void HashPassword_Should_return_argon2_formatted_string()
    {
        var password = "SecurePassword123!";

        var hash = _hasher.HashPassword(_testUser, password);

        // Argon2 hashes start with $argon2 prefix
        hash.ShouldStartWith("$argon2");
    }

    [Fact]
    public void HashPassword_Should_produce_different_hashes_for_same_password()
    {
        var password = "SecurePassword123!";

        var hash1 = _hasher.HashPassword(_testUser, password);
        var hash2 = _hasher.HashPassword(_testUser, password);

        // Different salts should produce different hashes
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void HashPassword_Should_produce_different_hashes_for_different_passwords()
    {
        var hash1 = _hasher.HashPassword(_testUser, "Password1!");
        var hash2 = _hasher.HashPassword(_testUser, "Password2!");

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void VerifyHashedPassword_Should_return_success_for_correct_password()
    {
        var password = "SecurePassword123!";
        var hash = _hasher.HashPassword(_testUser, password);

        var result = _hasher.VerifyHashedPassword(_testUser, hash, password);

        result.ShouldBe(PasswordVerificationResult.Success);
    }

    [Fact]
    public void VerifyHashedPassword_Should_return_failed_for_incorrect_password()
    {
        var password = "SecurePassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _hasher.HashPassword(_testUser, password);

        var result = _hasher.VerifyHashedPassword(_testUser, hash, wrongPassword);

        result.ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void VerifyHashedPassword_Should_return_failed_for_empty_password()
    {
        var password = "SecurePassword123!";
        var hash = _hasher.HashPassword(_testUser, password);

        var result = _hasher.VerifyHashedPassword(_testUser, hash, "");

        result.ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void VerifyHashedPassword_Should_return_failed_for_invalid_hash()
    {
        var result = _hasher.VerifyHashedPassword(_testUser, "invalid-hash", "password");

        result.ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void VerifyHashedPassword_Should_return_failed_for_empty_hash()
    {
        var result = _hasher.VerifyHashedPassword(_testUser, "", "password");

        result.ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void VerifyHashedPassword_Should_be_case_sensitive()
    {
        var password = "SecurePassword123!";
        var hash = _hasher.HashPassword(_testUser, password);

        var result = _hasher.VerifyHashedPassword(_testUser, hash, "securepassword123!");

        result.ShouldBe(PasswordVerificationResult.Failed);
    }
}
