namespace NovaTuneApp.ApiService.Models.Outbox;

/// <summary>
/// RavenDB document for the transactional outbox pattern.
/// Events are stored in the same transaction as domain changes,
/// then published to Kafka by a separate relay process.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// RavenDB document ID (format: "OutboxMessages/{ulid}").
    /// </summary>
    public string Id { get; init; } = null!;

    /// <summary>
    /// Message type for routing (e.g., "AudioUploadedEvent", "TrackDeletedEvent").
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Target Kafka topic name.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Optional partition key for message ordering.
    /// </summary>
    public string? PartitionKey { get; init; }

    /// <summary>
    /// Serialized message payload (JSON).
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Current processing status.
    /// </summary>
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    /// <summary>
    /// Number of publish attempts.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// When the message was successfully published.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Error message if publishing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of an outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>
    /// Awaiting publish to Kafka.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Successfully published.
    /// </summary>
    Published = 1,

    /// <summary>
    /// Failed after max retry attempts.
    /// </summary>
    Failed = 2
}
