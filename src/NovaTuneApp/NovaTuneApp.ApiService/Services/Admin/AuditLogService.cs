using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services.Admin;

/// <summary>
/// Audit log service with SHA-256 hash chain for tamper evidence (NF-3.5).
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IOptions<AdminOptions> _options;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IAsyncDocumentSession session,
        IOptions<AdminOptions> options,
        ILogger<AuditLogService> logger)
    {
        _session = session;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
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
            Expires = now.AddDays(_options.Value.AuditRetentionDays)
        };

        // Compute content hash
        entry = entry with { ContentHash = ComputeHash(entry) };

        await _session.StoreAsync(entry, ct);
        await _session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Audit entry created: {AuditId} {Action} {TargetType}/{TargetId} by {ActorUserId}",
            auditId, request.Action, request.TargetType, request.TargetId, request.ActorUserId);

        return entry;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogListItem>> ListAsync(
        AuditLogQuery query,
        CancellationToken ct = default)
    {
        var limit = Math.Min(query.Limit, _options.Value.MaxAuditPageSize);
        if (limit <= 0) limit = _options.Value.DefaultAuditPageSize;

        var dbQuery = _session.Query<AuditLogEntry>();

        // Apply filters
        if (!string.IsNullOrEmpty(query.ActorUserId))
            dbQuery = dbQuery.Where(e => e.ActorUserId == query.ActorUserId);

        if (!string.IsNullOrEmpty(query.Action))
            dbQuery = dbQuery.Where(e => e.Action == query.Action);

        if (!string.IsNullOrEmpty(query.TargetType))
            dbQuery = dbQuery.Where(e => e.TargetType == query.TargetType);

        if (!string.IsNullOrEmpty(query.TargetId))
            dbQuery = dbQuery.Where(e => e.TargetId == query.TargetId);

        if (query.StartDate.HasValue)
        {
            var startDateTime = query.StartDate.Value.ToDateTime(TimeOnly.MinValue);
            dbQuery = dbQuery.Where(e => e.Timestamp >= startDateTime);
        }

        if (query.EndDate.HasValue)
        {
            var endDateTime = query.EndDate.Value.ToDateTime(TimeOnly.MaxValue);
            dbQuery = dbQuery.Where(e => e.Timestamp <= endDateTime);
        }

        // Apply cursor-based pagination
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            var cursorTimestamp = DecodeCursor(query.Cursor);
            if (cursorTimestamp.HasValue)
            {
                dbQuery = dbQuery.Where(e => e.Timestamp < cursorTimestamp.Value);
            }
        }

        // Order by timestamp descending (newest first)
        var entries = await dbQuery
            .OrderByDescending(e => e.Timestamp)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = entries.Count > limit;
        if (hasMore)
            entries = entries.Take(limit).ToList();

        var items = entries.Select(e => new AuditLogListItem(
            e.AuditId,
            e.ActorUserId,
            e.ActorEmail,
            e.Action,
            e.TargetType,
            e.TargetId,
            e.ReasonCode,
            e.Timestamp)).ToList();

        var nextCursor = hasMore && entries.Count > 0
            ? EncodeCursor(entries.Last().Timestamp)
            : null;

        // TotalCount is -1 for cursor pagination (no COUNT query performed)
        return new PagedResult<AuditLogListItem>(items, nextCursor, -1, hasMore);
    }

    /// <inheritdoc />
    public async Task<AuditLogDetails?> GetAsync(
        string auditId,
        CancellationToken ct = default)
    {
        var entry = await _session.LoadAsync<AuditLogEntry>($"AuditLogs/{auditId}", ct);
        if (entry is null)
            return null;

        return new AuditLogDetails(
            entry.AuditId,
            entry.ActorUserId,
            entry.ActorEmail,
            entry.Action,
            entry.TargetType,
            entry.TargetId,
            entry.ReasonCode,
            entry.ReasonText,
            entry.PreviousState,
            entry.NewState,
            entry.Timestamp,
            entry.CorrelationId,
            entry.IpAddress);
    }

    /// <inheritdoc />
    public async Task<AuditIntegrityResult> VerifyIntegrityAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        if (!_options.Value.EnableIntegrityVerification)
        {
            return new AuditIntegrityResult(true, 0, 0, []);
        }

        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

        var entries = await _session
            .Query<AuditLogEntry>()
            .Where(e => e.Timestamp >= startDateTime && e.Timestamp <= endDateTime)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(ct);

        var invalidIds = new List<string>();
        string? expectedPreviousHash = null;

        // Get the hash of entry before our range (if any)
        var previousEntry = await _session
            .Query<AuditLogEntry>()
            .Where(e => e.Timestamp < startDateTime)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync(ct);

        expectedPreviousHash = previousEntry?.ContentHash;

        foreach (var entry in entries)
        {
            // Verify previous hash chain
            if (entry.PreviousEntryHash != expectedPreviousHash)
            {
                _logger.LogError(
                    "Audit chain broken at {AuditId}: expected previous hash {Expected}, found {Actual}",
                    entry.AuditId, expectedPreviousHash ?? "(null)", entry.PreviousEntryHash ?? "(null)");
                invalidIds.Add(entry.AuditId);
            }

            // Verify content hash
            var computedHash = ComputeHash(entry);
            if (entry.ContentHash != computedHash)
            {
                _logger.LogError(
                    "Audit content tampered at {AuditId}: expected hash {Expected}, found {Actual}",
                    entry.AuditId, computedHash, entry.ContentHash ?? "(null)");
                if (!invalidIds.Contains(entry.AuditId))
                    invalidIds.Add(entry.AuditId);
            }

            expectedPreviousHash = entry.ContentHash;
        }

        var result = new AuditIntegrityResult(
            invalidIds.Count == 0,
            entries.Count,
            invalidIds.Count,
            invalidIds);

        if (!result.IsValid)
        {
            _logger.LogError(
                "Audit integrity check FAILED: {InvalidCount}/{TotalCount} entries invalid",
                result.InvalidEntries, result.EntriesChecked);
        }
        else
        {
            _logger.LogInformation(
                "Audit integrity check passed: {TotalCount} entries verified",
                result.EntriesChecked);
        }

        return result;
    }

    /// <summary>
    /// Computes SHA-256 hash of audit entry content for tamper evidence.
    /// </summary>
    public static string ComputeHash(AuditLogEntry entry)
    {
        var content = string.Join("|",
            entry.AuditId,
            entry.ActorUserId,
            entry.ActorEmail,
            entry.Action,
            entry.TargetType,
            entry.TargetId,
            entry.ReasonCode ?? "",
            entry.Timestamp.ToString("O"),
            entry.PreviousState ?? "",
            entry.NewState ?? "",
            entry.PreviousEntryHash ?? "");

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string EncodeCursor(DateTimeOffset timestamp) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(timestamp.ToString("O")));

    private static DateTimeOffset? DecodeCursor(string cursor)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return DateTimeOffset.Parse(decoded);
        }
        catch
        {
            return null;
        }
    }
}
