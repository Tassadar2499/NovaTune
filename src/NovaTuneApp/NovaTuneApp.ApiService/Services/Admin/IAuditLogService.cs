using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;

namespace NovaTuneApp.ApiService.Services.Admin;

/// <summary>
/// Service for managing tamper-evident audit logs (NF-3.5).
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Records an audit log entry with hash chain linking.
    /// </summary>
    Task<AuditLogEntry> LogAsync(
        AuditLogRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Lists audit log entries with filtering and pagination.
    /// </summary>
    Task<PagedResult<AuditLogListItem>> ListAsync(
        AuditLogQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a specific audit log entry by ID.
    /// </summary>
    Task<AuditLogDetails?> GetAsync(
        string auditId,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies the integrity of the audit log hash chain.
    /// </summary>
    Task<AuditIntegrityResult> VerifyIntegrityAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}
