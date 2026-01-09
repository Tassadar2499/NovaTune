using KafkaFlow;
using NovaTuneApp.ApiService.Models.Storage;
using NovaTuneApp.Workers.UploadIngestor.Services;

namespace NovaTuneApp.Workers.UploadIngestor.Handlers;

/// <summary>
/// KafkaFlow handler for MinIO bucket notification events.
/// Processes uploads and creates Track records.
/// </summary>
public class MinioEventHandler : IMessageHandler<MinioEvent>
{
    private readonly IUploadIngestorService _ingestorService;
    private readonly ILogger<MinioEventHandler> _logger;

    public MinioEventHandler(
        IUploadIngestorService ingestorService,
        ILogger<MinioEventHandler> logger)
    {
        _ingestorService = ingestorService;
        _logger = logger;
    }

    public async Task Handle(IMessageContext context, MinioEvent message)
    {
        // Only process ObjectCreated events
        if (!message.EventName.StartsWith("s3:ObjectCreated:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Ignoring non-create event: {EventName}", message.EventName);
            return;
        }

        var record = message.Records.FirstOrDefault();
        if (record is null)
        {
            _logger.LogWarning("MinIO event has no records, skipping");
            return;
        }

        var objectKey = record.S3.Object.Key;
        if (string.IsNullOrEmpty(objectKey))
        {
            _logger.LogWarning("MinIO event has empty object key, skipping");
            return;
        }

        // Only process audio uploads (prefix: "audio/")
        if (!objectKey.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Ignoring non-audio object: {ObjectKey}", objectKey);
            return;
        }

        _logger.LogInformation(
            "Processing MinIO upload event for {ObjectKey}, size: {Size}, type: {ContentType}",
            objectKey,
            record.S3.Object.Size,
            record.S3.Object.ContentType);

        try
        {
            await _ingestorService.ProcessUploadAsync(
                objectKey,
                record.S3.Object.ContentType,
                record.S3.Object.Size,
                record.S3.Object.ETag,
                context.ConsumerContext.WorkerStopped);

            _logger.LogInformation("Successfully processed upload for {ObjectKey}", objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process upload for {ObjectKey}", objectKey);
            throw; // Re-throw to trigger retry/DLQ
        }
    }
}
