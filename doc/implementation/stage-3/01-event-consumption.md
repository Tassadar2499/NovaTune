# 1. Event Consumption

## Topic Configuration

- **Topic**: `{env}-audio-events` (e.g., `dev-audio-events`)
- **Consumer Group**: `audio-processor-worker`
- **Partition Key**: `TrackId` (ensures ordered processing per track)

## Event Schema: `AudioUploadedEvent` (Req 2.6)

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

## Consumer Configuration (NF-2.1)

| Setting | Value | Notes |
|---------|-------|-------|
| `MaxConcurrency` | 4 (configurable) | Bounded to control resource usage |
| `MaxRetries` | 3 | Per-message retry before DLQ |
| `RetryBackoff` | 1s, 5s, 30s | Exponential backoff |
| `CommitInterval` | 5s | Batch offset commits |
| `SessionTimeout` | 30s | Consumer heartbeat timeout |
