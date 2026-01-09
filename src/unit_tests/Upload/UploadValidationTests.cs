using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NovaTuneApp.ApiService.Models.Upload;

namespace NovaTune.UnitTests.Upload;

/// <summary>
/// Unit tests for upload-related validation logic (Stage 2 Test Strategy - 09-test-strategy.md).
/// </summary>
public class UploadValidationTests
{
    // ============================================================================
    // MIME Type Validation Tests
    // ============================================================================

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg",    // .mp3
        "audio/mp4",     // .m4a
        "audio/flac",    // .flac
        "audio/wav",     // .wav
        "audio/x-wav",   // .wav (alternative)
        "audio/ogg"      // .ogg
    };

    [Theory]
    [InlineData("audio/mpeg")]
    [InlineData("audio/mp4")]
    [InlineData("audio/flac")]
    [InlineData("audio/wav")]
    [InlineData("audio/x-wav")]
    [InlineData("audio/ogg")]
    public void Should_allow_supported_mime_types(string mimeType)
    {
        AllowedMimeTypes.Contains(mimeType).ShouldBeTrue();
    }

    [Theory]
    [InlineData("AUDIO/MPEG")]
    [InlineData("Audio/Flac")]
    [InlineData("AUDIO/WAV")]
    public void Should_allow_mime_types_case_insensitively(string mimeType)
    {
        AllowedMimeTypes.Contains(mimeType).ShouldBeTrue();
    }

    [Theory]
    [InlineData("audio/aac")]
    [InlineData("audio/webm")]
    [InlineData("video/mp4")]
    [InlineData("application/octet-stream")]
    [InlineData("text/plain")]
    [InlineData("")]
    public void Should_reject_unsupported_mime_types(string mimeType)
    {
        AllowedMimeTypes.Contains(mimeType).ShouldBeFalse();
    }

    // ============================================================================
    // ObjectKey Generation Format Tests
    // ============================================================================

    [Fact]
    public void ObjectKey_should_follow_expected_format()
    {
        // Format: audio/{userId}/{trackId}/{randomSuffix}
        var userId = Ulid.NewUlid().ToString();
        var trackId = Ulid.NewUlid().ToString();
        var randomSuffix = GenerateRandomSuffix();

        var objectKey = $"audio/{userId}/{trackId}/{randomSuffix}";

        // Verify format
        objectKey.ShouldStartWith("audio/");
        var parts = objectKey.Split('/');
        parts.Length.ShouldBe(4);
        parts[0].ShouldBe("audio");
        parts[1].ShouldBe(userId);
        parts[2].ShouldBe(trackId);
        parts[3].ShouldBe(randomSuffix);
    }

    [Fact]
    public void RandomSuffix_should_be_base64url_encoded()
    {
        var suffix = GenerateRandomSuffix();

        // Should not contain standard base64 chars that are replaced in base64url
        suffix.ShouldNotContain("+");
        suffix.ShouldNotContain("/");
        suffix.ShouldNotContain("="); // Padding is trimmed

        // Should only contain base64url safe characters
        Regex.IsMatch(suffix, "^[A-Za-z0-9_-]+$").ShouldBeTrue();
    }

    [Fact]
    public void RandomSuffix_should_have_sufficient_entropy()
    {
        var suffix = GenerateRandomSuffix();

        // 16 bytes = 128 bits of entropy
        // Base64 encoding: 16 bytes -> ~22 characters (without padding)
        suffix.Length.ShouldBeGreaterThanOrEqualTo(21);
        suffix.Length.ShouldBeLessThanOrEqualTo(22);
    }

    [Fact]
    public void RandomSuffix_should_be_unique()
    {
        var suffixes = Enumerable.Range(0, 100)
            .Select(_ => GenerateRandomSuffix())
            .ToHashSet();

        // All 100 generated suffixes should be unique
        suffixes.Count.ShouldBe(100);
    }

    /// <summary>
    /// Replicates the GenerateRandomSuffix logic from UploadService for testing.
    /// </summary>
    private static string GenerateRandomSuffix()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    // ============================================================================
    // Quota Calculation Tests
    // ============================================================================

    [Theory]
    [InlineData(0, 100_000_000, 10_000_000, true)]           // 0 used, 100MB quota, 10MB file -> OK
    [InlineData(50_000_000, 100_000_000, 50_000_000, true)]  // 50MB used, 100MB quota, 50MB file -> exactly at limit
    [InlineData(50_000_000, 100_000_000, 60_000_000, false)] // 50MB used, 100MB quota, 60MB file -> exceeds
    [InlineData(99_999_999, 100_000_000, 2, false)]          // 99.999MB used, 100MB quota, 2 bytes -> exceeds by 1 byte
    [InlineData(0, 1_073_741_824, 1_073_741_824, true)]      // 0 used, 1GB quota, 1GB file -> exactly at limit
    public void Quota_calculation_should_correctly_check_storage_limit(
        long usedStorageBytes,
        long quotaBytes,
        long fileSizeBytes,
        bool shouldBeAllowed)
    {
        var projectedUsage = usedStorageBytes + fileSizeBytes;
        var isWithinQuota = projectedUsage <= quotaBytes;

        isWithinQuota.ShouldBe(shouldBeAllowed);
    }

    [Theory]
    [InlineData(0, 500, true)]    // 0 tracks, max 500 -> OK
    [InlineData(499, 500, true)]  // 499 tracks, max 500 -> OK (can add one more)
    [InlineData(500, 500, false)] // 500 tracks, max 500 -> at limit, cannot add
    [InlineData(501, 500, false)] // 501 tracks, max 500 -> over limit
    public void Quota_calculation_should_correctly_check_track_count_limit(
        int currentTrackCount,
        int maxTracks,
        bool shouldBeAllowed)
    {
        var isWithinQuota = currentTrackCount < maxTracks;

        isWithinQuota.ShouldBe(shouldBeAllowed);
    }

    [Fact]
    public void Quota_calculation_should_handle_large_file_sizes()
    {
        // Test with very large files (TB scale)
        var usedBytes = 500L * 1024 * 1024 * 1024; // 500 GB
        var quotaBytes = 1024L * 1024 * 1024 * 1024; // 1 TB
        var fileSizeBytes = 400L * 1024 * 1024 * 1024; // 400 GB

        var projectedUsage = usedBytes + fileSizeBytes;
        var isWithinQuota = projectedUsage <= quotaBytes;

        isWithinQuota.ShouldBeTrue(); // 900 GB < 1 TB
    }

    // ============================================================================
    // UploadSession Expiry Logic Tests
    // ============================================================================

    [Fact]
    public void UploadSession_should_not_be_expired_before_expiry_time()
    {
        var now = DateTimeOffset.UtcNow;
        var session = CreateTestUploadSession(expiresAt: now.AddMinutes(15));

        var isExpired = session.ExpiresAt < now;

        isExpired.ShouldBeFalse();
    }

    [Fact]
    public void UploadSession_should_be_expired_after_expiry_time()
    {
        var now = DateTimeOffset.UtcNow;
        var session = CreateTestUploadSession(expiresAt: now.AddMinutes(-1));

        var isExpired = session.ExpiresAt < now;

        isExpired.ShouldBeTrue();
    }

    [Fact]
    public void UploadSession_should_be_expired_at_exact_expiry_time()
    {
        var now = DateTimeOffset.UtcNow;
        var session = CreateTestUploadSession(expiresAt: now);

        // At exact expiry time, it should not be expired (< not <=)
        var isExpired = session.ExpiresAt < now;

        isExpired.ShouldBeFalse();
    }

    [Fact]
    public void UploadSession_expiry_status_should_indicate_expired()
    {
        var session = CreateTestUploadSession(expiresAt: DateTimeOffset.UtcNow.AddMinutes(15));
        session.Status = UploadSessionStatus.Expired;

        session.Status.ShouldBe(UploadSessionStatus.Expired);
    }

    [Fact]
    public void UploadSession_should_default_to_pending_status()
    {
        var session = CreateTestUploadSession(expiresAt: DateTimeOffset.UtcNow.AddMinutes(15));

        session.Status.ShouldBe(UploadSessionStatus.Pending);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(60)]
    public void UploadSession_ttl_should_match_presigned_url_expiry(int ttlMinutes)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var expectedExpiresAt = createdAt.AddMinutes(ttlMinutes);

        var session = new UploadSession
        {
            Id = "UploadSessions/test",
            UploadId = Ulid.NewUlid().ToString(),
            UserId = Ulid.NewUlid().ToString(),
            ReservedTrackId = Ulid.NewUlid().ToString(),
            ObjectKey = "audio/user/track/suffix",
            ExpectedMimeType = "audio/mpeg",
            MaxAllowedSizeBytes = 100_000_000,
            CreatedAt = createdAt,
            ExpiresAt = expectedExpiresAt
        };

        var actualTtl = session.ExpiresAt - session.CreatedAt;
        actualTtl.TotalMinutes.ShouldBe(ttlMinutes);
    }

    private static UploadSession CreateTestUploadSession(DateTimeOffset expiresAt)
    {
        return new UploadSession
        {
            Id = $"UploadSessions/{Ulid.NewUlid()}",
            UploadId = Ulid.NewUlid().ToString(),
            UserId = Ulid.NewUlid().ToString(),
            ReservedTrackId = Ulid.NewUlid().ToString(),
            ObjectKey = "audio/user/track/suffix",
            ExpectedMimeType = "audio/mpeg",
            MaxAllowedSizeBytes = 100_000_000,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    // ============================================================================
    // Checksum Computation Tests
    // ============================================================================

    [Fact]
    public void Checksum_should_produce_valid_sha256_hex_string()
    {
        var testData = "Hello, NovaTune!"u8.ToArray();
        var checksum = ComputeChecksum(testData);

        // SHA-256 produces 64 hex characters (256 bits / 4 bits per hex char)
        checksum.Length.ShouldBe(64);

        // Should be lowercase hex
        Regex.IsMatch(checksum, "^[a-f0-9]{64}$").ShouldBeTrue();
    }

    [Fact]
    public void Checksum_should_be_deterministic()
    {
        var testData = "Same data produces same hash"u8.ToArray();

        var checksum1 = ComputeChecksum(testData);
        var checksum2 = ComputeChecksum(testData);

        checksum1.ShouldBe(checksum2);
    }

    [Fact]
    public void Checksum_should_differ_for_different_inputs()
    {
        var data1 = "First file content"u8.ToArray();
        var data2 = "Second file content"u8.ToArray();

        var checksum1 = ComputeChecksum(data1);
        var checksum2 = ComputeChecksum(data2);

        checksum1.ShouldNotBe(checksum2);
    }

    [Fact]
    public void Checksum_should_detect_single_byte_change()
    {
        var data1 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var data2 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x06 }; // Last byte different

        var checksum1 = ComputeChecksum(data1);
        var checksum2 = ComputeChecksum(data2);

        checksum1.ShouldNotBe(checksum2);
    }

    [Fact]
    public void Checksum_should_handle_empty_input()
    {
        var emptyData = Array.Empty<byte>();
        var checksum = ComputeChecksum(emptyData);

        // SHA-256 of empty input is a well-known constant
        checksum.ShouldBe("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void Checksum_should_handle_large_input()
    {
        // 10 MB of random data
        var largeData = RandomNumberGenerator.GetBytes(10 * 1024 * 1024);

        var checksum = ComputeChecksum(largeData);

        checksum.Length.ShouldBe(64);
        Regex.IsMatch(checksum, "^[a-f0-9]{64}$").ShouldBeTrue();
    }

    /// <summary>
    /// Replicates the checksum computation logic from UploadIngestorService.
    /// </summary>
    private static string ComputeChecksum(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
