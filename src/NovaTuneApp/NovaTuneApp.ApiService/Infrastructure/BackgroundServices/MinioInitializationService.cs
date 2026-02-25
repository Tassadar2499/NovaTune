using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using NovaTuneApp.ApiService.Infrastructure.Configuration;

namespace NovaTuneApp.ApiService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that ensures MinIO bucket exists with proper configuration.
/// Runs once at startup to create bucket with versioning if needed.
/// </summary>
public class MinioInitializationService : IHostedService
{
    private readonly IMinioClient _minioClient;
    private readonly IOptions<NovaTuneOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MinioInitializationService> _logger;

    private const int MaxRetries = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public MinioInitializationService(
        IMinioClient minioClient,
        IOptions<NovaTuneOptions> options,
        IHostEnvironment environment,
        ILogger<MinioInitializationService> logger)
    {
        _minioClient = minioClient;
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var minioOptions = _options.Value.Minio;
        var bucketName = minioOptions.AudioBucketName;

        _logger.LogInformation(
            "Initializing MinIO bucket {BucketName} with versioning={Versioning}",
            bucketName,
            minioOptions.EnableVersioning);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await EnsureBucketExistsAsync(bucketName, cancellationToken);

                if (minioOptions.EnableVersioning)
                {
                    await EnableVersioningAsync(bucketName, cancellationToken);
                }

                _logger.LogInformation(
                    "MinIO bucket {BucketName} initialized successfully on attempt {Attempt}",
                    bucketName,
                    attempt);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to initialize MinIO bucket {BucketName} (attempt {Attempt}/{MaxRetries})",
                    bucketName,
                    attempt,
                    MaxRetries);

                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
                else
                {
                    _logger.LogError(
                        ex,
                        "Failed to initialize MinIO bucket {BucketName} after {MaxRetries} attempts. " +
                        "Storage operations may fail until bucket is created.",
                        bucketName,
                        MaxRetries);
                    // Don't throw - allow app to start, health checks will report degraded
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct)
    {
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
        var exists = await _minioClient.BucketExistsAsync(bucketExistsArgs, ct);

        if (!exists)
        {
            _logger.LogInformation("Creating MinIO bucket {BucketName}", bucketName);
            var makeBucketArgs = new MakeBucketArgs().WithBucket(bucketName);
            await _minioClient.MakeBucketAsync(makeBucketArgs, ct);
        }
        else
        {
            _logger.LogDebug("MinIO bucket {BucketName} already exists", bucketName);
        }
    }

    private async Task EnableVersioningAsync(string bucketName, CancellationToken ct)
    {
        try
        {
            var args = new SetVersioningArgs()
                .WithBucket(bucketName)
                .WithVersioningEnabled();

            await _minioClient.SetVersioningAsync(args, ct);
            _logger.LogDebug("Versioning enabled on bucket {BucketName}", bucketName);
        }
        catch (Exception ex)
        {
            // Log but don't fail - versioning might already be enabled or not supported
            _logger.LogWarning(
                ex,
                "Could not enable versioning on bucket {BucketName}. This may be expected if versioning is already enabled.",
                bucketName);
        }
    }
}
