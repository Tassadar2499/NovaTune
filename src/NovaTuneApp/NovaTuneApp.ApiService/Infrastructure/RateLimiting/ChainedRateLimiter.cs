using System.Threading.RateLimiting;

namespace NovaTuneApp.ApiService.Infrastructure.RateLimiting;

/// <summary>
/// A rate limiter that chains two limiters together.
/// Both limiters must permit the request for it to succeed.
/// </summary>
internal sealed class ChainedRateLimiter : RateLimiter
{
    private readonly RateLimiter _first;
    private readonly RateLimiter _second;

    public ChainedRateLimiter(RateLimiter first, RateLimiter second)
    {
        _first = first;
        _second = second;
    }

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics()
    {
        var first = _first.GetStatistics();
        var second = _second.GetStatistics();

        if (first == null || second == null)
            return null;

        return new RateLimiterStatistics
        {
            CurrentAvailablePermits = Math.Min(first.CurrentAvailablePermits, second.CurrentAvailablePermits),
            CurrentQueuedCount = first.CurrentQueuedCount + second.CurrentQueuedCount,
            TotalFailedLeases = first.TotalFailedLeases + second.TotalFailedLeases,
            TotalSuccessfulLeases = Math.Min(first.TotalSuccessfulLeases, second.TotalSuccessfulLeases)
        };
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var firstLease = _first.AttemptAcquire(permitCount);
        if (!firstLease.IsAcquired)
        {
            return firstLease;
        }

        var secondLease = _second.AttemptAcquire(permitCount);
        if (!secondLease.IsAcquired)
        {
            firstLease.Dispose();
            return secondLease;
        }

        return new ChainedRateLimitLease(firstLease, secondLease);
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var firstLease = await _first.AcquireAsync(permitCount, cancellationToken);
        if (!firstLease.IsAcquired)
        {
            return firstLease;
        }

        var secondLease = await _second.AcquireAsync(permitCount, cancellationToken);
        if (!secondLease.IsAcquired)
        {
            firstLease.Dispose();
            return secondLease;
        }

        return new ChainedRateLimitLease(firstLease, secondLease);
    }

    protected override void Dispose(bool disposing)
    {
        // Don't dispose the underlying limiters - they're shared
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return ValueTask.CompletedTask;
    }
}
