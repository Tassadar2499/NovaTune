using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NovaTune.UnitTests.Fakes;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Services;

/// <summary>
/// Unit tests for TrackManagementService CRUD operations.
/// Tests cover GetTrackAsync, UpdateTrackAsync, DeleteTrackAsync, and RestoreTrackAsync.
/// Note: ListTracksAsync uses RavenDB Query with indexes which requires integration testing.
/// </summary>
public class TrackManagementServiceTests
{
    private readonly AsyncDocumentSessionFake _sessionFake;
    private readonly StreamingServiceFake _streamingServiceFake;
    private readonly TrackManagementOptions _trackOptions;
    private readonly NovaTuneOptions _novaTuneOptions;
    private readonly TrackManagementService _sut;

    public TrackManagementServiceTests()
    {
        _sessionFake = new AsyncDocumentSessionFake();
        _streamingServiceFake = new StreamingServiceFake();
        _trackOptions = new TrackManagementOptions
        {
            DeletionGracePeriod = TimeSpan.FromDays(30),
            MaxPageSize = 100,
            DefaultPageSize = 20
        };
        _novaTuneOptions = new NovaTuneOptions
        {
            TopicPrefix = "test"
        };

        _sut = new TrackManagementService(
            _sessionFake,
            Options.Create(_trackOptions),
            Options.Create(_novaTuneOptions),
            _streamingServiceFake,
            NullLogger<TrackManagementService>.Instance);
    }

    // ========================================================================
    // GetTrackAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetTrackAsync_WhenTrackExists_ReturnsTrackDetails()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        var result = await _sut.GetTrackAsync("01HXK123", "user1");

        // Assert
        result.ShouldNotBeNull();
        result.TrackId.ShouldBe("01HXK123");
        result.Title.ShouldBe(track.Title);
        result.Artist.ShouldBe(track.Artist);
        result.Status.ShouldBe(track.Status);
    }

    [Fact]
    public async Task GetTrackAsync_WhenTrackNotFound_ThrowsTrackNotFoundException()
    {
        // Arrange - no track stored

        // Act & Assert
        var ex = await Should.ThrowAsync<TrackNotFoundException>(
            () => _sut.GetTrackAsync("01HXK123", "user1"));

        ex.TrackId.ShouldBe("01HXK123");
    }

    [Fact]
    public async Task GetTrackAsync_WhenUserDoesNotOwnTrack_ThrowsTrackAccessDeniedException()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "otherUser");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act & Assert
        var ex = await Should.ThrowAsync<TrackAccessDeniedException>(
            () => _sut.GetTrackAsync("01HXK123", "user1"));

        ex.TrackId.ShouldBe("01HXK123");
    }

    [Fact]
    public async Task GetTrackAsync_WhenTrackIsDeleted_ReturnsTrackWithDeletedStatus()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        track.DeletedAt = DateTimeOffset.UtcNow;
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(30);
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        var result = await _sut.GetTrackAsync("01HXK123", "user1");

        // Assert - should return the track even if deleted (for viewing)
        result.ShouldNotBeNull();
        result.Status.ShouldBe(TrackStatus.Deleted);
        result.DeletedAt.ShouldNotBeNull();
    }

    // ========================================================================
    // UpdateTrackAsync Tests
    // ========================================================================

    [Fact]
    public async Task UpdateTrackAsync_WhenTitleProvided_UpdatesTitle()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        var request = new UpdateTrackRequest(Title: "New Title", Artist: null);

        // Act
        var result = await _sut.UpdateTrackAsync("01HXK123", "user1", request);

        // Assert
        result.Title.ShouldBe("New Title");
        result.Artist.ShouldBe(track.Artist); // Artist unchanged
    }

    [Fact]
    public async Task UpdateTrackAsync_WhenArtistProvided_UpdatesArtist()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        var request = new UpdateTrackRequest(Title: null, Artist: "New Artist");

        // Act
        var result = await _sut.UpdateTrackAsync("01HXK123", "user1", request);

        // Assert
        result.Artist.ShouldBe("New Artist");
        result.Title.ShouldBe(track.Title); // Title unchanged
    }

    [Fact]
    public async Task UpdateTrackAsync_WhenEmptyArtist_ClearsArtist()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        track.Artist = "Existing Artist";
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        var request = new UpdateTrackRequest(Title: null, Artist: "");

        // Act
        var result = await _sut.UpdateTrackAsync("01HXK123", "user1", request);

        // Assert
        result.Artist.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateTrackAsync_WhenBothFieldsProvided_UpdatesBoth()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        var request = new UpdateTrackRequest(Title: "New Title", Artist: "New Artist");

        // Act
        var result = await _sut.UpdateTrackAsync("01HXK123", "user1", request);

        // Assert
        result.Title.ShouldBe("New Title");
        result.Artist.ShouldBe("New Artist");
    }

    [Fact]
    public async Task UpdateTrackAsync_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        var originalUpdatedAt = track.UpdatedAt;
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        var request = new UpdateTrackRequest(Title: "New Title", Artist: null);

        // Act
        var result = await _sut.UpdateTrackAsync("01HXK123", "user1", request);

        // Assert
        result.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateTrackAsync_WhenTrackNotFound_ThrowsTrackNotFoundException()
    {
        // Arrange - no track stored
        var request = new UpdateTrackRequest(Title: "New Title", Artist: null);

        // Act & Assert
        await Should.ThrowAsync<TrackNotFoundException>(
            () => _sut.UpdateTrackAsync("01HXK123", "user1", request));
    }

    [Fact]
    public async Task UpdateTrackAsync_WhenUserDoesNotOwnTrack_ThrowsTrackAccessDeniedException()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "otherUser");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        var request = new UpdateTrackRequest(Title: "New Title", Artist: null);

        // Act & Assert
        await Should.ThrowAsync<TrackAccessDeniedException>(
            () => _sut.UpdateTrackAsync("01HXK123", "user1", request));
    }

    [Fact]
    public async Task UpdateTrackAsync_WhenTrackIsDeleted_ThrowsTrackDeletedException()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        track.DeletedAt = DateTimeOffset.UtcNow;
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        var request = new UpdateTrackRequest(Title: "New Title", Artist: null);

        // Act & Assert
        var ex = await Should.ThrowAsync<TrackDeletedException>(
            () => _sut.UpdateTrackAsync("01HXK123", "user1", request));

        ex.TrackId.ShouldBe("01HXK123");
        ex.DeletedAt.ShouldNotBeNull();
    }

    // ========================================================================
    // DeleteTrackAsync Tests
    // ========================================================================

    [Fact]
    public async Task DeleteTrackAsync_SoftDeletesTrack()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        await _sut.DeleteTrackAsync("01HXK123", "user1");

        // Assert
        track.Status.ShouldBe(TrackStatus.Deleted);
        track.DeletedAt.ShouldNotBeNull();
        track.ScheduledDeletionAt.ShouldNotBeNull();
        track.StatusBeforeDeletion.ShouldBe(TrackStatus.Ready);
    }

    [Fact]
    public async Task DeleteTrackAsync_SetsScheduledDeletionBasedOnGracePeriod()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);
        var beforeDelete = DateTimeOffset.UtcNow;

        // Act
        await _sut.DeleteTrackAsync("01HXK123", "user1");

        // Assert
        track.ScheduledDeletionAt.ShouldNotBeNull();
        var expectedSchedule = beforeDelete.Add(_trackOptions.DeletionGracePeriod);
        (track.ScheduledDeletionAt.Value - expectedSchedule).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeleteTrackAsync_InvalidatesStreamingCache()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        await _sut.DeleteTrackAsync("01HXK123", "user1");

        // Assert
        _streamingServiceFake.InvalidatedTracks.ShouldContain(("01HXK123", "user1"));
    }

    [Fact]
    public async Task DeleteTrackAsync_ContinuesEvenIfCacheInvalidationFails()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);
        _streamingServiceFake.ThrowOnInvalidate = true;
        _streamingServiceFake.ExceptionToThrow = new InvalidOperationException("Cache unavailable");

        // Act - should not throw
        await _sut.DeleteTrackAsync("01HXK123", "user1");

        // Assert - track should still be deleted
        track.Status.ShouldBe(TrackStatus.Deleted);
    }

    [Fact]
    public async Task DeleteTrackAsync_WhenTrackNotFound_ThrowsTrackNotFoundException()
    {
        // Arrange - no track stored

        // Act & Assert
        await Should.ThrowAsync<TrackNotFoundException>(
            () => _sut.DeleteTrackAsync("01HXK123", "user1"));
    }

    [Fact]
    public async Task DeleteTrackAsync_WhenUserDoesNotOwnTrack_ThrowsTrackAccessDeniedException()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "otherUser");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act & Assert
        await Should.ThrowAsync<TrackAccessDeniedException>(
            () => _sut.DeleteTrackAsync("01HXK123", "user1"));
    }

    [Fact]
    public async Task DeleteTrackAsync_WhenAlreadyDeleted_ThrowsTrackAlreadyDeletedException()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act & Assert
        var ex = await Should.ThrowAsync<TrackAlreadyDeletedException>(
            () => _sut.DeleteTrackAsync("01HXK123", "user1"));

        ex.TrackId.ShouldBe("01HXK123");
    }

    [Fact]
    public async Task DeleteTrackAsync_PreservesStatusBeforeDeletion()
    {
        // Arrange - track in Processing status
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Processing;
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        await _sut.DeleteTrackAsync("01HXK123", "user1");

        // Assert
        track.StatusBeforeDeletion.ShouldBe(TrackStatus.Processing);
    }

    // ========================================================================
    // RestoreTrackAsync Tests
    // ========================================================================

    [Fact]
    public async Task RestoreTrackAsync_RestoresDeletedTrack()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        track.StatusBeforeDeletion = TrackStatus.Ready;
        track.DeletedAt = DateTimeOffset.UtcNow.AddDays(-1);
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(29);
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        var result = await _sut.RestoreTrackAsync("01HXK123", "user1");

        // Assert
        result.Status.ShouldBe(TrackStatus.Ready);
        track.DeletedAt.ShouldBeNull();
        track.ScheduledDeletionAt.ShouldBeNull();
        track.StatusBeforeDeletion.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreTrackAsync_RestoresToPreviousStatus()
    {
        // Arrange - was Processing before deletion
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        track.StatusBeforeDeletion = TrackStatus.Processing;
        track.DeletedAt = DateTimeOffset.UtcNow.AddDays(-1);
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(29);
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        var result = await _sut.RestoreTrackAsync("01HXK123", "user1");

        // Assert
        result.Status.ShouldBe(TrackStatus.Processing);
    }

    [Fact]
    public async Task RestoreTrackAsync_DefaultsToReadyWhenNoStatusBeforeDeletion()
    {
        // Arrange - no StatusBeforeDeletion saved
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        track.StatusBeforeDeletion = null; // Edge case
        track.DeletedAt = DateTimeOffset.UtcNow.AddDays(-1);
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(29);
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        var result = await _sut.RestoreTrackAsync("01HXK123", "user1");

        // Assert
        result.Status.ShouldBe(TrackStatus.Ready);
    }

    [Fact]
    public async Task RestoreTrackAsync_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        track.StatusBeforeDeletion = TrackStatus.Ready;
        track.DeletedAt = DateTimeOffset.UtcNow.AddDays(-1);
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(29);
        var originalUpdatedAt = track.UpdatedAt;
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        var result = await _sut.RestoreTrackAsync("01HXK123", "user1");

        // Assert
        result.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
    }

    [Fact]
    public async Task RestoreTrackAsync_WhenTrackNotFound_ThrowsTrackNotFoundException()
    {
        // Arrange - no track stored

        // Act & Assert
        await Should.ThrowAsync<TrackNotFoundException>(
            () => _sut.RestoreTrackAsync("01HXK123", "user1"));
    }

    [Fact]
    public async Task RestoreTrackAsync_WhenUserDoesNotOwnTrack_ThrowsTrackAccessDeniedException()
    {
        // Arrange
        var track = CreateTestTrack("01HXK123", "otherUser");
        track.Status = TrackStatus.Deleted;
        track.DeletedAt = DateTimeOffset.UtcNow.AddDays(-1);
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(29);
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act & Assert
        await Should.ThrowAsync<TrackAccessDeniedException>(
            () => _sut.RestoreTrackAsync("01HXK123", "user1"));
    }

    [Fact]
    public async Task RestoreTrackAsync_WhenNotDeleted_ThrowsTrackNotDeletedException()
    {
        // Arrange - track not deleted
        var track = CreateTestTrack("01HXK123", "user1");
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act & Assert
        var ex = await Should.ThrowAsync<TrackNotDeletedException>(
            () => _sut.RestoreTrackAsync("01HXK123", "user1"));

        ex.TrackId.ShouldBe("01HXK123");
    }

    [Fact]
    public async Task RestoreTrackAsync_WhenGracePeriodExpired_ThrowsTrackRestorationExpiredException()
    {
        // Arrange - scheduled deletion time has passed
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        track.DeletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(-1); // Already past
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act & Assert
        var ex = await Should.ThrowAsync<TrackRestorationExpiredException>(
            () => _sut.RestoreTrackAsync("01HXK123", "user1"));

        ex.TrackId.ShouldBe("01HXK123");
        ex.DeletedAt.ShouldBe(track.DeletedAt.Value);
        ex.ScheduledDeletionAt.ShouldBe(track.ScheduledDeletionAt.Value);
    }

    [Fact]
    public async Task RestoreTrackAsync_WhenJustBeforeExpiry_Succeeds()
    {
        // Arrange - scheduled deletion is just moments away but hasn't passed
        var track = CreateTestTrack("01HXK123", "user1");
        track.Status = TrackStatus.Deleted;
        track.StatusBeforeDeletion = TrackStatus.Ready;
        track.DeletedAt = DateTimeOffset.UtcNow.AddDays(-29);
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddMinutes(1); // Still valid
        _sessionFake.StoreDocument($"Tracks/01HXK123", track);

        // Act
        var result = await _sut.RestoreTrackAsync("01HXK123", "user1");

        // Assert
        result.Status.ShouldBe(TrackStatus.Ready);
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static Track CreateTestTrack(string trackId, string userId) => new()
    {
        Id = $"Tracks/{trackId}",
        TrackId = trackId,
        UserId = userId,
        Title = "Test Track",
        Artist = "Test Artist",
        Duration = TimeSpan.FromMinutes(3),
        ObjectKey = $"audio/{userId}/{trackId}/original.mp3",
        FileSizeBytes = 5_000_000,
        MimeType = "audio/mpeg",
        Status = TrackStatus.Ready,
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
        UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
    };
}
