using NovaTuneApp.ApiService.Models.Admin;
using NovaTuneApp.ApiService.Services.Admin;

namespace NovaTune.UnitTests.Services.Admin;

/// <summary>
/// Unit tests for AuditLogService hash computation and integrity verification.
/// Tests the SHA-256 hash chain implementation for tamper evidence (NF-3.5).
/// Note: Full service tests require RavenDB and are covered in integration tests.
/// </summary>
public class AuditLogServiceTests
{
    // ========================================================================
    // ComputeHash Tests - Hash Computation Behavior
    // ========================================================================

    [Fact]
    public void ComputeHash_WhenCalledWithSameEntry_ReturnsSameHash()
    {
        // Arrange
        var entry = CreateTestAuditEntry();

        // Act
        var hash1 = AuditLogService.ComputeHash(entry);
        var hash2 = AuditLogService.ComputeHash(entry);

        // Assert
        hash1.ShouldNotBeNullOrWhiteSpace();
        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void ComputeHash_WhenEntryDiffers_ReturnsDifferentHash()
    {
        // Arrange
        var entry1 = CreateTestAuditEntry();
        var entry2 = entry1 with { AuditId = "different-id" };

        // Act
        var hash1 = AuditLogService.ComputeHash(entry1);
        var hash2 = AuditLogService.ComputeHash(entry2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeHash_WhenActionDiffers_ReturnsDifferentHash()
    {
        // Arrange
        var entry1 = CreateTestAuditEntry();
        var entry2 = entry1 with { Action = "different.action" };

        // Act
        var hash1 = AuditLogService.ComputeHash(entry1);
        var hash2 = AuditLogService.ComputeHash(entry2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeHash_WhenTimestampDiffers_ReturnsDifferentHash()
    {
        // Arrange
        var entry1 = CreateTestAuditEntry();
        var entry2 = entry1 with { Timestamp = entry1.Timestamp.AddSeconds(1) };

        // Act
        var hash1 = AuditLogService.ComputeHash(entry1);
        var hash2 = AuditLogService.ComputeHash(entry2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeHash_WhenPreviousHashDiffers_ReturnsDifferentHash()
    {
        // Arrange
        var entry1 = CreateTestAuditEntry() with { PreviousEntryHash = "hash1" };
        var entry2 = entry1 with { PreviousEntryHash = "hash2" };

        // Act
        var hash1 = AuditLogService.ComputeHash(entry1);
        var hash2 = AuditLogService.ComputeHash(entry2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsHexEncodedLowercase()
    {
        // Arrange
        var entry = CreateTestAuditEntry();

        // Act
        var hash = AuditLogService.ComputeHash(entry);

        // Assert
        hash.ShouldNotBeNullOrWhiteSpace();
        hash.Length.ShouldBe(64); // SHA-256 = 32 bytes = 64 hex chars
        hash.ShouldBe(hash.ToLowerInvariant()); // Should be lowercase
        hash.All(c => char.IsLetterOrDigit(c)).ShouldBeTrue();
    }

    [Fact]
    public void ComputeHash_WithNullOptionalFields_ComputesConsistently()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            Id = "AuditLogs/test",
            AuditId = "test-audit-id",
            ActorUserId = "user-123",
            ActorEmail = "admin@test.com",
            Action = AuditActions.UserStatusChanged,
            TargetType = AuditTargetTypes.User,
            TargetId = "target-456",
            ReasonCode = null,
            ReasonText = null,
            PreviousState = null,
            NewState = null,
            Timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            CorrelationId = null,
            IpAddress = null,
            UserAgent = null,
            PreviousEntryHash = null,
            ContentHash = null,
            Expires = DateTimeOffset.MaxValue
        };

        // Act
        var hash1 = AuditLogService.ComputeHash(entry);
        var hash2 = AuditLogService.ComputeHash(entry);

        // Assert
        hash1.ShouldBe(hash2);
        hash1.Length.ShouldBe(64);
    }

    [Fact]
    public void ComputeHash_IncludesPreviousStateInHash()
    {
        // Arrange
        var entry1 = CreateTestAuditEntry() with { PreviousState = "{\"status\":\"active\"}" };
        var entry2 = CreateTestAuditEntry() with { PreviousState = "{\"status\":\"disabled\"}" };

        // Act
        var hash1 = AuditLogService.ComputeHash(entry1);
        var hash2 = AuditLogService.ComputeHash(entry2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeHash_IncludesNewStateInHash()
    {
        // Arrange
        var entry1 = CreateTestAuditEntry() with { NewState = "{\"status\":\"active\"}" };
        var entry2 = CreateTestAuditEntry() with { NewState = "{\"status\":\"disabled\"}" };

        // Act
        var hash1 = AuditLogService.ComputeHash(entry1);
        var hash2 = AuditLogService.ComputeHash(entry2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    // ========================================================================
    // Hash Chain Verification Tests
    // ========================================================================

    [Fact]
    public void HashChain_WhenEntriesAreValid_CanBeVerified()
    {
        // Arrange - Create a valid chain of entries
        var entry1 = CreateTestAuditEntry("01", null);
        entry1 = entry1 with { ContentHash = AuditLogService.ComputeHash(entry1) };

        var entry2 = CreateTestAuditEntry("02", entry1.ContentHash);
        entry2 = entry2 with { ContentHash = AuditLogService.ComputeHash(entry2) };

        var entry3 = CreateTestAuditEntry("03", entry2.ContentHash);
        entry3 = entry3 with { ContentHash = AuditLogService.ComputeHash(entry3) };

        // Act - Verify chain
        var chain = new[] { entry1, entry2, entry3 };
        var isValid = VerifyChain(chain);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void HashChain_WhenContentTampered_VerificationFails()
    {
        // Arrange - Create a valid chain, then tamper with middle entry
        var entry1 = CreateTestAuditEntry("01", null);
        entry1 = entry1 with { ContentHash = AuditLogService.ComputeHash(entry1) };

        var entry2 = CreateTestAuditEntry("02", entry1.ContentHash);
        entry2 = entry2 with { ContentHash = AuditLogService.ComputeHash(entry2) };

        var entry3 = CreateTestAuditEntry("03", entry2.ContentHash);
        entry3 = entry3 with { ContentHash = AuditLogService.ComputeHash(entry3) };

        // Tamper with entry2's action (but keep the stored hash)
        entry2 = entry2 with { Action = "tampered.action" };

        // Act - Verify chain
        var chain = new[] { entry1, entry2, entry3 };
        var isValid = VerifyChain(chain);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void HashChain_WhenPreviousHashMismatch_VerificationFails()
    {
        // Arrange - Create entries with broken chain link
        var entry1 = CreateTestAuditEntry("01", null);
        entry1 = entry1 with { ContentHash = AuditLogService.ComputeHash(entry1) };

        // Entry2 has wrong previous hash
        var entry2 = CreateTestAuditEntry("02", "wrong-previous-hash");
        entry2 = entry2 with { ContentHash = AuditLogService.ComputeHash(entry2) };

        // Act - Verify chain
        var chain = new[] { entry1, entry2 };
        var isValid = VerifyChain(chain);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void HashChain_SingleEntry_WithValidHash_IsValid()
    {
        // Arrange
        var entry = CreateTestAuditEntry("01", null);
        entry = entry with { ContentHash = AuditLogService.ComputeHash(entry) };

        // Act
        var chain = new[] { entry };
        var isValid = VerifyChain(chain);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void HashChain_EmptyChain_IsValid()
    {
        // Arrange
        var chain = Array.Empty<AuditLogEntry>();

        // Act
        var isValid = VerifyChain(chain);

        // Assert
        isValid.ShouldBeTrue();
    }

    // ========================================================================
    // Audit Constants Tests
    // ========================================================================

    [Theory]
    [InlineData(AuditActions.UserStatusChanged)]
    [InlineData(AuditActions.TrackModerated)]
    [InlineData(AuditActions.TrackDeleted)]
    [InlineData(AuditActions.AuditLogViewed)]
    public void AuditActions_AreValidStrings(string action)
    {
        action.ShouldNotBeNullOrWhiteSpace();
        action.ShouldContain(".");
    }

    [Theory]
    [InlineData(AuditTargetTypes.User)]
    [InlineData(AuditTargetTypes.Track)]
    [InlineData(AuditTargetTypes.AuditLog)]
    public void AuditTargetTypes_AreValidStrings(string targetType)
    {
        targetType.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(ModerationReasonCodes.CopyrightViolation)]
    [InlineData(ModerationReasonCodes.CommunityGuidelines)]
    [InlineData(ModerationReasonCodes.Spam)]
    [InlineData(ModerationReasonCodes.IllegalContent)]
    [InlineData(ModerationReasonCodes.Other)]
    [InlineData(ModerationReasonCodes.UserRequest)]
    public void ModerationReasonCodes_IsValid_ReturnsTrue(string code)
    {
        ModerationReasonCodes.IsValid(code).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("INVALID_REASON")]
    [InlineData("tos")] // Case sensitive
    public void ModerationReasonCodes_IsValid_ReturnsFalseForInvalid(string code)
    {
        ModerationReasonCodes.IsValid(code).ShouldBeFalse();
    }

    [Fact]
    public void ModerationReasonCodes_IsValid_ReturnsFalseForNull()
    {
        ModerationReasonCodes.IsValid(null!).ShouldBeFalse();
    }

    // ========================================================================
    // Test Helpers
    // ========================================================================

    private static AuditLogEntry CreateTestAuditEntry(
        string? auditId = null,
        string? previousHash = null)
    {
        var id = auditId ?? "test-audit-id";
        return new AuditLogEntry
        {
            Id = $"AuditLogs/{id}",
            AuditId = id,
            ActorUserId = "admin-user-123",
            ActorEmail = "admin@test.com",
            Action = AuditActions.UserStatusChanged,
            TargetType = AuditTargetTypes.User,
            TargetId = "target-user-456",
            ReasonCode = ModerationReasonCodes.CommunityGuidelines,
            ReasonText = "Terms of service violation",
            PreviousState = "{\"Status\":\"Active\"}",
            NewState = "{\"Status\":\"Disabled\"}",
            Timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            CorrelationId = "corr-789",
            IpAddress = "192.168.1.1",
            UserAgent = "Test Agent",
            PreviousEntryHash = previousHash,
            ContentHash = null,
            Expires = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero)
        };
    }

    /// <summary>
    /// Verifies the integrity of a chain of audit log entries.
    /// Mirrors the logic in AuditLogService.VerifyIntegrityAsync.
    /// </summary>
    private static bool VerifyChain(IEnumerable<AuditLogEntry> entries)
    {
        string? expectedPreviousHash = null;

        foreach (var entry in entries)
        {
            // Check previous hash chain
            if (entry.PreviousEntryHash != expectedPreviousHash)
                return false;

            // Verify content hash
            var computedHash = AuditLogService.ComputeHash(entry);
            if (entry.ContentHash != computedHash)
                return false;

            expectedPreviousHash = entry.ContentHash;
        }

        return true;
    }
}
