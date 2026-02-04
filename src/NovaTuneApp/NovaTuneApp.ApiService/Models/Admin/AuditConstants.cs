namespace NovaTuneApp.ApiService.Models.Admin;

/// <summary>
/// Standard audit action types.
/// </summary>
public static class AuditActions
{
    // User management
    public const string UserStatusChanged = "user.status_changed";
    public const string UserRoleChanged = "user.role_changed";
    public const string UserDeleted = "user.deleted";

    // Track moderation
    public const string TrackDeleted = "track.deleted";
    public const string TrackModerated = "track.moderated";
    public const string TrackDisabled = "track.disabled";
    public const string TrackRestored = "track.restored";

    // Audit log access
    public const string AuditLogViewed = "audit.viewed";
    public const string AuditLogExported = "audit.exported";
}

/// <summary>
/// Target resource types for audit entries.
/// </summary>
public static class AuditTargetTypes
{
    public const string User = "User";
    public const string Track = "Track";
    public const string AuditLog = "AuditLog";
}

/// <summary>
/// Reason codes for moderation actions (Req 11.2).
/// </summary>
public static class ModerationReasonCodes
{
    public const string CopyrightViolation = "copyright_violation";
    public const string CommunityGuidelines = "community_guidelines";
    public const string IllegalContent = "illegal_content";
    public const string Spam = "spam";
    public const string UserRequest = "user_request";
    public const string InactiveAccount = "inactive_account";
    public const string SecurityConcern = "security_concern";
    public const string Other = "other";

    public static readonly HashSet<string> Valid = new(StringComparer.OrdinalIgnoreCase)
    {
        CopyrightViolation, CommunityGuidelines, IllegalContent,
        Spam, UserRequest, InactiveAccount, SecurityConcern, Other
    };

    public static bool IsValid(string? reasonCode) =>
        !string.IsNullOrEmpty(reasonCode) && Valid.Contains(reasonCode);
}
