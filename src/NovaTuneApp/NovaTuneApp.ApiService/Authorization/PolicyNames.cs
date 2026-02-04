namespace NovaTuneApp.ApiService.Authorization;

/// <summary>
/// Authorization policy name constants.
/// </summary>
public static class PolicyNames
{
    /// <summary>
    /// Requires an authenticated user with the Listener role.
    /// </summary>
    public const string Listener = "Listener";

    /// <summary>
    /// Requires the admin role claim.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Requires the user to have Active status.
    /// </summary>
    public const string ActiveUser = "ActiveUser";

    /// <summary>
    /// Requires the user to be able to stream (Active or PendingDeletion).
    /// </summary>
    public const string CanStream = "CanStream";

    /// <summary>
    /// Requires the admin role and audit.read permission.
    /// </summary>
    public const string AdminWithAuditAccess = "AdminWithAuditAccess";
}
