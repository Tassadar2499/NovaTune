using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Fake implementation of IStreamingService for unit tests.
/// Tracks cache invalidation calls for verification.
/// </summary>
public class StreamingServiceFake : IStreamingService
{
    public List<(string TrackId, string UserId)> InvalidatedTracks { get; } = [];
    public List<string> InvalidatedUsers { get; } = [];
    public bool ThrowOnInvalidate { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<StreamUrlResult> GetStreamUrlAsync(string trackId, string userId, CancellationToken ct = default)
    {
        return Task.FromResult(new StreamUrlResult(
            $"https://fake-storage.example.com/{trackId}",
            DateTimeOffset.UtcNow.AddMinutes(5),
            "audio/mpeg",
            1024000,
            true));
    }

    public Task InvalidateCacheAsync(string trackId, string userId, CancellationToken ct = default)
    {
        if (ThrowOnInvalidate && ExceptionToThrow is not null)
            throw ExceptionToThrow;

        InvalidatedTracks.Add((trackId, userId));
        return Task.CompletedTask;
    }

    public Task InvalidateAllUserCacheAsync(string userId, CancellationToken ct = default)
    {
        InvalidatedUsers.Add(userId);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        InvalidatedTracks.Clear();
        InvalidatedUsers.Clear();
        ThrowOnInvalidate = false;
        ExceptionToThrow = null;
    }
}
