namespace NovaTuneApp.ApiService.Exceptions;

/// <summary>
/// Types of upload errors.
/// </summary>
public enum UploadErrorType
{
    UnsupportedMimeType,
    FileTooLarge,
    QuotaExceeded,
    InvalidFileName,
    SessionNotFound,
    SessionExpired,

    /// <summary>
    /// Service unavailable due to dependency failure (NF-1.4 fail-closed behavior).
    /// Returns HTTP 503.
    /// </summary>
    ServiceUnavailable
}
