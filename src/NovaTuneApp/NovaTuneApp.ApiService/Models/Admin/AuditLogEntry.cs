using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NovaTuneApp.ApiService.Models.Admin;

/// <summary>
/// Audit log entry for admin actions (NF-3.5).
/// </summary>
public sealed record AuditLogEntry
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
