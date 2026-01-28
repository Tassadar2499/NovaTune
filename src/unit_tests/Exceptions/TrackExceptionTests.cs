using NovaTuneApp.ApiService.Exceptions;

namespace NovaTune.UnitTests.Exceptions;

/// <summary>
/// Unit tests for track-related exception types.
/// Verifies that exceptions properly capture and expose relevant context.
/// </summary>
public class TrackExceptionTests
{
    // ========================================================================
    // TrackNotFoundException Tests
    // ========================================================================

    [Fact]
    public void TrackNotFoundException_ContainsTrackId()
    {
        // Arrange & Act
        var ex = new TrackNotFoundException("01HXK123");

        // Assert
        ex.TrackId.ShouldBe("01HXK123");
        ex.Message.ShouldContain("01HXK123");
    }

    [Fact]
    public void TrackNotFoundException_MessageIndicatesNotFound()
    {
        // Arrange & Act
        var ex = new TrackNotFoundException("01HXK123");

        // Assert
        ex.Message.ShouldContain("not found", Case.Insensitive);
    }

    // ========================================================================
    // TrackAccessDeniedException Tests
    // ========================================================================

    [Fact]
    public void TrackAccessDeniedException_ContainsTrackId()
    {
        // Arrange & Act
        var ex = new TrackAccessDeniedException("01HXK456");

        // Assert
        ex.TrackId.ShouldBe("01HXK456");
        ex.Message.ShouldContain("01HXK456");
    }

    [Fact]
    public void TrackAccessDeniedException_MessageIndicatesAccessDenied()
    {
        // Arrange & Act
        var ex = new TrackAccessDeniedException("01HXK456");

        // Assert
        ex.Message.ShouldContain("denied", Case.Insensitive);
    }

    // ========================================================================
    // TrackDeletedException Tests
    // ========================================================================

    [Fact]
    public void TrackDeletedException_ContainsTrackId()
    {
        // Arrange & Act
        var ex = new TrackDeletedException("01HXK789");

        // Assert
        ex.TrackId.ShouldBe("01HXK789");
        ex.Message.ShouldContain("01HXK789");
    }

    [Fact]
    public void TrackDeletedException_ContainsDeletedAt_WhenProvided()
    {
        // Arrange
        var deletedAt = DateTimeOffset.UtcNow.AddHours(-2);

        // Act
        var ex = new TrackDeletedException("01HXK789", deletedAt);

        // Assert
        ex.TrackId.ShouldBe("01HXK789");
        ex.DeletedAt.ShouldBe(deletedAt);
    }

    [Fact]
    public void TrackDeletedException_DeletedAtIsNull_WhenNotProvided()
    {
        // Arrange & Act
        var ex = new TrackDeletedException("01HXK789");

        // Assert
        ex.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void TrackDeletedException_MessageIndicatesDeleted()
    {
        // Arrange & Act
        var ex = new TrackDeletedException("01HXK789");

        // Assert
        ex.Message.ShouldContain("deleted", Case.Insensitive);
    }

    // ========================================================================
    // TrackRestorationExpiredException Tests
    // ========================================================================

    [Fact]
    public void TrackRestorationExpiredException_ContainsAllTimestamps()
    {
        // Arrange
        var deletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        var scheduledAt = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        var ex = new TrackRestorationExpiredException("01HXKABC", deletedAt, scheduledAt);

        // Assert
        ex.TrackId.ShouldBe("01HXKABC");
        ex.DeletedAt.ShouldBe(deletedAt);
        ex.ScheduledDeletionAt.ShouldBe(scheduledAt);
    }

    [Fact]
    public void TrackRestorationExpiredException_MessageIndicatesExpired()
    {
        // Arrange
        var deletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        var scheduledAt = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        var ex = new TrackRestorationExpiredException("01HXKABC", deletedAt, scheduledAt);

        // Assert
        ex.Message.ShouldContain("expired", Case.Insensitive);
    }

    [Fact]
    public void TrackRestorationExpiredException_MessageMentionsCannotBeRestored()
    {
        // Arrange
        var deletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        var scheduledAt = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        var ex = new TrackRestorationExpiredException("01HXKABC", deletedAt, scheduledAt);

        // Assert
        ex.Message.ShouldContain("cannot be restored", Case.Insensitive);
    }

    // ========================================================================
    // TrackAlreadyDeletedException Tests
    // ========================================================================

    [Fact]
    public void TrackAlreadyDeletedException_ContainsTrackId()
    {
        // Arrange & Act
        var ex = new TrackAlreadyDeletedException("01HXKDEF");

        // Assert
        ex.TrackId.ShouldBe("01HXKDEF");
        ex.Message.ShouldContain("01HXKDEF");
    }

    [Fact]
    public void TrackAlreadyDeletedException_MessageIndicatesAlreadyDeleted()
    {
        // Arrange & Act
        var ex = new TrackAlreadyDeletedException("01HXKDEF");

        // Assert
        ex.Message.ShouldContain("already deleted", Case.Insensitive);
    }

    // ========================================================================
    // TrackNotDeletedException Tests
    // ========================================================================

    [Fact]
    public void TrackNotDeletedException_ContainsTrackId()
    {
        // Arrange & Act
        var ex = new TrackNotDeletedException("01HXKGHI");

        // Assert
        ex.TrackId.ShouldBe("01HXKGHI");
        ex.Message.ShouldContain("01HXKGHI");
    }

    [Fact]
    public void TrackNotDeletedException_MessageIndicatesNotDeleted()
    {
        // Arrange & Act
        var ex = new TrackNotDeletedException("01HXKGHI");

        // Assert
        ex.Message.ShouldContain("not deleted", Case.Insensitive);
    }

    // ========================================================================
    // TrackConcurrencyException Tests
    // ========================================================================

    [Fact]
    public void TrackConcurrencyException_ContainsTrackId()
    {
        // Arrange & Act
        var ex = new TrackConcurrencyException("01HXKJKL");

        // Assert
        ex.TrackId.ShouldBe("01HXKJKL");
        ex.Message.ShouldContain("01HXKJKL");
    }

    [Fact]
    public void TrackConcurrencyException_MessageIndicatesConcurrency()
    {
        // Arrange & Act
        var ex = new TrackConcurrencyException("01HXKJKL");

        // Assert
        ex.Message.ShouldContain("modified", Case.Insensitive);
    }

    // ========================================================================
    // Inheritance Tests
    // ========================================================================

    [Fact]
    public void AllTrackExceptions_InheritFromException()
    {
        // Assert
        typeof(Exception).IsAssignableFrom(typeof(TrackNotFoundException)).ShouldBeTrue();
        typeof(Exception).IsAssignableFrom(typeof(TrackAccessDeniedException)).ShouldBeTrue();
        typeof(Exception).IsAssignableFrom(typeof(TrackDeletedException)).ShouldBeTrue();
        typeof(Exception).IsAssignableFrom(typeof(TrackRestorationExpiredException)).ShouldBeTrue();
        typeof(Exception).IsAssignableFrom(typeof(TrackAlreadyDeletedException)).ShouldBeTrue();
        typeof(Exception).IsAssignableFrom(typeof(TrackNotDeletedException)).ShouldBeTrue();
        typeof(Exception).IsAssignableFrom(typeof(TrackConcurrencyException)).ShouldBeTrue();
    }
}
