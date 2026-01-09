# 6. Error Handling

## Error Classification

| Error Type | Action | Retry? | DLQ? |
|------------|--------|--------|------|
| Track not found | Log error, ack | No | No |
| Already processed | Log warning, ack | No | No |
| Duration exceeded | Mark Failed, ack | No | No |
| Corrupted file | Mark Failed, ack | No | No |
| ffprobe timeout | Mark Failed, ack | No | No |
| MinIO unavailable | Retry with backoff | Yes (3x) | Yes |
| RavenDB unavailable | Retry with backoff | Yes (3x) | Yes |
| Concurrency conflict | Reload and retry | Yes (3x) | Yes |
| Unknown exception | Log, retry | Yes (3x) | Yes |

## Dead Letter Queue (DLQ)

- **Topic**: `{env}-audio-events-dlq`
- **Retention**: 7 days (for investigation)
- **Alert**: Trigger alert when DLQ message count > 0

## DLQ Message Schema

```csharp
public record DlqMessage
{
    public required string OriginalTopic { get; init; }
    public required string OriginalKey { get; init; }
    public required string OriginalPayload { get; init; }
    public required string ErrorMessage { get; init; }
    public required string ErrorStackTrace { get; init; }
    public required int RetryCount { get; init; }
    public required DateTimeOffset FailedAt { get; init; }
}
```
