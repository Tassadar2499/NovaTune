using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Exceptions;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Models.Upload;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Implementation of IUploadService with validation, quota checks, and presigned URL generation.
/// </summary>
public class UploadService : IUploadService
{
    private readonly IAsyncDocumentSession _session;
    private readonly IStorageService _storageService;
    private readonly NovaTuneOptions _options;
    private readonly ILogger<UploadService> _logger;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg",    // .mp3
        "audio/mp4",     // .m4a
        "audio/flac",    // .flac
        "audio/wav",     // .wav
        "audio/x-wav",   // .wav (alternative)
        "audio/ogg"      // .ogg
    };

    public UploadService(
        IAsyncDocumentSession session,
        IStorageService storageService,
        IOptions<NovaTuneOptions> options,
        ILogger<UploadService> logger)
    {
        _session = session;
        _storageService = storageService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InitiateUploadResponse> InitiateUploadAsync(
        string userId,
        InitiateUploadRequest request,
        CancellationToken ct = default)
    {
        // 1. Validate MIME type
        if (!AllowedMimeTypes.Contains(request.MimeType))
        {
            throw new UploadException(
                UploadErrorType.UnsupportedMimeType,
                $"File type '{request.MimeType}' is not supported. Supported types: {string.Join(", ", AllowedMimeTypes)}");
        }

        // 2. Validate file size
        if (request.FileSizeBytes > _options.Quotas.MaxUploadSizeBytes)
        {
            throw new UploadException(
                UploadErrorType.FileTooLarge,
                $"File size {FormatBytes(request.FileSizeBytes)} exceeds maximum allowed size of {FormatBytes(_options.Quotas.MaxUploadSizeBytes)}.");
        }

        // 3. Validate filename
        if (string.IsNullOrWhiteSpace(request.FileName) ||
            request.FileName.Length > 255 ||
            request.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new UploadException(
                UploadErrorType.InvalidFileName,
                "File name is invalid or contains illegal characters.");
        }

        // 4. Load user and check quota
        var user = await _session.LoadAsync<ApplicationUser>($"ApplicationUsers/{userId}", ct);
        if (user == null)
        {
            throw new UploadException(
                UploadErrorType.SessionNotFound,
                "User not found.",
                404);
        }

        var projectedUsage = user.UsedStorageBytes + request.FileSizeBytes;
        if (projectedUsage > _options.Quotas.MaxStoragePerUserBytes)
        {
            throw new UploadException(
                UploadErrorType.QuotaExceeded,
                $"You have used {FormatBytes(user.UsedStorageBytes)} of your {FormatBytes(_options.Quotas.MaxStoragePerUserBytes)} storage quota.",
                400,
                new Dictionary<string, object>
                {
                    ["usedBytes"] = user.UsedStorageBytes,
                    ["quotaBytes"] = _options.Quotas.MaxStoragePerUserBytes
                });
        }

        // 5. Generate IDs
        var uploadId = Ulid.NewUlid().ToString();
        var trackId = Ulid.NewUlid().ToString();
        var randomSuffix = GenerateRandomSuffix();
        var objectKey = $"audio/{userId}/{trackId}/{randomSuffix}";

        // 6. Generate presigned URL
        var ttl = _options.PresignedUrl.Ttl;
        var presigned = await _storageService.GeneratePresignedUploadUrlAsync(
            objectKey, request.MimeType, request.FileSizeBytes, ttl, ct);

        // 7. Create UploadSession document
        var uploadSession = new UploadSession
        {
            Id = $"UploadSessions/{uploadId}",
            UploadId = uploadId,
            UserId = userId,
            ReservedTrackId = trackId,
            ObjectKey = objectKey,
            ExpectedMimeType = request.MimeType,
            MaxAllowedSizeBytes = request.FileSizeBytes,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = presigned.ExpiresAt,
            Status = UploadSessionStatus.Pending,
            Title = request.Title ?? Path.GetFileNameWithoutExtension(request.FileName),
            Artist = request.Artist
        };

        await _session.StoreAsync(uploadSession, ct);
        await _session.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Upload session initiated: {UploadId} for user {UserId}, track {TrackId}, file {FileName} ({FileSize})",
            uploadId, userId, trackId, request.FileName, FormatBytes(request.FileSizeBytes));

        return new InitiateUploadResponse(
            uploadId,
            trackId,
            presigned.Url,
            presigned.ExpiresAt,
            objectKey);
    }

    /// <summary>
    /// Generates a 16-byte base64url-encoded random suffix for guess-resistance.
    /// </summary>
    private static string GenerateRandomSuffix()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Formats bytes as human-readable string.
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
