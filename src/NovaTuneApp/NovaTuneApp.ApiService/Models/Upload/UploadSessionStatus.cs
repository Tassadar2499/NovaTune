namespace NovaTuneApp.ApiService.Models.Upload;

/// <summary>
/// Status of an upload session.
/// </summary>
public enum UploadSessionStatus
{
    /// <summary>
    /// Awaiting client upload via presigned URL.
    /// </summary>
    Pending,

    /// <summary>
    /// MinIO notification received, Track record created.
    /// </summary>
    Completed,

    /// <summary>
    /// TTL passed without completion.
    /// </summary>
    Expired,

    /// <summary>
    /// Validation failed on notification processing.
    /// </summary>
    Failed
}
