using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging;

/// <summary>
/// Abstraction for publishing messages to Kafka/Redpanda.
/// </summary>
public interface IMessageProducerService
{
    /// <summary>
    /// Publishes an audio uploaded event.
    /// </summary>
    Task PublishAudioUploadedAsync(AudioUploadedEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Publishes a track deleted event.
    /// </summary>
    Task PublishTrackDeletedAsync(TrackDeletedEvent evt, CancellationToken ct = default);
}
