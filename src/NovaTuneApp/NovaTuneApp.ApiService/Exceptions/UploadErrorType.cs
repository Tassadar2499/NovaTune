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
    SessionExpired
}
