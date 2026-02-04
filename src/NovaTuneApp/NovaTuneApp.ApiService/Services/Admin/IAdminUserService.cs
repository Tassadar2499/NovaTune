using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;

namespace NovaTuneApp.ApiService.Services.Admin;

/// <summary>
/// Service for admin user management operations (Req 11.1).
/// </summary>
public interface IAdminUserService
{
    /// <summary>
    /// Lists users with search, filtering, and pagination.
    /// </summary>
    Task<PagedResult<AdminUserListItem>> ListUsersAsync(
        AdminUserListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Gets detailed user information for admin view.
    /// </summary>
    Task<AdminUserDetails?> GetUserAsync(
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates a user's status with reason tracking.
    /// </summary>
    /// <exception cref="SelfModificationDeniedException">When admin tries to modify own status.</exception>
    /// <exception cref="UserNotFoundException">When target user doesn't exist.</exception>
    Task<AdminUserDetails> UpdateUserStatusAsync(
        string userId,
        UpdateUserStatusRequest request,
        string adminUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes all active sessions for a user.
    /// </summary>
    Task<int> RevokeUserSessionsAsync(
        string userId,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown when an admin attempts to modify their own status.
/// </summary>
public class SelfModificationDeniedException : Exception
{
    public SelfModificationDeniedException()
        : base("Administrators cannot modify their own status.") { }
}

/// <summary>
/// Thrown when a user is not found.
/// </summary>
public class UserNotFoundException : Exception
{
    public string UserId { get; }

    public UserNotFoundException(string userId)
        : base($"User '{userId}' not found.")
    {
        UserId = userId;
    }
}

/// <summary>
/// Thrown when a reason code is invalid.
/// </summary>
public class InvalidReasonCodeException : Exception
{
    public string ReasonCode { get; }

    public InvalidReasonCodeException(string reasonCode)
        : base($"Invalid reason code: '{reasonCode}'")
    {
        ReasonCode = reasonCode;
    }
}
