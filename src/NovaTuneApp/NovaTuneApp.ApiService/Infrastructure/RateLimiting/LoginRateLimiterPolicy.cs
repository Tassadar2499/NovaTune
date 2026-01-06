using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;

namespace NovaTuneApp.ApiService.Infrastructure.RateLimiting;

/// <summary>
/// Tracks a rate limiter with its last access time for cleanup purposes.
/// </summary>
internal sealed class TrackedRateLimiter
{
    public RateLimiter Limiter { get; }
    public DateTime LastAccessed { get; set; }

    public TrackedRateLimiter(RateLimiter limiter)
    {
        Limiter = limiter;
        LastAccessed = DateTime.UtcNow;
    }
}

/// <summary>
/// Custom rate limiter policy for login that applies both IP-based and per-account limits.
/// Implements Req 8.2: 10 requests per IP per minute, 5 requests per account per minute.
/// Includes automatic cleanup of stale rate limiters to prevent memory leaks.
/// </summary>
public class LoginRateLimiterPolicy : IRateLimiterPolicy<string>, IDisposable
{
    private readonly RateLimitSettings _settings;
    private readonly ConcurrentDictionary<string, TrackedRateLimiter> _ipLimiters = new();
    private readonly ConcurrentDictionary<string, TrackedRateLimiter> _accountLimiters = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _staleThreshold = TimeSpan.FromMinutes(10);
    private bool _disposed;

    public LoginRateLimiterPolicy(IOptions<RateLimitSettings> settings)
        : this(settings.Value)
    {
    }

    public LoginRateLimiterPolicy(RateLimitSettings settings)
    {
        _settings = settings;
        _cleanupTimer = new Timer(CleanupStaleEntries, null, _cleanupInterval, _cleanupInterval);
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var ipKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var accountKey = httpContext.Items.TryGetValue("login-email", out var email) && email is string emailStr
            ? emailStr
            : null;

        // Create a composite partition key
        var partitionKey = accountKey != null ? $"{ipKey}|{accountKey}" : ipKey;

        return RateLimitPartition.Get(partitionKey, key =>
        {
            var parts = key.Split('|');
            var ip = parts[0];
            var account = parts.Length > 1 ? parts[1] : null;

            // Get or create IP limiter
            var trackedIpLimiter = _ipLimiters.GetOrAdd(ip, _ => new TrackedRateLimiter(
                new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = _settings.Auth.LoginPerIp.PermitLimit,
                    Window = TimeSpan.FromMinutes(_settings.Auth.LoginPerIp.WindowMinutes),
                    SegmentsPerWindow = 4,
                    AutoReplenishment = true
                })));
            trackedIpLimiter.LastAccessed = DateTime.UtcNow;

            // If we have an account, chain with account limiter
            if (account != null)
            {
                var trackedAccountLimiter = _accountLimiters.GetOrAdd(account, _ => new TrackedRateLimiter(
                    new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = _settings.Auth.LoginPerAccount.PermitLimit,
                        Window = TimeSpan.FromMinutes(_settings.Auth.LoginPerAccount.WindowMinutes),
                        SegmentsPerWindow = 4,
                        AutoReplenishment = true
                    })));
                trackedAccountLimiter.LastAccessed = DateTime.UtcNow;

                return new ChainedRateLimiter(trackedIpLimiter.Limiter, trackedAccountLimiter.Limiter);
            }

            return trackedIpLimiter.Limiter;
        });
    }

    private void CleanupStaleEntries(object? state)
    {
        var cutoff = DateTime.UtcNow - _staleThreshold;

        CleanupDictionary(_ipLimiters, cutoff);
        CleanupDictionary(_accountLimiters, cutoff);
    }

    private static void CleanupDictionary(ConcurrentDictionary<string, TrackedRateLimiter> dictionary, DateTime cutoff)
    {
        foreach (var kvp in dictionary)
        {
            if (kvp.Value.LastAccessed < cutoff)
            {
                if (dictionary.TryRemove(kvp.Key, out var removed))
                {
                    removed.Limiter.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer.Dispose();

        foreach (var kvp in _ipLimiters)
        {
            kvp.Value.Limiter.Dispose();
        }
        _ipLimiters.Clear();

        foreach (var kvp in _accountLimiters)
        {
            kvp.Value.Limiter.Dispose();
        }
        _accountLimiters.Clear();
    }
}
