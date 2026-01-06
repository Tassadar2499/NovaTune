namespace NovaTuneApp.ApiService.Exceptions;

/// <summary>
/// Exception thrown for authentication-related errors.
/// </summary>
public class AuthException : Exception
{
    public AuthErrorType ErrorType { get; }
    public int StatusCode { get; }

    public AuthException(AuthErrorType errorType, string message, int statusCode = 400)
        : base(message)
    {
        ErrorType = errorType;
        StatusCode = statusCode;
    }
}
