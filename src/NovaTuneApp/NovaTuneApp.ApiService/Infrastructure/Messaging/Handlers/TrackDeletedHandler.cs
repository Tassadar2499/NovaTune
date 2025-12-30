using KafkaFlow;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Services;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles TrackDeletedEvent messages for cleanup operations.
/// </summary>
public class TrackDeletedHandler : IMessageHandler<TrackDeletedEvent>
{
    private readonly ILogger<TrackDeletedHandler> _logger;
    private readonly IStorageService _storageService;
    private readonly ICacheService _cacheService;

    public TrackDeletedHandler(
        ILogger<TrackDeletedHandler> logger,
        IStorageService storageService,
        ICacheService cacheService)
    {
        _logger = logger;
        _storageService = storageService;
        _cacheService = cacheService;
    }

    public async Task Handle(IMessageContext context, TrackDeletedEvent message)
    {
        _logger.LogInformation("Processing deletion for track {TrackId}", message.TrackId);

        // Invalidate cache
        await _cacheService.RemoveAsync($"presigned:{message.UserId}:{message.TrackId}");

        // Schedule storage cleanup (with grace period)
        await _storageService.ScheduleDeletionAsync(message.TrackId, TimeSpan.FromHours(24));

        _logger.LogInformation("Completed deletion processing for track {TrackId}", message.TrackId);
    }
}
