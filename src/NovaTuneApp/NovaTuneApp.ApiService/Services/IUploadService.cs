using NovaTuneApp.ApiService.Models.Upload;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Service for handling track upload operations.
/// </summary>
public interface IUploadService
{
    /// <summary>
    /// Initiates an upload session and generates a presigned URL for direct client upload.
    /// </summary>
    /// <param name="userId">The user initiating the upload.</param>
    /// <param name="request">Upload request details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Upload initiation response with presigned URL.</returns>
    Task<InitiateUploadResponse> InitiateUploadAsync(
        string userId,
        InitiateUploadRequest request,
        CancellationToken ct = default);
}
