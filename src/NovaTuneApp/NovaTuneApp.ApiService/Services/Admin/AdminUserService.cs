using System.Text;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;
using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services.Admin;

/// <summary>
/// Admin user management service implementation (Req 11.1).
/// </summary>
public class AdminUserService : IAdminUserService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IAuditLogService _auditLogService;
    private readonly IOptions<AdminOptions> _options;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        IAsyncDocumentSession session,
        IAuditLogService auditLogService,
        IOptions<AdminOptions> options,
        ILogger<AdminUserService> logger)
    {
        _session = session;
        _auditLogService = auditLogService;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AdminUserListItem>> ListUsersAsync(
        AdminUserListQuery query,
        CancellationToken ct = default)
    {
        var limit = Math.Min(query.Limit, _options.Value.MaxUserPageSize);
        if (limit <= 0) limit = _options.Value.DefaultUserPageSize;

        var dbQuery = _session.Query<ApplicationUser, Users_ForAdminSearch>();

        // Apply search filter using full-text search on email and display name
        // Chained Search calls default to OR behavior
        if (!string.IsNullOrEmpty(query.Search))
        {
            var searchTerm = $"*{query.Search}*";
            dbQuery = dbQuery
                .Search(u => u.Email, searchTerm)
                .Search(u => u.DisplayName, searchTerm);
        }

        // Apply status filter
        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(u => u.Status == query.Status.Value);
        }

        // Apply cursor-based pagination
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            var cursorData = DecodeCursor(query.Cursor);
            if (cursorData.HasValue)
            {
                // Cursor format: timestamp|userId for stable ordering
                dbQuery = query.SortBy.ToLowerInvariant() switch
                {
                    "createdat" => query.SortOrder.ToLowerInvariant() == "desc"
                        ? dbQuery.Where(u => u.CreatedAt < cursorData.Value.Timestamp ||
                            (u.CreatedAt == cursorData.Value.Timestamp && string.Compare(u.UserId, cursorData.Value.UserId) < 0))
                        : dbQuery.Where(u => u.CreatedAt > cursorData.Value.Timestamp ||
                            (u.CreatedAt == cursorData.Value.Timestamp && string.Compare(u.UserId, cursorData.Value.UserId) > 0)),
                    _ => dbQuery
                };
            }
        }

        // Apply sorting
        dbQuery = query.SortBy.ToLowerInvariant() switch
        {
            "email" => query.SortOrder.ToLowerInvariant() == "desc"
                ? dbQuery.OrderByDescending(u => u.Email)
                : dbQuery.OrderBy(u => u.Email),
            "lastloginat" => query.SortOrder.ToLowerInvariant() == "desc"
                ? dbQuery.OrderByDescending(u => u.LastLoginAt)
                : dbQuery.OrderBy(u => u.LastLoginAt),
            "trackcount" => query.SortOrder.ToLowerInvariant() == "desc"
                ? dbQuery.OrderByDescending(u => u.TrackCount)
                : dbQuery.OrderBy(u => u.TrackCount),
            _ => query.SortOrder.ToLowerInvariant() == "desc"
                ? dbQuery.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.UserId)
                : dbQuery.OrderBy(u => u.CreatedAt).ThenBy(u => u.UserId)
        };

        var users = await dbQuery
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = users.Count > limit;
        if (hasMore)
            users = users.Take(limit).ToList();

        var items = users.Select(u => new AdminUserListItem(
            u.UserId,
            u.Email,
            u.DisplayName,
            u.Status,
            u.Roles,
            u.TrackCount,
            u.UsedStorageBytes,
            u.CreatedAt,
            u.LastLoginAt)).ToList();

        var nextCursor = hasMore && users.Count > 0
            ? EncodeCursor(users.Last().CreatedAt, users.Last().UserId)
            : null;

        // TotalCount is -1 for cursor pagination (no COUNT query performed)
        return new PagedResult<AdminUserListItem>(items, nextCursor, -1, hasMore);
    }

    /// <inheritdoc />
    public async Task<AdminUserDetails?> GetUserAsync(
        string userId,
        CancellationToken ct = default)
    {
        var user = await _session
            .Query<ApplicationUser>()
            .Where(u => u.UserId == userId)
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return null;

        // Count active sessions (refresh tokens)
        var activeSessionCount = await _session
            .Query<RefreshToken>()
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .CountAsync(ct);

        return new AdminUserDetails(
            user.UserId,
            user.Email,
            user.DisplayName,
            user.Status,
            user.Roles,
            user.TrackCount,
            user.UsedStorageBytes,
            _options.Value.DefaultStorageQuotaBytes,
            user.CreatedAt,
            user.LastLoginAt,
            activeSessionCount);
    }

    /// <inheritdoc />
    public async Task<AdminUserDetails> UpdateUserStatusAsync(
        string userId,
        UpdateUserStatusRequest request,
        string adminUserId,
        CancellationToken ct = default)
    {
        // Prevent self-modification
        if (userId == adminUserId)
        {
            throw new SelfModificationDeniedException();
        }

        // Validate reason code
        if (!ModerationReasonCodes.IsValid(request.ReasonCode))
        {
            throw new InvalidReasonCodeException(request.ReasonCode);
        }

        var user = await _session
            .Query<ApplicationUser>()
            .Where(u => u.UserId == userId)
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            throw new UserNotFoundException(userId);
        }

        var previousStatus = user.Status;
        user.Status = request.Status;

        // Get admin info for audit
        var admin = await _session
            .Query<ApplicationUser>()
            .Where(u => u.UserId == adminUserId)
            .FirstOrDefaultAsync(ct);

        // Create audit log entry
        await _auditLogService.LogAsync(new AuditLogRequest(
            ActorUserId: adminUserId,
            ActorEmail: admin?.Email ?? "unknown",
            Action: AuditActions.UserStatusChanged,
            TargetType: AuditTargetTypes.User,
            TargetId: userId,
            ReasonCode: request.ReasonCode,
            ReasonText: request.ReasonText,
            PreviousState: new { Status = previousStatus.ToString() },
            NewState: new { Status = request.Status.ToString() },
            CorrelationId: null,
            IpAddress: null,
            UserAgent: null), ct);

        await _session.SaveChangesAsync(ct);

        _logger.LogWarning(
            "User status changed: {UserId} from {OldStatus} to {NewStatus} by admin {AdminUserId}, reason: {ReasonCode}",
            userId, previousStatus, request.Status, adminUserId, request.ReasonCode);

        // If user is disabled, revoke their sessions
        if (request.Status == UserStatus.Disabled)
        {
            var revokedCount = await RevokeUserSessionsAsync(userId, ct);
            _logger.LogWarning(
                "Revoked {SessionCount} sessions for disabled user {UserId}",
                revokedCount, userId);
        }

        // Return updated details
        return (await GetUserAsync(userId, ct))!;
    }

    /// <inheritdoc />
    public async Task<int> RevokeUserSessionsAsync(
        string userId,
        CancellationToken ct = default)
    {
        var tokens = await _session
            .Query<RefreshToken>()
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await _session.SaveChangesAsync(ct);

        return tokens.Count;
    }

    private static string EncodeCursor(DateTime timestamp, string userId)
    {
        var data = $"{timestamp:O}|{userId}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
    }

    private static (DateTime Timestamp, string UserId)? DecodeCursor(string cursor)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = decoded.Split('|', 2);
            if (parts.Length == 2)
            {
                return (DateTime.Parse(parts[0]), parts[1]);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
