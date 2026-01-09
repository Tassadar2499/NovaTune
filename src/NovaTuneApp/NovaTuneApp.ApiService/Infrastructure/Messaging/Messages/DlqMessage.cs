namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

/// <summary>
/// Dead Letter Queue message schema per 06-error-handling.md.
/// Published when message processing fails after all retries.
/// </summary>
public record DlqMessage
{
    /// <summary>
    /// Original topic the message was consumed from.
    /// </summary>
    public required string OriginalTopic { get; init; }

    /// <summary>
    /// Original message key (partition key).
    /// </summary>
    public required string OriginalKey { get; init; }

    /// <summary>
    /// Original message payload (JSON string).
    /// </summary>
    public required string OriginalPayload { get; init; }

    /// <summary>
    /// Error message from the exception.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Full stack trace of the exception.
    /// </summary>
    public required string ErrorStackTrace { get; init; }

    /// <summary>
    /// Number of retry attempts before DLQ.
    /// </summary>
    public required int RetryCount { get; init; }

    /// <summary>
    /// Timestamp when the message was moved to DLQ.
    /// </summary>
    public required DateTimeOffset FailedAt { get; init; }
}
