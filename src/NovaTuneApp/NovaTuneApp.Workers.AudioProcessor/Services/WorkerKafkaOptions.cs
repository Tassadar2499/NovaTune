namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Kafka configuration options for the Audio Processor Worker.
/// Maps to appsettings.json Kafka section.
/// </summary>
public class WorkerKafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>
    /// Topic name prefix for environment isolation.
    /// Topics are constructed as {TopicPrefix}-{TopicName}.
    /// Default: dev
    /// </summary>
    public string TopicPrefix { get; set; } = "dev";

    /// <summary>
    /// Consumer group ID for the audio processor worker.
    /// Default: audio-processor-worker
    /// </summary>
    public string ConsumerGroup { get; set; } = "audio-processor-worker";

    /// <summary>
    /// Topic name configuration.
    /// </summary>
    public KafkaTopicsOptions Topics { get; set; } = new();

    /// <summary>
    /// Gets the full audio events topic name.
    /// </summary>
    public string GetAudioEventsTopic() => $"{TopicPrefix}-{Topics.AudioEvents}";

    /// <summary>
    /// Gets the full DLQ topic name.
    /// </summary>
    public string GetDlqTopic() => $"{TopicPrefix}-{Topics.AudioEventsDlq}";
}

/// <summary>
/// Topic name configuration for Kafka.
/// </summary>
public class KafkaTopicsOptions
{
    /// <summary>
    /// Audio events topic name (without prefix).
    /// Default: audio-events
    /// </summary>
    public string AudioEvents { get; set; } = "audio-events";

    /// <summary>
    /// Audio events DLQ topic name (without prefix).
    /// Default: audio-events-dlq
    /// </summary>
    public string AudioEventsDlq { get; set; } = "audio-events-dlq";
}
