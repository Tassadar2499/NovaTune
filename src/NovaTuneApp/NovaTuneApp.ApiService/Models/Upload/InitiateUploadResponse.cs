namespace NovaTuneApp.ApiService.Models.Upload;

/// <summary>
/// Response from upload initiation with presigned URL for direct upload.
/// </summary>
public record InitiateUploadResponse(
    /// <summary>
    /// Upload session identifier (ULID).
    /// </summary>
    string UploadId,

    /// <summary>
    /// Reserved track identifier (ULID). Track record created after upload completes.
    /// </summary>
    string TrackId,

    /// <summary>
    /// Presigned PUT URL for direct client upload to MinIO.
    /// </summary>
    string PresignedUrl,

    /// <summary>
    /// Presigned URL expiry timestamp.
    /// </summary>
    DateTimeOffset ExpiresAt,

    /// <summary>
    /// Storage object key (for reference).
    /// </summary>
    string ObjectKey);
