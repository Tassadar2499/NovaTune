namespace NovaTuneApp.ApiService.Exceptions;

/// <summary>
/// Thrown when a playlist is not found.
/// </summary>
public class PlaylistNotFoundException : Exception
{
    public string PlaylistId { get; }

    public PlaylistNotFoundException(string playlistId)
        : base($"Playlist '{playlistId}' was not found.")
    {
        PlaylistId = playlistId;
    }
}

/// <summary>
/// Thrown when user does not have access to a playlist.
/// </summary>
public class PlaylistAccessDeniedException : Exception
{
    public string PlaylistId { get; }

    public PlaylistAccessDeniedException(string playlistId)
        : base($"Access denied to playlist '{playlistId}'.")
    {
        PlaylistId = playlistId;
    }
}

/// <summary>
/// Thrown when user exceeds maximum playlist quota.
/// </summary>
public class PlaylistQuotaExceededException : Exception
{
    public string UserId { get; }
    public int CurrentCount { get; }
    public int MaxCount { get; }

    public PlaylistQuotaExceededException(string userId, int currentCount, int maxCount)
        : base($"User has reached maximum playlist limit ({maxCount}).")
    {
        UserId = userId;
        CurrentCount = currentCount;
        MaxCount = maxCount;
    }
}

/// <summary>
/// Thrown when adding tracks would exceed the playlist track limit.
/// </summary>
public class PlaylistTrackLimitExceededException : Exception
{
    public string PlaylistId { get; }
    public int CurrentCount { get; }
    public int AddCount { get; }
    public int MaxCount { get; }

    public PlaylistTrackLimitExceededException(
        string playlistId,
        int currentCount,
        int addCount,
        int maxCount)
        : base($"Cannot add {addCount} tracks. Playlist has {currentCount} tracks, limit is {maxCount}.")
    {
        PlaylistId = playlistId;
        CurrentCount = currentCount;
        AddCount = addCount;
        MaxCount = maxCount;
    }
}

/// <summary>
/// Thrown when a track is not found at the specified position in a playlist.
/// </summary>
public class PlaylistTrackNotFoundException : Exception
{
    public string PlaylistId { get; }
    public int Position { get; }

    public PlaylistTrackNotFoundException(string playlistId, int position)
        : base($"No track found at position {position} in playlist '{playlistId}'.")
    {
        PlaylistId = playlistId;
        Position = position;
    }
}

/// <summary>
/// Thrown when a position is out of valid range.
/// </summary>
public class InvalidPositionException : Exception
{
    public int Position { get; }
    public int MaxPosition { get; }

    public InvalidPositionException(int position, int maxPosition)
        : base($"Position {position} is out of range. Valid range: 0 to {maxPosition - 1}.")
    {
        Position = position;
        MaxPosition = maxPosition;
    }
}

/// <summary>
/// Thrown when a concurrent modification is detected on a playlist.
/// </summary>
public class PlaylistConcurrencyException : Exception
{
    public string PlaylistId { get; }

    public PlaylistConcurrencyException(string playlistId)
        : base($"Playlist '{playlistId}' was modified by another request.")
    {
        PlaylistId = playlistId;
    }
}
