namespace NovaTuneApp.ApiService.Models.Upload;

/// <summary>
/// RavenDB document for tracking upload session state.
/// Created when client initiates upload, updated when MinIO notification arrives.
/// </summary>
public sealed class UploadSession
{
    /// <summary>
    /// RavenDB document ID (format: "UploadSessions/{uploadId}").
    /// </summary>
    public string Id { get; init; } = null!;

    /// <summary>
    /// External identifier (ULID format).
    /// </summary>
    public required string UploadId { get; init; }

    /// <summary>
    /// User who initiated the upload (ULID format).
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Pre-allocated Track ID (ULID format).
    /// Track record is created only after MinIO confirms upload.
    /// </summary>
    public required string ReservedTrackId { get; init; }

    /// <summary>
    /// MinIO object key (format: "audio/{userId}/{trackId}/{randomSuffix}").
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// Expected content type for validation.
    /// </summary>
    public required string ExpectedMimeType { get; init; }

    /// <summary>
    /// Maximum allowed file size for validation.
    /// </summary>
    public required long MaxAllowedSizeBytes { get; init; }

    /// <summary>
    /// Session creation timestamp.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Session expiry timestamp (presigned URL expiry).
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Current session status.
    /// </summary>
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Pending;

    /// <summary>
    /// Optional track title (defaults to filename without extension).
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Optional artist name.
    /// </summary>
    public string? Artist { get; init; }
}
