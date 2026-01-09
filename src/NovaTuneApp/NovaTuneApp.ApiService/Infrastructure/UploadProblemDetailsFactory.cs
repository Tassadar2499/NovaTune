using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Exceptions;

namespace NovaTuneApp.ApiService.Infrastructure;

/// <summary>
/// Factory for creating RFC 7807 Problem Details for upload errors.
/// </summary>
public static class UploadProblemDetailsFactory
{
    private const string BaseUri = "https://novatune.dev/errors/";

    public static ProblemDetails Create(UploadException ex, HttpContext context)
    {
        var (type, title) = ex.ErrorType switch
        {
            UploadErrorType.UnsupportedMimeType => (
                "unsupported-mime-type",
                "Unsupported File Type"),
            UploadErrorType.FileTooLarge => (
                "file-too-large",
                "File Too Large"),
            UploadErrorType.QuotaExceeded => (
                "quota-exceeded",
                "Storage Quota Exceeded"),
            UploadErrorType.InvalidFileName => (
                "invalid-file-name",
                "Invalid File Name"),
            UploadErrorType.SessionNotFound => (
                "session-not-found",
                "Upload Session Not Found"),
            UploadErrorType.SessionExpired => (
                "session-expired",
                "Upload Session Expired"),
            UploadErrorType.ServiceUnavailable => (
                "service-unavailable",
                "Service Temporarily Unavailable"),
            _ => (
                "upload-error",
                "Upload Error")
        };

        var problemDetails = new ProblemDetails
        {
            Type = $"{BaseUri}{type}",
            Title = title,
            Status = ex.StatusCode,
            Detail = ex.Message,
            Instance = context.Request.Path,
            Extensions =
            {
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier
            }
        };

        // Add custom extensions (e.g., usedBytes, quotaBytes)
        if (ex.Extensions != null)
        {
            foreach (var ext in ex.Extensions)
            {
                problemDetails.Extensions[ext.Key] = ext.Value;
            }
        }

        return problemDetails;
    }
}
