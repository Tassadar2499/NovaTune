namespace NovaTuneApp.ApiService.Exceptions;

/// <summary>
/// Exception thrown for upload-related errors.
/// </summary>
public class UploadException : Exception
{
    public UploadErrorType ErrorType { get; }
    public int StatusCode { get; }
    public IDictionary<string, object>? Extensions { get; }

    public UploadException(
        UploadErrorType errorType,
        string message,
        int statusCode = 400,
        IDictionary<string, object>? extensions = null)
        : base(message)
    {
        ErrorType = errorType;
        StatusCode = statusCode;
        Extensions = extensions;
    }
}
