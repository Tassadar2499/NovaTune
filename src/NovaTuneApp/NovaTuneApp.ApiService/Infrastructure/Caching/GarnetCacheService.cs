using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Registry;
using StackExchange.Redis;

namespace NovaTuneApp.ApiService.Infrastructure.Caching;

/// <summary>
/// Redis/Garnet-backed implementation of ICacheService with resilience support (NF-1.4).
/// Operations are wrapped with timeout, circuit breaker, and bulkhead policies.
/// </summary>
public class GarnetCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ResiliencePipeline _resiliencePipeline;

    public GarnetCacheService(
        IConnectionMultiplexer redis,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _redis = redis;
        _resiliencePipeline = pipelineProvider.GetPipeline(ResilienceExtensions.CachePipeline);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
        }, ct);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, json, ttl);
        }, ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }, ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }, ct);
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());

            // Use SCAN to find all matching keys (safer than KEYS for production)
            var keys = new List<RedisKey>();
            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                keys.Add(key);
            }

            if (keys.Count > 0)
            {
                await db.KeyDeleteAsync(keys.ToArray());
            }
        }, ct);
    }
}
