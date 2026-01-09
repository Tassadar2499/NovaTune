using KafkaFlow;
using KafkaFlow.Producers;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging;

/// <summary>
/// KafkaFlow-backed implementation of IMessageProducerService.
/// </summary>
public class MessageProducerService : IMessageProducerService
{
    private readonly IMessageProducer _audioProducer;
    private readonly IMessageProducer _deletionProducer;

    public MessageProducerService(IProducerAccessor producerAccessor)
    {
        _audioProducer = producerAccessor.GetProducer("audio-producer");
        _deletionProducer = producerAccessor.GetProducer("deletion-producer");
    }

    public async Task PublishAudioUploadedAsync(AudioUploadedEvent evt, CancellationToken ct = default)
    {
        await _audioProducer.ProduceAsync(
            messageKey: evt.TrackId,
            messageValue: evt,
            headers: new MessageHeaders { { "schema-version", "1"u8.ToArray() } }
        );
    }

    public async Task PublishTrackDeletedAsync(TrackDeletedEvent evt, CancellationToken ct = default)
    {
        await _deletionProducer.ProduceAsync(
            messageKey: evt.TrackId.ToString(),
            messageValue: evt
        );
    }
}
