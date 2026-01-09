namespace NovaTuneApp.Workers.UploadIngestor.Services;

/// <summary>
/// Service for processing upload events from MinIO bucket notifications.
/// </summary>
public interface IUploadIngestorService
{
    /// <summary>
    /// Processes an upload event by validating the upload, creating a Track record,
    /// updating the UploadSession, and inserting an outbox message.
    /// </summary>
    /// <param name="objectKey">The MinIO object key.</param>
    /// <param name="contentType">The content type of the uploaded object.</param>
    /// <param name="size">The size of the uploaded object in bytes.</param>
    /// <param name="eTag">The ETag (MD5 hash) of the uploaded object.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessUploadAsync(
        string objectKey,
        string contentType,
        long size,
        string eTag,
        CancellationToken ct = default);
}
