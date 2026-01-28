namespace NovaTuneApp.ApiService.Exceptions;

/// <summary>
/// Thrown when a track is not found.
/// </summary>
public class TrackNotFoundException : Exception
{
    public string TrackId { get; }

    public TrackNotFoundException(string trackId)
        : base($"Track '{trackId}' was not found.")
    {
        TrackId = trackId;
    }
}

/// <summary>
/// Thrown when user does not have access to a track.
/// </summary>
public class TrackAccessDeniedException : Exception
{
    public string TrackId { get; }

    public TrackAccessDeniedException(string trackId)
        : base($"Access denied to track '{trackId}'.")
    {
        TrackId = trackId;
    }
}

/// <summary>
/// Thrown when attempting to modify a deleted track.
/// </summary>
public class TrackDeletedException : Exception
{
    public string TrackId { get; }
    public DateTimeOffset? DeletedAt { get; }

    public TrackDeletedException(string trackId, DateTimeOffset? deletedAt = null)
        : base($"Track '{trackId}' is deleted.")
    {
        TrackId = trackId;
        DeletedAt = deletedAt;
    }
}

/// <summary>
/// Thrown when attempting to restore a track outside the grace period.
/// </summary>
public class TrackRestorationExpiredException : Exception
{
    public string TrackId { get; }
    public DateTimeOffset DeletedAt { get; }
    public DateTimeOffset ScheduledDeletionAt { get; }

    public TrackRestorationExpiredException(
        string trackId,
        DateTimeOffset deletedAt,
        DateTimeOffset scheduledDeletionAt)
        : base($"Track '{trackId}' cannot be restored. Grace period expired.")
    {
        TrackId = trackId;
        DeletedAt = deletedAt;
        ScheduledDeletionAt = scheduledDeletionAt;
    }
}

/// <summary>
/// Thrown when attempting to delete an already deleted track.
/// </summary>
public class TrackAlreadyDeletedException : Exception
{
    public string TrackId { get; }

    public TrackAlreadyDeletedException(string trackId)
        : base($"Track '{trackId}' is already deleted.")
    {
        TrackId = trackId;
    }
}

/// <summary>
/// Thrown when attempting to restore a track that is not deleted.
/// </summary>
public class TrackNotDeletedException : Exception
{
    public string TrackId { get; }

    public TrackNotDeletedException(string trackId)
        : base($"Track '{trackId}' is not deleted.")
    {
        TrackId = trackId;
    }
}

/// <summary>
/// Thrown when a concurrent modification is detected.
/// </summary>
public class TrackConcurrencyException : Exception
{
    public string TrackId { get; }

    public TrackConcurrencyException(string trackId)
        : base($"Track '{trackId}' was modified by another request.")
    {
        TrackId = trackId;
    }
}
