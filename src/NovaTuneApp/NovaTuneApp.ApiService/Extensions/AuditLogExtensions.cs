using System.Diagnostics;
using System.Security.Claims;
using NovaTuneApp.ApiService.Models.Admin;

namespace NovaTuneApp.ApiService.Extensions;

/// <summary>
/// Extension methods for creating audit log requests from HttpContext.
/// </summary>
public static class AuditLogExtensions
{
    /// <summary>
    /// Creates an AuditLogRequest from the current HTTP context.
    /// </summary>
    public static AuditLogRequest CreateAuditRequest(
        this HttpContext context,
        string action,
        string targetType,
        string targetId,
        string? reasonCode = null,
        string? reasonText = null,
        object? previousState = null,
        object? newState = null)
    {
        var user = context.User;
        var actorUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? "unknown";
        var actorEmail = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email")
            ?? "unknown";

        return new AuditLogRequest(
            ActorUserId: actorUserId,
            ActorEmail: actorEmail,
            Action: action,
            TargetType: targetType,
            TargetId: targetId,
            ReasonCode: reasonCode,
            ReasonText: reasonText,
            PreviousState: previousState,
            NewState: newState,
            CorrelationId: Activity.Current?.Id ?? context.TraceIdentifier,
            IpAddress: context.Connection.RemoteIpAddress?.ToString(),
            UserAgent: context.Request.Headers.UserAgent.ToString());
    }

    /// <summary>
    /// Gets the admin user ID from the HTTP context.
    /// </summary>
    public static string? GetAdminUserId(this HttpContext context)
    {
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
    }
}
