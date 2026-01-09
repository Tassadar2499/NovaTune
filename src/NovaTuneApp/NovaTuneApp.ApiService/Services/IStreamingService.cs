namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for generating and caching presigned streaming URLs (Req 5.1, 5.2).
/// </summary>
public interface IStreamingService
{
    /// <summary>
    /// Gets or generates a presigned streaming URL for a track.
    /// </summary>
    /// <param name="trackId">Track identifier (ULID).</param>
    /// <param name="userId">Requesting user identifier (ULID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Streaming URL response with expiry.</returns>
    Task<StreamUrlResult> GetStreamUrlAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates cached streaming URL for a track.
    /// Called on track deletion or user logout.
    /// </summary>
    /// <param name="trackId">Track identifier (ULID).</param>
    /// <param name="userId">User identifier (ULID).</param>
    /// <param name="ct">Cancellation token.</param>
    Task InvalidateCacheAsync(
        string trackId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates all cached streaming URLs for a user.
    /// Called on user logout (all sessions).
    /// </summary>
    /// <param name="userId">User identifier (ULID).</param>
    /// <param name="ct">Cancellation token.</param>
    Task InvalidateAllUserCacheAsync(
        string userId,
        CancellationToken ct = default);
}

/// <summary>
/// Result of streaming URL generation.
/// </summary>
public record StreamUrlResult(
    string StreamUrl,
    DateTimeOffset ExpiresAt,
    string ContentType,
    long FileSizeBytes,
    bool SupportsRangeRequests);
