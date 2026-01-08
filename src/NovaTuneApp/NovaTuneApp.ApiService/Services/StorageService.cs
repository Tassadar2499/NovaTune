using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using Polly;
using Polly.Registry;

namespace NovaTuneApp.ApiService.Services;

/// <summary>
/// Implementation of IStorageService with MinIO integration and resilience scaffolding (NF-1.4).
/// Storage operations are wrapped with timeout, circuit breaker, and bulkhead policies.
/// </summary>
public class StorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<StorageService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly string _audioBucket;

    public StorageService(
        IMinioClient minioClient,
        IOptions<NovaTuneOptions> options,
        ILogger<StorageService> logger,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _minioClient = minioClient;
        _logger = logger;
        _resiliencePipeline = pipelineProvider.GetPipeline(ResilienceExtensions.StoragePipeline);
        _audioBucket = options.Value.Minio.AudioBucketName;
    }

    public async Task ScheduleDeletionAsync(Guid trackId, TimeSpan gracePeriod, CancellationToken ct = default)
    {
        await _resiliencePipeline.ExecuteAsync(async token =>
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
        return await _resiliencePipeline.ExecuteAsync(async token =>
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
}
