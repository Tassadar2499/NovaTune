using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging;

/// <summary>
/// No-op implementation of IMessageProducerService for testing environments.
/// </summary>
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

    public Task PublishTelemetryEventAsync(TelemetryEvent evt, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
