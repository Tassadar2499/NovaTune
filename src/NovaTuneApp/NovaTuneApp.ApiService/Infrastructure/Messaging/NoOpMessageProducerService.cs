using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging;

public class NoOpMessageProducerService : IMessageProducerService
{
    public Task PublishAudioUploadedAsync(AudioUploadedEvent evt, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task PublishTrackDeletedAsync(TrackDeletedEvent evt, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
