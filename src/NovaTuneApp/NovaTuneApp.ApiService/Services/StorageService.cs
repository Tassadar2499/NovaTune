using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using Polly;
using Polly.Registry;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Implementation of IStorageService with MinIO integration and resilience scaffolding (NF-1.4).
/// Storage operations are wrapped with timeout, circuit breaker, retry, and bulkhead policies.
/// </summary>
public class StorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<StorageService> _logger;
    private readonly ResiliencePipeline _generalPipeline;
    private readonly ResiliencePipeline _presignPipeline;
    private readonly ResiliencePipeline _downloadPipeline;
    private readonly string _audioBucket;

    public StorageService(
        IMinioClient minioClient,
        IOptions<NovaTuneOptions> options,
        ILogger<StorageService> logger,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _minioClient = minioClient;
        _logger = logger;
        _generalPipeline = pipelineProvider.GetPipeline(ResilienceExtensions.StoragePipeline);
        _presignPipeline = pipelineProvider.GetPipeline(ResilienceExtensions.StoragePresignPipeline);
        _downloadPipeline = pipelineProvider.GetPipeline(ResilienceExtensions.StorageDownloadPipeline);
        _audioBucket = options.Value.Minio.AudioBucketName;
    }

    public async Task ScheduleDeletionAsync(Guid trackId, TimeSpan gracePeriod, CancellationToken ct = default)
    {
        await _generalPipeline.ExecuteAsync(async token =>
        {
            // TODO: Replace stub with actual MinIO/S3 deletion scheduling
            _logger.LogInformation(
                "StorageService.ScheduleDeletionAsync called for {TrackId} with grace period {GracePeriod} (stub)",
                trackId, gracePeriod);
            await Task.CompletedTask;
        }, ct);
    }

    public async Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        long contentLength,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        // Use presign pipeline: 5s timeout, 1 retry per NF-1.4
        return await _presignPipeline.ExecuteAsync(async token =>
        {
            var args = new PresignedPutObjectArgs()
                .WithBucket(_audioBucket)
                .WithObject(objectKey)
                .WithExpiry((int)expiry.TotalSeconds)
                .WithHeaders(new Dictionary<string, string>
                {
                    ["Content-Type"] = contentType,
                    ["Content-Length"] = contentLength.ToString()
                });

            var url = await _minioClient.PresignedPutObjectAsync(args);
            var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

            _logger.LogDebug(
                "Generated presigned upload URL for {ObjectKey}, expires at {ExpiresAt}",
                objectKey, expiresAt);

            return new PresignedUploadResult(url, expiresAt);
        }, ct);
    }

    /// <summary>
    /// Downloads an object to a local file using streaming IO (NF-2.4).
    /// </summary>
    public async Task DownloadToFileAsync(string objectKey, string destinationPath, CancellationToken ct = default)
    {
        await _generalPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Downloading {ObjectKey} to {DestinationPath}", objectKey, destinationPath);

            var args = new GetObjectArgs()
                .WithBucket(_audioBucket)
                .WithObject(objectKey)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    // Use streaming IO to avoid unbounded memory (NF-2.4)
                    await using var fileStream = new FileStream(
                        destinationPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920, // 80KB buffer
                        useAsync: true);

                    await stream.CopyToAsync(fileStream, cancellationToken);
                });

            await _minioClient.GetObjectAsync(args, token);

            _logger.LogDebug("Downloaded {ObjectKey} to {DestinationPath}", objectKey, destinationPath);
        }, ct);
    }

    /// <summary>
    /// Downloads a large object to a local file using streaming IO (NF-2.4).
    /// Uses 5-minute timeout for files up to 500 MB per 10-resilience.md.
    /// </summary>
    public async Task DownloadLargeFileAsync(string objectKey, string destinationPath, CancellationToken ct = default)
    {
        await _downloadPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Downloading large file {ObjectKey} to {DestinationPath}", objectKey, destinationPath);

            var args = new GetObjectArgs()
                .WithBucket(_audioBucket)
                .WithObject(objectKey)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    // Use streaming IO to avoid unbounded memory (NF-2.4)
                    await using var fileStream = new FileStream(
                        destinationPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920, // 80KB buffer
                        useAsync: true);

                    await stream.CopyToAsync(fileStream, cancellationToken);
                });

            await _minioClient.GetObjectAsync(args, token);

            _logger.LogDebug("Downloaded large file {ObjectKey} to {DestinationPath}", objectKey, destinationPath);
        }, ct);
    }

    /// <summary>
    /// Uploads a local file to storage using streaming IO (NF-2.4).
    /// </summary>
    public async Task UploadFromFileAsync(string objectKey, string sourcePath, string contentType, CancellationToken ct = default)
    {
        await _generalPipeline.ExecuteAsync(async token =>
        {
            var fileInfo = new FileInfo(sourcePath);
            _logger.LogDebug(
                "Uploading {SourcePath} ({Size} bytes) to {ObjectKey}",
                sourcePath, fileInfo.Length, objectKey);

            var args = new PutObjectArgs()
                .WithBucket(_audioBucket)
                .WithObject(objectKey)
                .WithFileName(sourcePath)
                .WithContentType(contentType)
                .WithObjectSize(fileInfo.Length);

            await _minioClient.PutObjectAsync(args, token);

            _logger.LogDebug("Uploaded {SourcePath} to {ObjectKey}", sourcePath, objectKey);
        }, ct);
    }

    /// <summary>
    /// Generates a presigned GET URL for streaming (Req 5.2, NF-3.3).
    /// Uses presign pipeline: 5s timeout, 1 retry per NF-1.4.
    /// </summary>
    public async Task<PresignedDownloadResult> GeneratePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        return await _presignPipeline.ExecuteAsync(async token =>
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(_audioBucket)
                .WithObject(objectKey)
                .WithExpiry((int)expiry.TotalSeconds);

            var url = await _minioClient.PresignedGetObjectAsync(args);
            var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

            _logger.LogDebug(
                "Generated presigned download URL for {ObjectKey}, expires at {ExpiresAt}",
                objectKey, expiresAt);

            return new PresignedDownloadResult(url, expiresAt);
        }, ct);
    }

    /// <summary>
    /// Deletes an object from storage (Stage 5).
    /// Uses general pipeline with timeout and circuit breaker.
    /// </summary>
    public async Task DeleteObjectAsync(string objectKey, CancellationToken ct = default)
    {
        await _generalPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Deleting object {ObjectKey}", objectKey);

            var args = new RemoveObjectArgs()
                .WithBucket(_audioBucket)
                .WithObject(objectKey);

            await _minioClient.RemoveObjectAsync(args, token);

            _logger.LogDebug("Deleted object {ObjectKey}", objectKey);
        }, ct);
    }
}
