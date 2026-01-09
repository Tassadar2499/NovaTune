# 5. Track Document Updates

## Track Schema (Relevant Fields)

```csharp
public sealed class Track
{
    public string Id { get; init; }                      // RavenDB doc ID
    public required string TrackId { get; init; }        // ULID (external)
    public required string UserId { get; init; }         // ULID
    public required string ObjectKey { get; init; }      // Audio file in MinIO
    public string? WaveformObjectKey { get; set; }       // Waveform sidecar

    public TrackStatus Status { get; set; }
    public string? FailureReason { get; set; }           // Populated on Failed

    public AudioMetadata? Metadata { get; set; }         // Populated on Ready

    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; set; }     // When processing completed

    // Optimistic concurrency
    public string? ChangeVector { get; set; }
}

public enum TrackStatus
{
    Processing,  // Initial state after upload
    Ready,       // Successfully processed
    Failed,      // Processing failed (unrecoverable)
    Deleted      // Soft-deleted by user
}
```

## Status Transition Rules (NF-6.2)

| From | To | Allowed | Notes |
|------|-----|---------|-------|
| `Processing` | `Ready` | ✓ | Normal success path |
| `Processing` | `Failed` | ✓ | Unrecoverable error |
| `Ready` | `Processing` | ✗ | **Never revert** |
| `Ready` | `Deleted` | ✓ | User deletion |
| `Failed` | `Processing` | ✗ | Requires manual retry flow |
| `Failed` | `Deleted` | ✓ | User cleanup |

## Optimistic Concurrency (NF-6.2)

```csharp
// In AudioProcessorHandler
var track = await session.LoadAsync<Track>(trackId);
if (track == null || track.Status != TrackStatus.Processing)
{
    // Already processed or deleted - skip
    return;
}

track.Metadata = extractedMetadata;
track.WaveformObjectKey = waveformKey;
track.Status = TrackStatus.Ready;
track.ProcessedAt = DateTimeOffset.UtcNow;

// RavenDB will throw ConcurrencyException if ChangeVector mismatches
await session.SaveChangesAsync();
```

## Merge Policy

- **Worker wins** for: `Metadata`, `WaveformObjectKey`, `Status`, `ProcessedAt`, `FailureReason`
- **User wins** for: `Title`, `Artist` (via separate edit endpoint)
