using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models.Admin;

// ============================================================================
// User Management DTOs
// ============================================================================

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
    [property: Required] UserStatus Status,
    [property: Required] string ReasonCode,
    [property: MaxLength(1000)] string? ReasonText);

public record AdminUserListQuery(
    string? Search = null,
    UserStatus? Status = null,
    string SortBy = "createdAt",
    string SortOrder = "desc",
    string? Cursor = null,
    int Limit = 50);

// ============================================================================
// Track Moderation DTOs
// ============================================================================

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
    [property: Required] ModerationStatus ModerationStatus,
    [property: Required] string ReasonCode,
    [property: MaxLength(1000)] string? ReasonText);

public record AdminDeleteTrackRequest(
    [property: Required] string ReasonCode,
    [property: MaxLength(1000)] string? ReasonText);

public record AdminTrackListQuery(
    string? Search = null,
    TrackStatus? Status = null,
    ModerationStatus? ModerationStatus = null,
    string? UserId = null,
    string SortBy = "createdAt",
    string SortOrder = "desc",
    string? Cursor = null,
    int Limit = 50);

// ============================================================================
// Analytics Dashboard DTOs
// ============================================================================

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

public record TopTracksResponse(
    string Period,
    IReadOnlyList<TopTrackItem> Items);

public record UserActivityItem(
    string UserId,
    string Email,
    string? DisplayName,
    int TracksUploaded,
    int TotalPlays,
    TimeSpan TotalListenTime,
    DateTimeOffset LastActivityAt);

public record ActiveUsersResponse(
    string Period,
    IReadOnlyList<UserActivityItem> Items);

public enum AnalyticsPeriod
{
    Last24Hours,
    Last7Days,
    Last30Days,
    AllTime
}

// ============================================================================
// Audit Log DTOs
// ============================================================================

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
