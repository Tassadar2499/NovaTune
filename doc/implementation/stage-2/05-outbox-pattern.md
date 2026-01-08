# 5. Event Publication: Outbox Pattern (NF-5.2, Req 2.6)

## Outbox Document Schema

```csharp
public sealed class OutboxMessage
{
    public string Id { get; init; }                    // RavenDB doc ID
    public required string EventType { get; init; }    // e.g., "AudioUploadedEvent"
    public required string Payload { get; init; }      // JSON-serialized event
    public required string PartitionKey { get; init; } // TrackId for ordering
    public required DateTimeOffset CreatedAt { get; init; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum OutboxMessageStatus
{
    Pending,
    Published,
    Failed
}
```

## Outbox Processor (Background Service)

- Polls `OutboxMessages` with `Status=Pending` every 1 second
- Publishes to Redpanda topic `{env}-audio-events`
- On success: Update `Status=Published`, set `ProcessedAt`
- On failure: Increment `RetryCount`, exponential backoff
- After 5 failures: `Status=Failed`, alert via logging

## AudioUploadedEvent Schema (Req 2.6)

**Note:** Migrate from `Guid` to ULID string per cross-cutting decision 3.1.

```csharp
public record AudioUploadedEvent
{
    public int SchemaVersion { get; init; } = 1;
    public required string TrackId { get; init; }       // ULID string
    public required string UserId { get; init; }        // ULID string
    public required string ObjectKey { get; init; }
    public required string MimeType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string Checksum { get; init; }      // SHA-256 hex
    public required string CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
```
