using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Exceptions;

namespace NovaTuneApp.ApiService.Infrastructure;

/// <summary>
/// Factory for creating RFC 7807 Problem Details for auth errors (Req 8.1).
/// </summary>
public static class AuthProblemDetailsFactory
{
    private const string BaseUri = "https://novatune.example/errors/";

    public static ProblemDetails Create(AuthException ex, HttpContext context)
    {
        var (type, title, detail) = ex.ErrorType switch
        {
            AuthErrorType.InvalidCredentials => (
                "invalid-credentials",
                "Invalid Credentials",
                "The email or password provided is incorrect."),
            AuthErrorType.AccountDisabled => (
                "account-disabled",
                "Account Disabled",
                "This account has been disabled."),
            AuthErrorType.AccountPendingDeletion => (
                "account-pending-deletion",
                "Account Pending Deletion",
                "This account is pending deletion."),
            AuthErrorType.EmailExists => (
                "email-exists",
                "Email Already Registered",
                "An account with this email already exists."),
            AuthErrorType.TokenExpired => (
                "token-expired",
                "Token Expired",
                "The token has expired."),
            AuthErrorType.InvalidToken => (
                "invalid-token",
                "Invalid Token",
                "The token is invalid or has been revoked."),
            AuthErrorType.SessionLimitExceeded => (
                "session-limit-exceeded",
                "Session Limit Exceeded",
                "Maximum number of active sessions reached."),
            AuthErrorType.ValidationError => (
                "validation-error",
                "Validation Error",
                ex.Message),
            _ => (
                "auth-error",
                "Authentication Error",
                ex.Message)
        };

        return new ProblemDetails
        {
            Type = $"{BaseUri}{type}",
            Title = title,
            Status = ex.StatusCode,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions =
            {
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier
            }
        };
    }

    public static IResult InvalidCredentials(HttpContext context) =>
        TypedResults.Problem(Create(
            new AuthException(AuthErrorType.InvalidCredentials, "", 401), context));

    public static IResult AccountDisabled(HttpContext context) =>
        TypedResults.Problem(Create(
            new AuthException(AuthErrorType.AccountDisabled, "", 403), context));

    public static IResult EmailExists(HttpContext context) =>
        TypedResults.Problem(Create(
            new AuthException(AuthErrorType.EmailExists, "", 409), context));

    public static IResult InvalidToken(HttpContext context) =>
        TypedResults.Problem(Create(
            new AuthException(AuthErrorType.InvalidToken, "", 401), context));

    public static IResult RateLimitExceeded(HttpContext context, TimeSpan? retryAfter = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"{BaseUri}rate-limit-exceeded",
            Title = "Rate Limit Exceeded",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "Too many requests. Please try again later.",
            Instance = context.Request.Path,
            Extensions =
            {
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier
            }
        };

        if (retryAfter.HasValue)
        {
            context.Response.Headers.RetryAfter = ((int)retryAfter.Value.TotalSeconds).ToString();
        }

        return TypedResults.Problem(problem);
    }
}
