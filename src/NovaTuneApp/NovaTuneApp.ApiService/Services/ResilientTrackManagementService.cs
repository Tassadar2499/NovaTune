using Microsoft.Extensions.Hosting;
using NovaTuneApp.ApiService.Models;
using Polly;
using Polly.Registry;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Decorator that wraps TrackManagementService with resilience policies.
/// Applies timeouts, retries, and circuit breakers per NF-1.4 spec.
/// </summary>
public class ResilientTrackManagementService : ITrackManagementService
{
    private readonly ITrackManagementService _inner;
    private readonly ResiliencePipeline _readPipeline;
    private readonly ResiliencePipeline _writePipeline;
    private readonly ILogger<ResilientTrackManagementService> _logger;

    public ResilientTrackManagementService(
        ITrackManagementService inner,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ResilientTrackManagementService> logger)
    {
        _inner = inner;
        _logger = logger;
        // Read pipeline: 2s timeout, 1 retry (for single document loads and queries)
        _readPipeline = pipelineProvider.GetPipeline(ResilienceExtensions.DatabaseReadPipeline);
        // Write pipeline: 5s timeout, 0 retries (for updates with optimistic concurrency)
        _writePipeline = pipelineProvider.GetPipeline(ResilienceExtensions.DatabaseWritePipeline);
    }

    public async Task<PagedResult<TrackListItem>> ListTracksAsync(
        string userId,
        TrackListQuery query,
        CancellationToken ct = default)
    {
        // List uses read pipeline (5s timeout via query, 1 retry)
        return await _readPipeline.ExecuteAsync(
            async token => await _inner.ListTracksAsync(userId, query, token),
            ct);
    }

    public async Task<TrackDetails> GetTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        // Get uses read pipeline (2s timeout, 1 retry)
        return await _readPipeline.ExecuteAsync(
            async token => await _inner.GetTrackAsync(trackId, userId, token),
            ct);
    }

    public async Task<TrackDetails> UpdateTrackAsync(
        string trackId,
        string userId,
        UpdateTrackRequest request,
        CancellationToken ct = default)
    {
        // Update uses write pipeline (5s timeout, 0 retries for optimistic concurrency)
        return await _writePipeline.ExecuteAsync(
            async token => await _inner.UpdateTrackAsync(trackId, userId, request, token),
            ct);
    }

    public async Task DeleteTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        // Delete uses write pipeline (5s timeout, 0 retries)
        await _writePipeline.ExecuteAsync(
            async token => await _inner.DeleteTrackAsync(trackId, userId, token),
            ct);
    }

    public async Task<TrackDetails> RestoreTrackAsync(
        string trackId,
        string userId,
        CancellationToken ct = default)
    {
        // Restore uses write pipeline (5s timeout, 0 retries)
        return await _writePipeline.ExecuteAsync(
            async token => await _inner.RestoreTrackAsync(trackId, userId, token),
            ct);
    }
}
