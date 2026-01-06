using System.Threading.RateLimiting;

namespace NovaTuneApp.ApiService.Infrastructure.RateLimiting;

/// <summary>
/// A lease that wraps two chained leases.
/// </summary>
internal sealed class ChainedRateLimitLease : RateLimitLease
{
    private readonly RateLimitLease _first;
    private readonly RateLimitLease _second;

    public ChainedRateLimitLease(RateLimitLease first, RateLimitLease second)
    {
        _first = first;
        _second = second;
    }

    public override bool IsAcquired => _first.IsAcquired && _second.IsAcquired;

    public override IEnumerable<string> MetadataNames
    {
        get
        {
            foreach (var name in _first.MetadataNames)
                yield return name;
            foreach (var name in _second.MetadataNames)
                yield return name;
        }
    }

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        // Prefer the shorter retry-after if both have it
        if (metadataName == MetadataName.RetryAfter.Name)
        {
            var hasFirst = _first.TryGetMetadata(metadataName, out var firstMeta);
            var hasSecond = _second.TryGetMetadata(metadataName, out var secondMeta);

            if (hasFirst && hasSecond)
            {
                var firstRetry = (TimeSpan)firstMeta!;
                var secondRetry = (TimeSpan)secondMeta!;
                metadata = firstRetry > secondRetry ? firstRetry : secondRetry;
                return true;
            }

            if (hasFirst)
            {
                metadata = firstMeta;
                return true;
            }

            if (hasSecond)
            {
                metadata = secondMeta;
                return true;
            }
        }

        return _first.TryGetMetadata(metadataName, out metadata)
            || _second.TryGetMetadata(metadataName, out metadata);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _first.Dispose();
            _second.Dispose();
        }
    }
}
