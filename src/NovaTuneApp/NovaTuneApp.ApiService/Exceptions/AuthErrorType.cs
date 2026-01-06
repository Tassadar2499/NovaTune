namespace NovaTuneApp.ApiService.Exceptions;

/// <summary>
/// Types of authentication errors.
/// </summary>
public enum AuthErrorType
{
    InvalidCredentials,
    AccountDisabled,
    AccountPendingDeletion,
    EmailExists,
    TokenExpired,
    InvalidToken,
    SessionLimitExceeded,
    ValidationError
}
