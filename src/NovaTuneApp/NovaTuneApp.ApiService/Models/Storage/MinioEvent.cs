using System.Text.Json.Serialization;

namespace NovaTuneApp.ApiService.Models.Storage;

/// <summary>
/// MinIO bucket notification event payload.
/// Published to Kafka/Redpanda when objects are created/deleted.
/// </summary>
public sealed class MinioEvent
{
    /// <summary>
    /// Event name (e.g., "s3:ObjectCreated:Put", "s3:ObjectRemoved:Delete").
    /// </summary>
    [JsonPropertyName("EventName")]
    public string EventName { get; init; } = string.Empty;

    /// <summary>
    /// Object key that triggered the event.
    /// </summary>
    [JsonPropertyName("Key")]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Event records containing detailed information.
    /// </summary>
    [JsonPropertyName("Records")]
    public List<MinioEventRecord> Records { get; init; } = [];
}

/// <summary>
/// Individual record within a MinIO event.
/// </summary>
public sealed class MinioEventRecord
{
    /// <summary>
    /// Event version.
    /// </summary>
    [JsonPropertyName("eventVersion")]
    public string EventVersion { get; init; } = string.Empty;

    /// <summary>
    /// Event source (e.g., "minio:s3").
    /// </summary>
    [JsonPropertyName("eventSource")]
    public string EventSource { get; init; } = string.Empty;

    /// <summary>
    /// AWS region (typically empty for MinIO).
    /// </summary>
    [JsonPropertyName("awsRegion")]
    public string AwsRegion { get; init; } = string.Empty;

    /// <summary>
    /// Event timestamp.
    /// </summary>
    [JsonPropertyName("eventTime")]
    public DateTimeOffset EventTime { get; init; }

    /// <summary>
    /// Event name within the record.
    /// </summary>
    [JsonPropertyName("eventName")]
    public string EventName { get; init; } = string.Empty;

    /// <summary>
    /// S3-specific event data.
    /// </summary>
    [JsonPropertyName("s3")]
    public MinioS3Data S3 { get; init; } = new();
}

/// <summary>
/// S3-specific data within a MinIO event record.
/// </summary>
public sealed class MinioS3Data
{
    /// <summary>
    /// S3 schema version.
    /// </summary>
    [JsonPropertyName("s3SchemaVersion")]
    public string S3SchemaVersion { get; init; } = string.Empty;

    /// <summary>
    /// Configuration ID for the notification.
    /// </summary>
    [JsonPropertyName("configurationId")]
    public string ConfigurationId { get; init; } = string.Empty;

    /// <summary>
    /// Bucket information.
    /// </summary>
    [JsonPropertyName("bucket")]
    public MinioBucketInfo Bucket { get; init; } = new();

    /// <summary>
    /// Object information.
    /// </summary>
    [JsonPropertyName("object")]
    public MinioObjectInfo Object { get; init; } = new();
}

/// <summary>
/// Bucket information in MinIO event.
/// </summary>
public sealed class MinioBucketInfo
{
    /// <summary>
    /// Bucket name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Bucket owner identity.
    /// </summary>
    [JsonPropertyName("ownerIdentity")]
    public MinioIdentity OwnerIdentity { get; init; } = new();

    /// <summary>
    /// Bucket ARN.
    /// </summary>
    [JsonPropertyName("arn")]
    public string Arn { get; init; } = string.Empty;
}

/// <summary>
/// Object information in MinIO event.
/// </summary>
public sealed class MinioObjectInfo
{
    /// <summary>
    /// Object key (path).
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Object size in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>
    /// Object ETag (MD5 hash).
    /// </summary>
    [JsonPropertyName("eTag")]
    public string ETag { get; init; } = string.Empty;

    /// <summary>
    /// Content type of the object.
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// Object version ID (if versioning enabled).
    /// </summary>
    [JsonPropertyName("versionId")]
    public string? VersionId { get; init; }

    /// <summary>
    /// Sequencer for event ordering.
    /// </summary>
    [JsonPropertyName("sequencer")]
    public string Sequencer { get; init; } = string.Empty;
}

/// <summary>
/// Identity information in MinIO event.
/// </summary>
public sealed class MinioIdentity
{
    /// <summary>
    /// Principal ID.
    /// </summary>
    [JsonPropertyName("principalId")]
    public string PrincipalId { get; init; } = string.Empty;
}
