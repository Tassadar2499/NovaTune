# Stage 8 — Admin / Moderation + Audit Logs

**Goal:** Allow administrative operations with full auditability, including user management, track moderation, analytics dashboards, and tamper-evident audit logging.

## Prerequisites

- Stage 1 completed: Authentication with Admin role support
- Stage 5 completed: Track management with soft-delete
- Stage 7 completed: Analytics aggregates available for dashboards

---

## Overview

```
┌─────────────┐                              ┌─────────────────┐
│    Admin    │                              │    RavenDB      │
│   Client    │                              │                 │
└──────┬──────┘                              │ ┌─────────────┐ │
       │                                     │ │    Users    │ │
       │  GET /admin/users                   │ └─────────────┘ │
       │ ──────────────────────────────────► │ ┌─────────────┐ │
       │ ◄─────────────────────────────────  │ │   Tracks    │ │
       │  User list + search                 │ └─────────────┘ │
       │                                     │ ┌─────────────┐ │
       │  PATCH /admin/users/{userId}        │ │  Analytics  │ │
       │ ──────────────────────────────────► │ │ Aggregates  │ │
       │ ◄─────────────────────────────────  │ └─────────────┘ │
       │  Updated user + audit log           │ ┌─────────────┐ │
       │                                     │ │ Audit Logs  │ │
       │  GET /admin/tracks                  │ └─────────────┘ │
       │ ──────────────────────────────────► └────────┬────────┘
       │ ◄─────────────────────────────────           │
       │  Track list (all users)            ┌────────┴────────┐
       │                                    │   API Service   │
       │  DELETE /admin/tracks/{trackId}    │                 │
       │ ──────────────────────────────────►│ • Auth (Admin)  │
       │ ◄─────────────────────────────────│ • Validation    │
       │  204 + audit log                   │ • Audit logging │
       │                                    │ • Rate limiting │
       │  POST /admin/tracks/{id}/moderate  └─────────────────┘
       │ ──────────────────────────────────►
       │ ◄─────────────────────────────────
       │  Moderated track + audit log
       │
       │  GET /admin/analytics/overview
       │ ──────────────────────────────────►
       │ ◄─────────────────────────────────
       │  Dashboard metrics (from Stage 7 aggregates)
       │
       │  GET /admin/audit-logs
       │ ──────────────────────────────────►
       │ ◄─────────────────────────────────
       │  Paginated audit entries
```

---

## 1. Data Models

### Audit Log Entry (RavenDB Document)

```csharp
namespace NovaTuneApp.ApiService.Models.Admin;

/// <summary>
/// Audit log entry for admin actions (NF-3.5).
/// </summary>
public sealed class AuditLogEntry
{
    /// <summary>
    /// RavenDB document ID: "AuditLogs/{ulid}".
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Unique audit entry ID (ULID).
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string AuditId { get; init; } = string.Empty;

    /// <summary>
    /// Admin user who performed the action.
    /// </summary>
    [Required]
    public string ActorUserId { get; init; } = string.Empty;

    /// <summary>
    /// Actor's email at time of action (denormalized for audit).
    /// </summary>
    [Required]
    public string ActorEmail { get; init; } = string.Empty;

    /// <summary>
    /// Action performed (from AuditActions).
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Resource type affected.
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string TargetType { get; init; } = string.Empty;

    /// <summary>
    /// Target resource ID.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Reason code for the action (required for moderation).
    /// </summary>
    [MaxLength(64)]
    public string? ReasonCode { get; init; }

    /// <summary>
    /// Free-text reason/notes from admin.
    /// </summary>
    [MaxLength(1000)]
    public string? ReasonText { get; init; }

    /// <summary>
    /// Previous state (JSON serialized, for reversibility tracking).
    /// </summary>
    public string? PreviousState { get; init; }

    /// <summary>
    /// New state (JSON serialized).
    /// </summary>
    public string? NewState { get; init; }

    /// <summary>
    /// Server timestamp when action occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Request correlation ID for tracing.
    /// </summary>
    [MaxLength(128)]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// IP address of the admin (for security audit).
    /// </summary>
    [MaxLength(45)]
    public string? IpAddress { get; init; }

    /// <summary>
    /// User agent of the admin client.
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; init; }

    /// <summary>
    /// Hash of previous audit entry (tamper-evidence chain).
    /// </summary>
    [MaxLength(64)]
    public string? PreviousEntryHash { get; init; }

    /// <summary>
    /// Hash of this entry's content (for verification).
    /// </summary>
    [MaxLength(64)]
    public string? ContentHash { get; init; }

    /// <summary>
    /// Expiration for document retention (1 year per NF-3.5).
    /// </summary>
    [JsonPropertyName("@expires")]
    public DateTimeOffset? Expires { get; init; }
}

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
}
```

### Moderation Status for Tracks

```csharp
/// <summary>
/// Moderation status for tracks (Req 11.2 clarifications).
/// </summary>
public enum ModerationStatus
{
    /// <summary>
    /// Track is visible and streamable.
    /// </summary>
    None = 0,

    /// <summary>
    /// Track flagged for review, still accessible.
    /// </summary>
    UnderReview = 1,

    /// <summary>
    /// Track disabled by admin, not streamable but retained.
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// Track removed by admin (triggers deletion flow).
    /// </summary>
    Removed = 3
}
```

### Admin DTOs

```csharp
namespace NovaTuneApp.ApiService.Models.Admin;

// User Management DTOs

public record AdminUserListItem(
    string UserId,
    string Email,
    string? DisplayName,
    UserStatus Status,
    IReadOnlyList<string> Roles,
    int TrackCount,
    long UsedStorageBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public record AdminUserDetails(
    string UserId,
    string Email,
    string? DisplayName,
    UserStatus Status,
    IReadOnlyList<string> Roles,
    int TrackCount,
    long UsedStorageBytes,
    long StorageQuotaBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    int ActiveSessionCount);

public record UpdateUserStatusRequest(
    [Required] UserStatus Status,
    [Required] string ReasonCode,
    [MaxLength(1000)] string? ReasonText);

// Track Moderation DTOs

public record AdminTrackListItem(
    string TrackId,
    string UserId,
    string UserEmail,
    string Title,
    string? Artist,
    TrackStatus Status,
    ModerationStatus ModerationStatus,
    long FileSizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModeratedAt);

public record AdminTrackDetails(
    string TrackId,
    string UserId,
    string UserEmail,
    string Title,
    string? Artist,
    TimeSpan Duration,
    TrackStatus Status,
    ModerationStatus ModerationStatus,
    string? ModerationReason,
    long FileSizeBytes,
    string MimeType,
    AudioMetadata? Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModeratedAt,
    string? ModeratedByUserId,
    int TotalPlays);

public record ModerateTrackRequest(
    [Required] ModerationStatus ModerationStatus,
    [Required] string ReasonCode,
    [MaxLength(1000)] string? ReasonText);

public record AdminDeleteTrackRequest(
    [Required] string ReasonCode,
    [MaxLength(1000)] string? ReasonText);

// Analytics Dashboard DTOs

public record AnalyticsOverview(
    int TotalUsers,
    int ActiveUsersLast24h,
    int TotalTracks,
    int TracksUploadedLast24h,
    long TotalPlaysLast24h,
    TimeSpan TotalListenTimeLast24h,
    long TotalStorageUsedBytes);

public record TopTrackItem(
    string TrackId,
    string Title,
    string? Artist,
    string UserId,
    string UserEmail,
    int PlayCount,
    TimeSpan TotalListenTime);

public record UserActivityItem(
    string UserId,
    string Email,
    string? DisplayName,
    int TracksUploaded,
    int TotalPlays,
    TimeSpan TotalListenTime,
    DateTimeOffset LastActivityAt);

// Audit Log DTOs

public record AuditLogListItem(
    string AuditId,
    string ActorUserId,
    string ActorEmail,
    string Action,
    string TargetType,
    string TargetId,
    string? ReasonCode,
    DateTimeOffset Timestamp);

public record AuditLogDetails(
    string AuditId,
    string ActorUserId,
    string ActorEmail,
    string Action,
    string TargetType,
    string TargetId,
    string? ReasonCode,
    string? ReasonText,
    string? PreviousState,
    string? NewState,
    DateTimeOffset Timestamp,
    string? CorrelationId,
    string? IpAddress);
```

---

## 2. API Endpoints

### 2.1 User Management

#### `GET /admin/users` — List/Search Users (Req 11.1)

**Request:**
- **Method:** `GET`
- **Path:** `/admin/users`
- **Authentication:** Required (Bearer token)
- **Authorization:** Admin role only

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `search` | string | — | Search by email or display name |
| `status` | string | — | Filter by status: `Active`, `Disabled`, `PendingDeletion` |
| `sortBy` | string | `createdAt` | Sort field: `createdAt`, `lastLoginAt`, `email`, `trackCount` |
| `sortOrder` | string | `desc` | Sort direction: `asc`, `desc` |
| `cursor` | string | — | Cursor for pagination |
| `limit` | int | 50 | Page size (1-100) |

**Response Schema (200 OK):**

```json
{
  "items": [
    {
      "userId": "01HXK...",
      "email": "user@example.com",
      "displayName": "User Name",
      "status": "Active",
      "roles": ["Listener"],
      "trackCount": 42,
      "usedStorageBytes": 1073741824,
      "createdAt": "2025-01-01T00:00:00Z",
      "lastLoginAt": "2025-01-08T10:00:00Z"
    }
  ],
  "nextCursor": "eyJza...",
  "totalCount": 1500,
  "hasMore": true
}
```

#### `GET /admin/users/{userId}` — Get User Details

**Response Schema (200 OK):**

```json
{
  "userId": "01HXK...",
  "email": "user@example.com",
  "displayName": "User Name",
  "status": "Active",
  "roles": ["Listener"],
  "trackCount": 42,
  "usedStorageBytes": 1073741824,
  "storageQuotaBytes": 10737418240,
  "createdAt": "2025-01-01T00:00:00Z",
  "lastLoginAt": "2025-01-08T10:00:00Z",
  "activeSessionCount": 2
}
```

#### `PATCH /admin/users/{userId}` — Update User Status (Req 11.1)

**Request Schema:**

```json
{
  "status": "Disabled",
  "reasonCode": "community_guidelines",
  "reasonText": "Repeated violation of upload guidelines."
}
```

**Response Schema (200 OK):**

Returns updated `AdminUserDetails`.

**Business Rules:**
- Status change from `Active` to `Disabled` revokes all active sessions immediately
- Status change to `PendingDeletion` triggers 30-day deletion countdown
- All status changes create an audit log entry
- Cannot change own status (prevent self-lockout)

**Error Responses:**

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-status-transition` | Invalid status transition |
| `400` | `invalid-reason-code` | Unknown reason code |
| `403` | `self-modification-denied` | Cannot modify own status |
| `404` | `user-not-found` | User does not exist |

---

### 2.2 Track Moderation

#### `GET /admin/tracks` — List/Search All Tracks (Req 11.2)

**Request:**
- **Method:** `GET`
- **Path:** `/admin/tracks`
- **Authentication:** Required (Bearer token)
- **Authorization:** Admin role only

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `search` | string | — | Search by title, artist, or owner email |
| `status` | string | — | Filter by TrackStatus |
| `moderationStatus` | string | — | Filter by ModerationStatus |
| `userId` | string | — | Filter by owner user ID |
| `sortBy` | string | `createdAt` | Sort field |
| `sortOrder` | string | `desc` | Sort direction |
| `cursor` | string | — | Cursor for pagination |
| `limit` | int | 50 | Page size (1-100) |

**Response Schema (200 OK):**

```json
{
  "items": [
    {
      "trackId": "01HXK...",
      "userId": "01HXJ...",
      "userEmail": "user@example.com",
      "title": "My Track",
      "artist": "Artist Name",
      "status": "Ready",
      "moderationStatus": "None",
      "fileSizeBytes": 15728640,
      "createdAt": "2025-01-08T10:00:00Z",
      "moderatedAt": null
    }
  ],
  "nextCursor": "eyJza...",
  "totalCount": 5000,
  "hasMore": true
}
```

#### `GET /admin/tracks/{trackId}` — Get Track Details (Admin View)

**Response Schema (200 OK):**

```json
{
  "trackId": "01HXK...",
  "userId": "01HXJ...",
  "userEmail": "user@example.com",
  "title": "My Track",
  "artist": "Artist Name",
  "duration": "PT3M42S",
  "status": "Ready",
  "moderationStatus": "None",
  "moderationReason": null,
  "fileSizeBytes": 15728640,
  "mimeType": "audio/mpeg",
  "metadata": {
    "bitrate": 320000,
    "sampleRate": 44100,
    "channels": 2,
    "codec": "mp3"
  },
  "createdAt": "2025-01-08T10:00:00Z",
  "moderatedAt": null,
  "moderatedByUserId": null,
  "totalPlays": 150
}
```

#### `POST /admin/tracks/{trackId}/moderate` — Moderate Track (Req 11.2)

**Request Schema:**

```json
{
  "moderationStatus": "Disabled",
  "reasonCode": "copyright_violation",
  "reasonText": "DMCA takedown request received."
}
```

**Moderation Semantics (from Req 11.2 clarifications):**

| ModerationStatus | Effect |
|------------------|--------|
| `UnderReview` | Track flagged; remains streamable while review pending |
| `Disabled` | Track not streamable but data retained for appeal |
| `Removed` | Triggers soft-delete; 30-day grace then physical deletion |

**Response Schema (200 OK):**

Returns updated `AdminTrackDetails`.

**Business Rules:**
- Creates audit log entry with reason code
- `Disabled` immediately invalidates cached streaming URLs
- `Removed` publishes `TrackDeletedEvent` (reuses Stage 5 deletion flow)
- Track is removed from all playlists when `Removed`
- User notified via email (future: notification service)

#### `DELETE /admin/tracks/{trackId}` — Admin Delete Track (Req 11.2)

**Request Schema:**

```json
{
  "reasonCode": "illegal_content",
  "reasonText": "Content violates local laws."
}
```

**Response (204 No Content)**

**Business Rules:**
- Equivalent to `moderate` with `Removed` status
- Bypasses user ownership check
- Audit log records admin actor and reason
- Triggers same deletion flow as user-initiated delete

---

### 2.3 Analytics Dashboard (Req 11.3)

#### `GET /admin/analytics/overview` — Dashboard Overview

**Response Schema (200 OK):**

```json
{
  "totalUsers": 1500,
  "activeUsersLast24h": 342,
  "totalTracks": 5000,
  "tracksUploadedLast24h": 47,
  "totalPlaysLast24h": 12500,
  "totalListenTimeLast24h": "PT450H30M",
  "totalStorageUsedBytes": 53687091200
}
```

#### `GET /admin/analytics/tracks/top` — Top Tracks

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `count` | int | 10 | Number of tracks (1-100) |
| `period` | string | `7d` | Time period: `24h`, `7d`, `30d`, `all` |

**Response Schema (200 OK):**

```json
{
  "period": "7d",
  "items": [
    {
      "trackId": "01HXK...",
      "title": "Popular Track",
      "artist": "Famous Artist",
      "userId": "01HXJ...",
      "userEmail": "artist@example.com",
      "playCount": 1250,
      "totalListenTime": "PT52H30M"
    }
  ]
}
```

#### `GET /admin/analytics/users/active` — Active Users

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `count` | int | 10 | Number of users (1-100) |
| `period` | string | `7d` | Time period: `24h`, `7d`, `30d` |

**Response Schema (200 OK):**

```json
{
  "period": "7d",
  "items": [
    {
      "userId": "01HXJ...",
      "email": "user@example.com",
      "displayName": "Active User",
      "tracksUploaded": 15,
      "totalPlays": 450,
      "totalListenTime": "PT18H45M",
      "lastActivityAt": "2025-01-08T10:00:00Z"
    }
  ]
}
```

---

### 2.4 Audit Log Access (NF-3.5)

#### `GET /admin/audit-logs` — View Audit Logs

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `actorUserId` | string | — | Filter by admin who performed action |
| `action` | string | — | Filter by action type |
| `targetType` | string | — | Filter by target resource type |
| `targetId` | string | — | Filter by specific target |
| `startDate` | date | — | Start of date range (inclusive) |
| `endDate` | date | — | End of date range (inclusive) |
| `cursor` | string | — | Cursor for pagination |
| `limit` | int | 50 | Page size (1-100) |

**Response Schema (200 OK):**

```json
{
  "items": [
    {
      "auditId": "01HXK...",
      "actorUserId": "01HXA...",
      "actorEmail": "admin@example.com",
      "action": "user.status_changed",
      "targetType": "User",
      "targetId": "01HXJ...",
      "reasonCode": "community_guidelines",
      "timestamp": "2025-01-08T10:00:00Z"
    }
  ],
  "nextCursor": "eyJza...",
  "totalCount": 500,
  "hasMore": true
}
```

**Access Control:**
- Admin role required
- Viewing audit logs itself creates an audit entry (`audit.viewed`)
- Export of large date ranges requires additional authorization (future)

#### `GET /admin/audit-logs/{auditId}` — Get Audit Entry Details

**Response Schema (200 OK):**

```json
{
  "auditId": "01HXK...",
  "actorUserId": "01HXA...",
  "actorEmail": "admin@example.com",
  "action": "user.status_changed",
  "targetType": "User",
  "targetId": "01HXJ...",
  "reasonCode": "community_guidelines",
  "reasonText": "Repeated violation of upload guidelines.",
  "previousState": "{\"status\":\"Active\"}",
  "newState": "{\"status\":\"Disabled\"}",
  "timestamp": "2025-01-08T10:00:00Z",
  "correlationId": "00-abc123...",
  "ipAddress": "192.168.1.100"
}
```

---

## 3. Service Interfaces

### `IAdminUserService`

```csharp
namespace NovaTuneApp.ApiService.Services.Admin;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserListItem>> ListUsersAsync(
        AdminUserListQuery query,
        CancellationToken ct = default);

    Task<AdminUserDetails> GetUserAsync(
        string userId,
        CancellationToken ct = default);

    Task<AdminUserDetails> UpdateUserStatusAsync(
        string userId,
        UpdateUserStatusRequest request,
        string adminUserId,
        CancellationToken ct = default);

    Task RevokeUserSessionsAsync(
        string userId,
        CancellationToken ct = default);
}

public record AdminUserListQuery(
    string? Search = null,
    UserStatus? Status = null,
    string SortBy = "createdAt",
    string SortOrder = "desc",
    string? Cursor = null,
    int Limit = 50);
```

### `IAdminTrackService`

```csharp
namespace NovaTuneApp.ApiService.Services.Admin;

public interface IAdminTrackService
{
    Task<PagedResult<AdminTrackListItem>> ListTracksAsync(
        AdminTrackListQuery query,
        CancellationToken ct = default);

    Task<AdminTrackDetails> GetTrackAsync(
        string trackId,
        CancellationToken ct = default);

    Task<AdminTrackDetails> ModerateTrackAsync(
        string trackId,
        ModerateTrackRequest request,
        string adminUserId,
        CancellationToken ct = default);

    Task DeleteTrackAsync(
        string trackId,
        AdminDeleteTrackRequest request,
        string adminUserId,
        CancellationToken ct = default);
}

public record AdminTrackListQuery(
    string? Search = null,
    TrackStatus? Status = null,
    ModerationStatus? ModerationStatus = null,
    string? UserId = null,
    string SortBy = "createdAt",
    string SortOrder = "desc",
    string? Cursor = null,
    int Limit = 50);
```

### `IAdminAnalyticsService`

```csharp
namespace NovaTuneApp.ApiService.Services.Admin;

public interface IAdminAnalyticsService
{
    Task<AnalyticsOverview> GetOverviewAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<TopTrackItem>> GetTopTracksAsync(
        int count,
        AnalyticsPeriod period,
        CancellationToken ct = default);

    Task<IReadOnlyList<UserActivityItem>> GetActiveUsersAsync(
        int count,
        AnalyticsPeriod period,
        CancellationToken ct = default);
}

public enum AnalyticsPeriod
{
    Last24Hours,
    Last7Days,
    Last30Days,
    AllTime
}
```

### `IAuditLogService`

```csharp
namespace NovaTuneApp.ApiService.Services.Admin;

public interface IAuditLogService
{
    /// <summary>
    /// Records an audit log entry.
    /// </summary>
    Task<AuditLogEntry> LogAsync(
        AuditLogRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Lists audit log entries with filtering.
    /// </summary>
    Task<PagedResult<AuditLogListItem>> ListAsync(
        AuditLogQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a specific audit log entry.
    /// </summary>
    Task<AuditLogDetails> GetAsync(
        string auditId,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies integrity of audit log chain.
    /// </summary>
    Task<AuditIntegrityResult> VerifyIntegrityAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}

public record AuditLogRequest(
    string ActorUserId,
    string ActorEmail,
    string Action,
    string TargetType,
    string TargetId,
    string? ReasonCode,
    string? ReasonText,
    object? PreviousState,
    object? NewState,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent);

public record AuditLogQuery(
    string? ActorUserId = null,
    string? Action = null,
    string? TargetType = null,
    string? TargetId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? Cursor = null,
    int Limit = 50);

public record AuditIntegrityResult(
    bool IsValid,
    int EntriesChecked,
    int InvalidEntries,
    IReadOnlyList<string> InvalidAuditIds);
```

---

## 4. Tamper-Evidence Mechanism (NF-3.5)

### Hash Chain Implementation

Each audit entry includes a hash of the previous entry, creating a verifiable chain:

```csharp
public class AuditLogService : IAuditLogService
{
    private readonly IAsyncDocumentSession _session;
    private readonly ILogger<AuditLogService> _logger;

    public async Task<AuditLogEntry> LogAsync(
        AuditLogRequest request,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var auditId = Ulid.NewUlid().ToString();

        // Get hash of previous entry for chain
        var previousEntry = await _session
            .Query<AuditLogEntry>()
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync(ct);

        var previousHash = previousEntry?.ContentHash;

        var entry = new AuditLogEntry
        {
            Id = $"AuditLogs/{auditId}",
            AuditId = auditId,
            ActorUserId = request.ActorUserId,
            ActorEmail = request.ActorEmail,
            Action = request.Action,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            ReasonCode = request.ReasonCode,
            ReasonText = request.ReasonText,
            PreviousState = request.PreviousState is not null
                ? JsonSerializer.Serialize(request.PreviousState)
                : null,
            NewState = request.NewState is not null
                ? JsonSerializer.Serialize(request.NewState)
                : null,
            Timestamp = now,
            CorrelationId = request.CorrelationId,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            PreviousEntryHash = previousHash,
            Expires = now.AddYears(1) // 1 year retention per NF-3.5
        };

        // Compute content hash
        entry = entry with { ContentHash = ComputeHash(entry) };

        await _session.StoreAsync(entry, ct);
        await _session.SaveChangesAsync(ct);

        return entry;
    }

    public async Task<AuditIntegrityResult> VerifyIntegrityAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        var entries = await _session
            .Query<AuditLogEntry>()
            .Where(e => e.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue)
                     && e.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
            .OrderBy(e => e.Timestamp)
            .ToListAsync(ct);

        var invalidIds = new List<string>();
        string? expectedPreviousHash = null;

        foreach (var entry in entries)
        {
            // Verify previous hash chain
            if (entry.PreviousEntryHash != expectedPreviousHash)
            {
                invalidIds.Add(entry.AuditId);
            }

            // Verify content hash
            var computedHash = ComputeHash(entry);
            if (entry.ContentHash != computedHash)
            {
                if (!invalidIds.Contains(entry.AuditId))
                    invalidIds.Add(entry.AuditId);
            }

            expectedPreviousHash = entry.ContentHash;
        }

        return new AuditIntegrityResult(
            invalidIds.Count == 0,
            entries.Count,
            invalidIds.Count,
            invalidIds);
    }

    private static string ComputeHash(AuditLogEntry entry)
    {
        var content = $"{entry.AuditId}|{entry.ActorUserId}|{entry.Action}|" +
                      $"{entry.TargetType}|{entry.TargetId}|{entry.Timestamp:O}|" +
                      $"{entry.PreviousState}|{entry.NewState}|{entry.PreviousEntryHash}";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
```

### Future Enhancements (TBD items resolved)

| Enhancement | Description | Priority |
|-------------|-------------|----------|
| Write-once storage | Use RavenDB `@read-only` metadata after creation | High |
| External witness | Publish hash to external immutable store (blockchain, S3 object lock) | Medium |
| Periodic integrity checks | Background job to verify chain integrity | High |
| Alert on tampering | Notify security team if integrity check fails | High |

---

## 5. RavenDB Indexes

### `AuditLogs_ByFilters`

```csharp
public class AuditLogs_ByFilters : AbstractIndexCreationTask<AuditLogEntry>
{
    public AuditLogs_ByFilters()
    {
        Map = entries => from entry in entries
                         select new
                         {
                             entry.ActorUserId,
                             entry.Action,
                             entry.TargetType,
                             entry.TargetId,
                             entry.Timestamp,
                             entry.ReasonCode
                         };
    }
}
```

### `Users_ForAdminSearch`

```csharp
public class Users_ForAdminSearch : AbstractIndexCreationTask<ApplicationUser>
{
    public Users_ForAdminSearch()
    {
        Map = users => from user in users
                       select new
                       {
                           user.UserId,
                           user.Email,
                           user.NormalizedEmail,
                           user.DisplayName,
                           user.Status,
                           user.Roles,
                           user.CreatedAt,
                           user.LastLoginAt,
                           SearchText = new[] { user.Email, user.DisplayName }
                       };

        Index("SearchText", FieldIndexing.Search);
        Analyze("SearchText", "StandardAnalyzer");
    }
}
```

### `Tracks_ForAdminSearch`

```csharp
public class Tracks_ForAdminSearch : AbstractIndexCreationTask<Track>
{
    public Tracks_ForAdminSearch()
    {
        Map = tracks => from track in tracks
                        select new
                        {
                            track.TrackId,
                            track.UserId,
                            track.Title,
                            track.Artist,
                            track.Status,
                            track.ModerationStatus,
                            track.CreatedAt,
                            track.ModeratedAt,
                            SearchText = new[] { track.Title, track.Artist }
                        };

        Index("SearchText", FieldIndexing.Search);
        Analyze("SearchText", "StandardAnalyzer");
    }
}
```

---

## 6. Authorization Policies

```csharp
// In Program.cs or AuthorizationConfig.cs

services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.AdminOnly, policy =>
        policy.RequireClaim(ClaimTypes.Role, "Admin"));

    options.AddPolicy(PolicyNames.AdminWithAuditAccess, policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(ClaimTypes.Role, "Admin") &&
            context.User.HasClaim("permissions", "audit.read")));
});

public static class PolicyNames
{
    public const string ActiveUser = "ActiveUser";
    public const string AdminOnly = "AdminOnly";
    public const string AdminWithAuditAccess = "AdminWithAuditAccess";
}
```

---

## 7. Rate Limiting

| Endpoint | Policy | Limit | Window |
|----------|--------|-------|--------|
| `GET /admin/users` | `admin-user-list` | 60/min | Sliding |
| `PATCH /admin/users/{id}` | `admin-user-modify` | 30/min | Sliding |
| `GET /admin/tracks` | `admin-track-list` | 60/min | Sliding |
| `POST /admin/tracks/{id}/moderate` | `admin-track-modify` | 30/min | Sliding |
| `DELETE /admin/tracks/{id}` | `admin-track-modify` | 30/min | Sliding |
| `GET /admin/analytics/*` | `admin-analytics` | 30/min | Sliding |
| `GET /admin/audit-logs` | `admin-audit` | 30/min | Sliding |

---

## 8. Endpoint Implementation

### `AdminEndpoints.cs`

```csharp
namespace NovaTuneApp.ApiService.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin")
            .RequireAuthorization(PolicyNames.AdminOnly)
            .WithTags("Admin");

        // User Management
        var users = group.MapGroup("/users");
        users.MapGet("/", HandleListUsers)
            .WithName("AdminListUsers")
            .RequireRateLimiting("admin-user-list");

        users.MapGet("/{userId}", HandleGetUser)
            .WithName("AdminGetUser");

        users.MapPatch("/{userId}", HandleUpdateUserStatus)
            .WithName("AdminUpdateUserStatus")
            .RequireRateLimiting("admin-user-modify");

        // Track Moderation
        var tracks = group.MapGroup("/tracks");
        tracks.MapGet("/", HandleListTracks)
            .WithName("AdminListTracks")
            .RequireRateLimiting("admin-track-list");

        tracks.MapGet("/{trackId}", HandleGetTrack)
            .WithName("AdminGetTrack");

        tracks.MapPost("/{trackId}/moderate", HandleModerateTrack)
            .WithName("AdminModerateTrack")
            .RequireRateLimiting("admin-track-modify");

        tracks.MapDelete("/{trackId}", HandleDeleteTrack)
            .WithName("AdminDeleteTrack")
            .RequireRateLimiting("admin-track-modify");

        // Analytics Dashboard
        var analytics = group.MapGroup("/analytics");
        analytics.MapGet("/overview", HandleGetOverview)
            .WithName("AdminAnalyticsOverview")
            .RequireRateLimiting("admin-analytics");

        analytics.MapGet("/tracks/top", HandleGetTopTracks)
            .WithName("AdminTopTracks")
            .RequireRateLimiting("admin-analytics");

        analytics.MapGet("/users/active", HandleGetActiveUsers)
            .WithName("AdminActiveUsers")
            .RequireRateLimiting("admin-analytics");

        // Audit Logs
        var audit = group.MapGroup("/audit-logs")
            .RequireAuthorization(PolicyNames.AdminWithAuditAccess);

        audit.MapGet("/", HandleListAuditLogs)
            .WithName("AdminListAuditLogs")
            .RequireRateLimiting("admin-audit");

        audit.MapGet("/{auditId}", HandleGetAuditLog)
            .WithName("AdminGetAuditLog");

        audit.MapGet("/verify", HandleVerifyIntegrity)
            .WithName("AdminVerifyAuditIntegrity");
    }

    // Handler implementations...
}
```

---

## 9. Observability (NF-4.x)

### Logging

| Event | Level | Fields |
|-------|-------|--------|
| Admin user list requested | Info | `AdminUserId`, `Filters` |
| User status changed | Warning | `AdminUserId`, `TargetUserId`, `OldStatus`, `NewStatus`, `ReasonCode` |
| User sessions revoked | Warning | `AdminUserId`, `TargetUserId`, `SessionCount` |
| Admin track list requested | Info | `AdminUserId`, `Filters` |
| Track moderated | Warning | `AdminUserId`, `TrackId`, `ModerationStatus`, `ReasonCode` |
| Track deleted by admin | Warning | `AdminUserId`, `TrackId`, `ReasonCode` |
| Audit log viewed | Info | `AdminUserId`, `Filters` |
| Audit integrity check | Info | `AdminUserId`, `DateRange`, `IsValid`, `InvalidCount` |
| Audit integrity failure | Error | `AdminUserId`, `InvalidAuditIds` |

**Redaction (NF-4.5):** Log user IDs and track IDs but never log reason text content.

### Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `admin_user_status_changes_total` | Counter | `new_status`, `reason_code` |
| `admin_track_moderations_total` | Counter | `moderation_status`, `reason_code` |
| `admin_track_deletions_total` | Counter | `reason_code` |
| `admin_audit_queries_total` | Counter | `has_filters` |
| `admin_audit_integrity_checks_total` | Counter | `result` (valid/invalid) |
| `admin_api_request_duration_ms` | Histogram | `endpoint` |

### Tracing

- All admin operations create spans with admin user context
- Audit log writes are traced separately for debugging
- Session revocation creates a span per session

---

## 10. Resilience (NF-1.4)

### Timeouts

| Operation | Timeout | Retries |
|-----------|---------|---------|
| RavenDB read (user/track) | 2s | 1 |
| RavenDB write (status change) | 5s | 0 |
| RavenDB query (list) | 5s | 1 |
| Audit log write | 5s | 1 |
| Session revocation | 2s per session | 0 |
| Analytics aggregation query | 10s | 0 |

### Circuit Breaker

| Dependency | Failure Threshold | Half-Open After |
|------------|-------------------|-----------------|
| RavenDB | 5 consecutive | 30s |

---

## 11. Security Considerations

### Access Control

- All admin endpoints require Admin role via JWT claim
- Audit log access requires additional `audit.read` permission
- Admin cannot modify their own status (prevent lockout)
- Admin actions on other admins require confirmation (future: 2FA)

### Audit Completeness

- Every state-changing admin operation creates audit entry
- Audit entries cannot be deleted (only expire after 1 year)
- Audit log access itself is audited
- Hash chain provides tamper evidence

### IP and Session Tracking

- Admin IP address recorded in audit logs
- User-Agent recorded for forensics
- Consider IP allowlisting for admin endpoints (future)

### Data Retention

- Audit logs retained for 1 year per NF-3.5
- Right-to-delete requests must preserve audit logs (documented exception)
- Audit logs for deleted users retain user ID but may anonymize PII

---

## 12. Configuration

### `AdminOptions`

```csharp
public class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// Maximum users per page in admin list.
    /// </summary>
    public int MaxUserPageSize { get; set; } = 100;

    /// <summary>
    /// Maximum tracks per page in admin list.
    /// </summary>
    public int MaxTrackPageSize { get; set; } = 100;

    /// <summary>
    /// Maximum audit log entries per page.
    /// </summary>
    public int MaxAuditPageSize { get; set; } = 100;

    /// <summary>
    /// Audit log retention period in days (default: 365).
    /// </summary>
    public int AuditRetentionDays { get; set; } = 365;

    /// <summary>
    /// Enable audit log integrity verification.
    /// </summary>
    public bool EnableIntegrityVerification { get; set; } = true;

    /// <summary>
    /// Days of analytics data to include in overview.
    /// </summary>
    public int AnalyticsOverviewDays { get; set; } = 1;
}
```

### `appsettings.json` Example

```json
{
  "Admin": {
    "MaxUserPageSize": 100,
    "MaxTrackPageSize": 100,
    "MaxAuditPageSize": 100,
    "AuditRetentionDays": 365,
    "EnableIntegrityVerification": true,
    "AnalyticsOverviewDays": 1
  },
  "RateLimiting": {
    "AdminUserList": { "PermitLimit": 60, "WindowMinutes": 1 },
    "AdminUserModify": { "PermitLimit": 30, "WindowMinutes": 1 },
    "AdminTrackList": { "PermitLimit": 60, "WindowMinutes": 1 },
    "AdminTrackModify": { "PermitLimit": 30, "WindowMinutes": 1 },
    "AdminAnalytics": { "PermitLimit": 30, "WindowMinutes": 1 },
    "AdminAudit": { "PermitLimit": 30, "WindowMinutes": 1 }
  }
}
```

---

## 13. Test Strategy

### Unit Tests

- `AdminUserService`: List, search, status change, session revocation
- `AdminTrackService`: List, search, moderation, deletion
- `AdminAnalyticsService`: Overview calculation, top tracks, active users
- `AuditLogService`: Log creation, hash computation, integrity verification
- Reason code validation
- Status transition validation
- Self-modification prevention

### Integration Tests

- End-to-end admin user management flow
- Track moderation with audit trail verification
- Analytics queries against Stage 7 aggregates
- Audit log pagination and filtering
- Hash chain integrity across multiple operations
- Rate limiting enforcement
- Authorization policy enforcement

### Security Tests

- Admin role requirement on all endpoints
- Self-modification prevention
- Audit log immutability
- Hash chain tampering detection

---

## 14. Implementation Tasks

### API Service

- [ ] Add `AuditLogEntry` document model
- [ ] Add `ModerationStatus` enum to Track model
- [ ] Add admin DTOs (user, track, analytics, audit)
- [ ] Add `IAdminUserService` interface and implementation
- [ ] Add `IAdminTrackService` interface and implementation
- [ ] Add `IAdminAnalyticsService` interface and implementation
- [ ] Add `IAuditLogService` interface and implementation
- [ ] Add `AdminEndpoints.cs` with all admin routes
- [ ] Add authorization policies for Admin role
- [ ] Add rate limiting policies for admin endpoints
- [ ] Add `AdminOptions` configuration class
- [ ] Add admin metrics to `NovaTuneMetrics`

### RavenDB

- [ ] Add `AuditLogs_ByFilters` index
- [ ] Add `Users_ForAdminSearch` index
- [ ] Add `Tracks_ForAdminSearch` index
- [ ] Configure audit log document expiration

### Integration

- [ ] Connect to Stage 7 analytics aggregates
- [ ] Connect to Stage 5 track deletion flow for admin deletes
- [ ] Connect to Stage 1 session management for revocation

### Testing

- [ ] Unit tests for admin services
- [ ] Unit tests for audit log hash chain
- [ ] Integration tests for admin endpoints
- [ ] Integration tests for audit integrity verification

---

## Requirements Covered

- `Req 11.1` — Admin list/search users; update user status
- `Req 11.2` — Admin list/search tracks; delete/moderate with reason codes and audit logs
- `Req 11.3` — Admin view analytics dashboards (play counts, recent activity)
- `NF-3.5` — Audit logs with actor, timestamp, action, target, reason codes; 1-year retention; Admin-only access; tamper-evidence

---

## Open Items

- [ ] Define notification mechanism for users when tracks are moderated
- [ ] Determine if admin 2FA is required for sensitive operations
- [ ] Evaluate IP allowlisting for admin endpoints
- [ ] Design bulk moderation operations for efficiency
- [ ] Consider real-time admin dashboard via WebSocket/SSE
- [ ] Evaluate external witness service for audit chain (blockchain, S3 Object Lock)
- [ ] Define appeal workflow for moderated content
- [ ] Determine anonymization strategy for audit logs of deleted users

---

## Claude Skills

The following Claude Code skills are available to assist with implementing Stage 8:

### Stage 8 Specific Skills

| Skill | Use For | Location |
|-------|---------|----------|
| `implement-admin-moderation` | Overall Stage 8 planning and implementation | `.claude/skills/implement-admin-moderation/SKILL.md` |
| `add-audit-logging` | Tamper-evident audit logs with hash chain | `.claude/skills/add-audit-logging/SKILL.md` |
| `add-admin-endpoints` | Admin API endpoints with authorization | `.claude/skills/add-admin-endpoints/SKILL.md` |

### Supporting Skills

| Skill | Use For | Stage 8 Components |
|-------|---------|-------------------|
| `add-api-endpoint` | Minimal API endpoint structure | All admin endpoints |
| `add-cursor-pagination` | Cursor-based pagination | User/track/audit list endpoints |
| `add-ravendb-index` | RavenDB index creation | Admin search indexes |
| `add-rate-limiting` | Rate limiting policies | Admin endpoint limits |
| `add-observability` | Metrics, logging, tracing | Admin operation metrics |

### Usage

Invoke skills using the Skill tool:
```
Skill: implement-admin-moderation  # For overall Stage 8 planning
Skill: add-audit-logging           # For tamper-evident audit infrastructure
Skill: add-admin-endpoints         # For admin API implementation
Skill: add-cursor-pagination       # For paginated lists
Skill: add-ravendb-index           # For search indexes
Skill: add-rate-limiting           # For admin rate limits
Skill: add-observability           # For metrics and tracing
```

---

## Claude Agents

The following Claude Code agents are available for autonomous task execution:

### Stage 8 Agents

| Agent | Description | Location |
|-------|-------------|----------|
| `admin-planner` | Plan Stage 8 implementation with architecture decisions | `.claude/agents/admin-planner.md` |
| `admin-api-implementer` | Implement admin API endpoints (user, track, analytics) | `.claude/agents/admin-api-implementer.md` |
| `audit-service-implementer` | Implement audit logging with hash chain | `.claude/agents/audit-service-implementer.md` |
| `admin-tester` | Write unit and integration tests | `.claude/agents/admin-tester.md` |

### Workflow Example

Use agents for structured implementation:

```
# Phase 1: Planning
Task(subagent_type="admin-planner", prompt="Analyze codebase and create detailed implementation plan for Stage 8")

# Phase 2: Audit Infrastructure
Task(subagent_type="audit-service-implementer", prompt="Implement AuditLogService with SHA-256 hash chain and integrity verification")

# Phase 3: Admin APIs
Task(subagent_type="admin-api-implementer", prompt="Implement AdminUserService, AdminTrackService, and AdminAnalyticsService with endpoints")

# Phase 4: Testing
Task(subagent_type="admin-tester", prompt="Write comprehensive unit and integration tests for admin functionality")
```

### Parallel Implementation

For faster implementation, run independent phases in parallel:

```
# Run audit service and API implementation in parallel
Task(subagent_type="audit-service-implementer", prompt="Implement audit logging infrastructure", run_in_background=true)
Task(subagent_type="admin-api-implementer", prompt="Implement admin DTOs, indexes, and service interfaces")
```
