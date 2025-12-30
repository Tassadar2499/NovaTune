namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Stub implementation of ITrackService for messaging handler dependencies.
/// </summary>
public class TrackService : ITrackService
{
    private readonly ILogger<TrackService> _logger;

    public TrackService(ILogger<TrackService> logger)
    {
        _logger = logger;
    }

    public Task ProcessUploadedTrackAsync(Guid trackId, CancellationToken ct = default)
    {
        _logger.LogInformation("TrackService.ProcessUploadedTrackAsync called for {TrackId} (stub)", trackId);
        return Task.CompletedTask;
    }
}
