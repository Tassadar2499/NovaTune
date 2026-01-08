using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models.Upload;
using Raven.Client.Documents;

namespace NovaTuneApp.ApiService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that cleans up expired upload sessions.
/// Per spec: marks expired sessions as Expired, deletes them after 24 hours retention.
/// </summary>
public class UploadSessionCleanupService : BackgroundService
{
    private readonly IDocumentStore _documentStore;
    private readonly IOptions<NovaTuneOptions> _options;
    private readonly ILogger<UploadSessionCleanupService> _logger;

    public UploadSessionCleanupService(
        IDocumentStore documentStore,
        IOptions<NovaTuneOptions> options,
        ILogger<UploadSessionCleanupService> logger)
    {
        _documentStore = documentStore;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Upload session cleanup service starting. Interval: {Interval}, Retention: {Retention}",
            _options.Value.UploadSession.CleanupInterval,
            _options.Value.UploadSession.RetentionPeriod);

        // Initial delay to let the application start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during upload session cleanup");
            }

            await Task.Delay(_options.Value.UploadSession.CleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Upload session cleanup service stopped");
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionOptions = _options.Value.UploadSession;
        var batchSize = sessionOptions.CleanupBatchSize;

        // Phase 1: Mark expired pending sessions as Expired
        var expiredCount = await MarkExpiredSessionsAsync(now, batchSize, ct);

        // Phase 2: Delete expired sessions older than retention period
        var retentionCutoff = now - sessionOptions.RetentionPeriod;
        var deletedCount = await DeleteOldExpiredSessionsAsync(retentionCutoff, batchSize, ct);

        if (expiredCount > 0 || deletedCount > 0)
        {
            _logger.LogInformation(
                "Upload session cleanup completed. Marked expired: {ExpiredCount}, Deleted: {DeletedCount}",
                expiredCount,
                deletedCount);
        }
    }

    /// <summary>
    /// Marks pending sessions that have passed their expiry time as Expired.
    /// </summary>
    private async Task<int> MarkExpiredSessionsAsync(DateTimeOffset now, int batchSize, CancellationToken ct)
    {
        using var session = _documentStore.OpenAsyncSession();

        var expiredSessions = await session
            .Query<UploadSession>()
            .Where(s => s.Status == UploadSessionStatus.Pending && s.ExpiresAt < now)
            .Take(batchSize)
            .ToListAsync(ct);

        if (expiredSessions.Count == 0)
        {
            return 0;
        }

        foreach (var uploadSession in expiredSessions)
        {
            uploadSession.Status = UploadSessionStatus.Expired;
            _logger.LogDebug(
                "Marking upload session {UploadId} as expired (expired at {ExpiresAt})",
                uploadSession.UploadId,
                uploadSession.ExpiresAt);
        }

        await session.SaveChangesAsync(ct);

        return expiredSessions.Count;
    }

    /// <summary>
    /// Deletes expired sessions that have exceeded the retention period.
    /// </summary>
    private async Task<int> DeleteOldExpiredSessionsAsync(DateTimeOffset retentionCutoff, int batchSize, CancellationToken ct)
    {
        using var session = _documentStore.OpenAsyncSession();

        var sessionsToDelete = await session
            .Query<UploadSession>()
            .Where(s => s.Status == UploadSessionStatus.Expired && s.ExpiresAt < retentionCutoff)
            .Take(batchSize)
            .ToListAsync(ct);

        if (sessionsToDelete.Count == 0)
        {
            return 0;
        }

        foreach (var uploadSession in sessionsToDelete)
        {
            session.Delete(uploadSession);
            _logger.LogDebug(
                "Deleting expired upload session {UploadId} (expired at {ExpiresAt})",
                uploadSession.UploadId,
                uploadSession.ExpiresAt);
        }

        await session.SaveChangesAsync(ct);

        return sessionsToDelete.Count;
    }
}
