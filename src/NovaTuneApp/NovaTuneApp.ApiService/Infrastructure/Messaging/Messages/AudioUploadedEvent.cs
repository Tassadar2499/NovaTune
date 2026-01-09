namespace NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;

/// <summary>
/// Event published when an audio file is uploaded.
/// Per cross-cutting decision 3.1, IDs use ULID string format.
/// </summary>
public record AudioUploadedEvent
{
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Track identifier (ULID string).
    /// </summary>
    public required string TrackId { get; init; }

    /// <summary>
    /// User identifier (ULID string).
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// MinIO object key for the uploaded file.
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// MIME type of the audio file.
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>
    /// SHA-256 checksum hex string for integrity verification (Req 2.6).
    /// </summary>
    public required string Checksum { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// When the upload completed.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}
