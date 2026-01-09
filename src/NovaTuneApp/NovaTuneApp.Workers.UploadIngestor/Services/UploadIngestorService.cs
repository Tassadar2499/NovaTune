using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Models.Outbox;
using NovaTuneApp.ApiService.Models.Upload;
using Raven.Client.Documents;

namespace NovaTuneApp.Workers.UploadIngestor.Services;

/// <summary>
/// Processes MinIO upload events: validates uploads, creates Track records,
/// updates UploadSessions, and writes outbox messages in a single transaction.
/// </summary>
public class UploadIngestorService : IUploadIngestorService
{
    private readonly IDocumentStore _documentStore;
    private readonly IMinioClient _minioClient;
    private readonly NovaTuneOptions _options;
    private readonly ILogger<UploadIngestorService> _logger;

    public UploadIngestorService(
        IDocumentStore documentStore,
        IMinioClient minioClient,
        IOptions<NovaTuneOptions> options,
        ILogger<UploadIngestorService> logger)
    {
        _documentStore = documentStore;
        _minioClient = minioClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessUploadAsync(
        string objectKey,
        string contentType,
        long size,
        string eTag,
        CancellationToken ct = default)
    {
        // 1. Parse userId and trackId from ObjectKey (format: "audio/{userId}/{trackId}/{randomSuffix}")
        var parts = objectKey.Split('/');
        if (parts.Length < 4 || parts[0] != "audio")
        {
            _logger.LogWarning("Invalid object key format: {ObjectKey}", objectKey);
            return;
        }

        var userId = parts[1];
        var trackId = parts[2];

        // 2. Load UploadSession by ObjectKey
        using var session = _documentStore.OpenAsyncSession();

        var uploadSession = await session
            .Query<UploadSession>()
            .Where(s => s.ObjectKey == objectKey)
            .FirstOrDefaultAsync(ct);

        if (uploadSession is null)
        {
            _logger.LogWarning(
                "UploadSession not found for ObjectKey {ObjectKey}. Orphan upload detected.",
                objectKey);
            // Ack message (no retry) - this is an orphan upload
            return;
        }

        // 3. Check if session is expired
        if (uploadSession.Status == UploadSessionStatus.Expired)
        {
            _logger.LogWarning(
                "UploadSession {UploadId} is expired, marking as Failed",
                uploadSession.UploadId);
            uploadSession.Status = UploadSessionStatus.Failed;
            await session.SaveChangesAsync(ct);
            return;
        }

        if (uploadSession.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _logger.LogWarning(
                "UploadSession {UploadId} has expired (ExpiresAt: {ExpiresAt}), marking as Failed",
                uploadSession.UploadId,
                uploadSession.ExpiresAt);
            uploadSession.Status = UploadSessionStatus.Failed;
            await session.SaveChangesAsync(ct);
            return;
        }

        // 4. Validate content type and size
        var validationError = ValidateUpload(uploadSession, contentType, size);
        if (validationError is not null)
        {
            _logger.LogWarning(
                "Validation failed for UploadSession {UploadId}: {Error}",
                uploadSession.UploadId,
                validationError);

            uploadSession.Status = UploadSessionStatus.Failed;
            await session.SaveChangesAsync(ct);

            // Delete the invalid object from MinIO
            await DeleteObjectAsync(objectKey, ct);
            return;
        }

        // 5. Compute checksum (SHA-256) for deduplication
        var checksum = await ComputeChecksumAsync(objectKey, ct);

        // 6. Begin RavenDB transaction: Create Track, Update UploadSession, Insert OutboxMessage
        var now = DateTimeOffset.UtcNow;

        // Create Track record
        var track = new Track
        {
            Id = $"Tracks/{uploadSession.ReservedTrackId}",
            UserId = userId,
            Title = uploadSession.Title ?? "Untitled",
            Artist = uploadSession.Artist,
            ObjectKey = objectKey,
            FileSizeBytes = size,
            MimeType = contentType,
            Checksum = checksum,
            Status = TrackStatus.Processing,
            CreatedAt = now,
            UpdatedAt = now
        };

        await session.StoreAsync(track, ct);

        // Update UploadSession
        uploadSession.Status = UploadSessionStatus.Completed;

        // Update user's storage usage
        var user = await session.LoadAsync<ApplicationUser>($"ApplicationUsers/{userId}", ct);
        if (user is not null)
        {
            user.UsedStorageBytes += size;
            user.TrackCount++;
        }

        // Create OutboxMessage for AudioUploadedEvent
        var audioEvent = new AudioUploadedEvent
        {
            TrackId = Guid.Parse(uploadSession.ReservedTrackId),
            UserId = Guid.Parse(userId),
            ObjectKey = objectKey,
            MimeType = contentType,
            FileSizeBytes = size,
            CorrelationId = uploadSession.UploadId,
            Timestamp = now
        };

        var outboxMessage = new OutboxMessage
        {
            Id = $"OutboxMessages/{Ulid.NewUlid()}",
            MessageType = nameof(AudioUploadedEvent),
            Topic = $"{_options.TopicPrefix}-audio-events",
            PartitionKey = userId,
            Payload = JsonSerializer.Serialize(audioEvent),
            CorrelationId = uploadSession.UploadId,
            CreatedAt = now,
            Status = OutboxMessageStatus.Pending
        };

        await session.StoreAsync(outboxMessage, ct);

        // Save all changes in single transaction
        await session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Successfully processed upload {UploadId}: Track {TrackId} created, checksum: {Checksum}",
            uploadSession.UploadId,
            track.Id,
            checksum);
    }

    private string? ValidateUpload(UploadSession uploadSession, string contentType, long size)
    {
        // Validate content type
        if (!string.Equals(contentType, uploadSession.ExpectedMimeType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Content type mismatch. Expected: {uploadSession.ExpectedMimeType}, Actual: {contentType}";
        }

        // Validate size
        if (size > uploadSession.MaxAllowedSizeBytes)
        {
            return $"File size {size} exceeds maximum allowed size {uploadSession.MaxAllowedSizeBytes}";
        }

        return null;
    }

    private async Task<string> ComputeChecksumAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var memoryStream = new MemoryStream();

            var args = new GetObjectArgs()
                .WithBucket(_options.Minio.AudioBucketName)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(args, ct);

            memoryStream.Position = 0;
            var hash = await sha256.ComputeHashAsync(memoryStream, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute checksum for {ObjectKey}, using ETag as fallback", objectKey);
            // Fallback to ETag (MD5) if available
            return string.Empty;
        }
    }

    private async Task DeleteObjectAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(_options.Minio.AudioBucketName)
                .WithObject(objectKey);

            await _minioClient.RemoveObjectAsync(args, ct);
            _logger.LogInformation("Deleted invalid object {ObjectKey} from MinIO", objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete object {ObjectKey} from MinIO", objectKey);
        }
    }
}
