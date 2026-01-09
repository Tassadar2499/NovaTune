# Stage 3 — Post-Upload Audio Processing Worker (ffprobe/ffmpeg)

**Goal:** Process uploaded tracks asynchronously, extract metadata, generate waveforms, and transition to `Ready`.

## Overview

```
┌─────────────────┐                           ┌─────────────────┐
│    RavenDB      │                           │     MinIO       │
│  (Track record) │                           │  (audio files)  │
└────────┬────────┘                           └────────┬────────┘
         │                                             │
         │ Update Track                                │ Fetch audio
         │ (metadata + status)                         │ Store waveform
         │                                             │
         ▼                                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Audio Processing Worker                       │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐  │
│  │   ffprobe   │───►│  Metadata   │───►│  Waveform Generator │  │
│  │  (extract)  │    │  Validator  │    │      (ffmpeg)       │  │
│  └─────────────┘    └─────────────┘    └─────────────────────┘  │
└────────────────────────────────────────────────────────────────┬┘
                                                                 │
                          ▲                                      │
                          │ Consume AudioUploadedEvent           │
                          │                                      │
┌─────────────────────────┴───────────────────────────────────────┘
│                     Redpanda
│                 ({env}-audio-events)
└─────────────────────────────────────────────────────────────────┘
```

---

## 1. Event Consumption

### Topic Configuration

- **Topic**: `{env}-audio-events` (e.g., `dev-audio-events`)
- **Consumer Group**: `audio-processor-worker`
- **Partition Key**: `TrackId` (ensures ordered processing per track)

### Event Schema: `AudioUploadedEvent` (Req 2.6)

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

### Consumer Configuration (NF-2.1)

| Setting | Value | Notes |
|---------|-------|-------|
| `MaxConcurrency` | 4 (configurable) | Bounded to control resource usage |
| `MaxRetries` | 3 | Per-message retry before DLQ |
| `RetryBackoff` | 1s, 5s, 30s | Exponential backoff |
| `CommitInterval` | 5s | Batch offset commits |
| `SessionTimeout` | 30s | Consumer heartbeat timeout |

---

## 2. Processing Pipeline

### Project: `NovaTuneApp.Workers.AudioProcessor`

Separate deployment per NF-1.1. Consumes from `{env}-audio-events` topic.

### Processing Flow

```
1. Receive AudioUploadedEvent
2. Load Track record from RavenDB by TrackId
   └─ If not found → log error, ack message (orphan event)
   └─ If Status != Processing → log warning, ack message (already processed)
3. Download audio from MinIO to temp storage
   └─ Use streaming IO to avoid unbounded memory (NF-2.4)
4. Run ffprobe to extract metadata
   └─ Validate duration ≤ MaxTrackDuration
   └─ If invalid → mark Track Failed, ack message
5. Generate waveform data using ffmpeg
   └─ Store as sidecar object in MinIO
6. Begin RavenDB transaction:
   a. Update Track with AudioMetadata
   b. Update Track.WaveformObjectKey
   c. Update Track.Status = Ready
   d. Update Track.ProcessedAt timestamp
   e. SaveChanges with optimistic concurrency
7. Clean up temp files
8. Ack Kafka message
```

### Temp Storage Strategy

- Directory: `/tmp/novatune-processing/{TrackId}/`
- Cleanup: Always in `finally` block, regardless of success/failure
- Max concurrent temp files: Limited by `MaxConcurrency` setting

---

## 3. Metadata Extraction (ffprobe)

### ffprobe Command

```bash
ffprobe -v quiet -print_format json -show_format -show_streams "{input_file}"
```

### AudioMetadata Schema

```csharp
public sealed class AudioMetadata
{
    public required TimeSpan Duration { get; init; }
    public required int SampleRate { get; init; }        // e.g., 44100, 48000
    public required int Channels { get; init; }          // 1 = mono, 2 = stereo
    public required int BitRate { get; init; }           // bits per second
    public required string Codec { get; init; }          // e.g., "mp3", "flac", "aac"
    public required string CodecLongName { get; init; }  // e.g., "MP3 (MPEG audio layer 3)"
    public int? BitDepth { get; init; }                  // For lossless formats (16, 24, 32)

    // Embedded metadata (optional, extracted from tags)
    public string? EmbeddedTitle { get; init; }
    public string? EmbeddedArtist { get; init; }
    public string? EmbeddedAlbum { get; init; }
    public int? EmbeddedYear { get; init; }
    public string? EmbeddedGenre { get; init; }
}
```

### Validation Rules (NF-2.4)

| Field | Rule | Action on Failure |
|-------|------|-------------------|
| `Duration` | ≤ `MaxTrackDuration` (default: 2 hours) | Mark Track `Failed` |
| `Duration` | > 0 | Mark Track `Failed` |
| `SampleRate` | > 0 | Mark Track `Failed` |
| `Channels` | 1–8 | Mark Track `Failed` |
| Codec | Recognized audio codec | Mark Track `Failed` |

### Failure Reasons

```csharp
public static class ProcessingFailureReason
{
    public const string DurationExceeded = "DURATION_EXCEEDED";
    public const string InvalidDuration = "INVALID_DURATION";
    public const string UnsupportedCodec = "UNSUPPORTED_CODEC";
    public const string CorruptedFile = "CORRUPTED_FILE";
    public const string FfprobeTimeout = "FFPROBE_TIMEOUT";
    public const string FfmpegTimeout = "FFMPEG_TIMEOUT";
    public const string StorageError = "STORAGE_ERROR";
    public const string UnknownError = "UNKNOWN_ERROR";
}
```

---

## 4. Waveform Generation (ffmpeg)

### Waveform Data Format

Generate peak data for visualization (not audio playback):

```bash
ffmpeg -i "{input_file}" -ac 1 -filter:a "aresample=8000,asetnsamples=n=1000" \
  -f wav -acodec pcm_s16le "{output_file}"
```

**Alternative: JSON peaks format** (recommended for smaller size):

```bash
ffmpeg -i "{input_file}" -ac 1 -filter:a "aresample=8000" \
  -f lavfi -i "sine=frequency=1:duration=0.001" \
  -filter_complex "[0:a]astats=metadata=1:reset=1,ametadata=print:key=lavfi.astats.Overall.Peak_level:file=-" \
  -f null -
```

### Waveform Storage

- **Object Key**: `waveforms/{userId}/{trackId}/peaks.json`
- **Content-Type**: `application/json`
- **Compression**: gzip (optional, configurable)
- **Max Size**: 100 KB (truncate if larger)

### Waveform JSON Schema

```json
{
  "version": 1,
  "sampleRate": 8000,
  "samplesPerPeak": 441,
  "peaks": [0.12, 0.45, 0.78, 0.32, ...]  // Normalized 0-1 values
}
```

---

## 5. Track Document Updates

### Track Schema (Relevant Fields)

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

### Status Transition Rules (NF-6.2)

| From | To | Allowed | Notes |
|------|-----|---------|-------|
| `Processing` | `Ready` | ✓ | Normal success path |
| `Processing` | `Failed` | ✓ | Unrecoverable error |
| `Ready` | `Processing` | ✗ | **Never revert** |
| `Ready` | `Deleted` | ✓ | User deletion |
| `Failed` | `Processing` | ✗ | Requires manual retry flow |
| `Failed` | `Deleted` | ✓ | User cleanup |

### Optimistic Concurrency (NF-6.2)

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

### Merge Policy

- **Worker wins** for: `Metadata`, `WaveformObjectKey`, `Status`, `ProcessedAt`, `FailureReason`
- **User wins** for: `Title`, `Artist` (via separate edit endpoint)

---

## 6. Error Handling

### Error Classification

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

### Dead Letter Queue (DLQ)

- **Topic**: `{env}-audio-events-dlq`
- **Retention**: 7 days (for investigation)
- **Alert**: Trigger alert when DLQ message count > 0

### DLQ Message Schema

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

---

## 7. Idempotency (Req 3.5)

### Guarantees

1. **Status check**: Skip processing if `Status != Processing`
2. **Overwrite safe**: Re-extracting metadata and regenerating waveform overwrites previous values safely
3. **Optimistic concurrency**: Prevents lost updates from concurrent processing

### Replay Scenarios

| Scenario | Behavior |
|----------|----------|
| Event replayed, track `Ready` | Skip (no-op) |
| Event replayed, track `Failed` | Skip (no-op, requires manual intervention) |
| Event replayed, track `Processing` | Reprocess (safe) |
| Event replayed, track `Deleted` | Skip (no-op) |

---

## 8. Health Checks (NF-1.2)

### Readiness Requirements

- Redpanda connectivity (consumer can connect)
- RavenDB connectivity (can execute queries)
- MinIO connectivity (can list buckets)
- ffprobe available (`which ffprobe` succeeds)
- ffmpeg available (`which ffmpeg` succeeds)
- Temp directory writable

### Health Endpoint

```
GET /health      → 200 OK if all checks pass
GET /health/live → 200 OK if process is running
```

---

## 9. Observability (NF-4.x)

### Logging

| Event | Level | Fields |
|-------|-------|--------|
| Processing started | Info | `TrackId`, `UserId`, `CorrelationId` |
| Track not found | Error | `TrackId`, `CorrelationId` |
| Already processed | Warning | `TrackId`, `Status`, `CorrelationId` |
| Metadata extracted | Debug | `TrackId`, `Duration`, `Codec`, `SampleRate` |
| Waveform generated | Debug | `TrackId`, `WaveformSize` |
| Processing succeeded | Info | `TrackId`, `DurationMs`, `CorrelationId` |
| Processing failed | Warning | `TrackId`, `FailureReason`, `CorrelationId` |
| Retry attempted | Warning | `TrackId`, `RetryCount`, `ErrorMessage` |
| DLQ message sent | Error | `TrackId`, `ErrorMessage`, `CorrelationId` |

**Redaction (NF-4.5):** Never log full `ObjectKey` or file paths containing user IDs.

### Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `audio_processing_received_total` | Counter | |
| `audio_processing_success_total` | Counter | |
| `audio_processing_failed_total` | Counter | `reason` |
| `audio_processing_skipped_total` | Counter | `reason` (already_processed, not_found) |
| `audio_processing_duration_seconds` | Histogram | `stage` (download, ffprobe, waveform, persist) |
| `audio_processing_retries_total` | Counter | |
| `audio_processing_dlq_total` | Counter | |
| `audio_track_duration_seconds` | Histogram | | (track audio duration) |
| `kafka_consumer_lag` | Gauge | `topic`, `partition` |

### Tracing

- Propagate `CorrelationId` from `AudioUploadedEvent` to all logs and spans
- Create spans for:
  - `audio.download` (MinIO fetch)
  - `audio.ffprobe` (metadata extraction)
  - `audio.waveform` (waveform generation)
  - `audio.persist` (RavenDB update)

### SLO Targets (NF-4.4)

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Processing success rate | ≥ 99% | < 95% over 15 min |
| p95 processing time | < 60s | > 120s |
| Consumer lag | < 100 messages | > 1000 messages |
| DLQ rate | 0 | > 0 per hour |

---

## 10. Resilience (NF-1.4)

### Timeouts

| Operation | Timeout | Notes |
|-----------|---------|-------|
| MinIO download | 5 min | Large files up to 500 MB |
| ffprobe execution | 30s | Should complete quickly |
| ffmpeg waveform | 2 min | CPU-bound |
| RavenDB read | 5s | |
| RavenDB write | 10s | |
| Total processing | 10 min | Hard limit per track |

### Circuit Breaker

- **MinIO**: Open after 5 consecutive failures, half-open after 30s
- **RavenDB**: Open after 5 consecutive failures, half-open after 30s

### Resource Limits

| Resource | Limit | Notes |
|----------|-------|-------|
| Temp disk space | 2 GB | Fail fast if exceeded |
| Memory per process | 512 MB soft limit | Streaming IO prevents spikes |
| Concurrent ffmpeg | = `MaxConcurrency` | Bound by consumer concurrency |

### Graceful Shutdown

1. Stop accepting new messages
2. Wait for in-flight processing to complete (timeout: 60s)
3. Commit final offsets
4. Clean up temp files
5. Exit

---

## 11. Configuration

### appsettings.json

```json
{
  "AudioProcessor": {
    "MaxConcurrency": 4,
    "MaxRetries": 3,
    "RetryBackoffMs": [1000, 5000, 30000],
    "MaxTrackDurationMinutes": 120,
    "TempDirectory": "/tmp/novatune-processing",
    "MaxTempDiskSpaceMb": 2048,
    "FfprobeTimeoutSeconds": 30,
    "FfmpegTimeoutSeconds": 120,
    "TotalProcessingTimeoutMinutes": 10,
    "WaveformPeakCount": 1000,
    "WaveformCompressionEnabled": true
  },
  "Kafka": {
    "ConsumerGroup": "audio-processor-worker",
    "Topics": {
      "AudioEvents": "{env}-audio-events",
      "AudioEventsDlq": "{env}-audio-events-dlq"
    }
  }
}
```

---

## 12. Test Strategy

### Unit Tests

| Test Case | Description |
|-----------|-------------|
| ffprobe output parsing | Parse JSON output, handle malformed responses |
| Metadata validation | Duration limits, sample rate, channel count |
| Status transitions | Verify allowed/disallowed state changes |
| Idempotency | Replay scenarios for each track status |
| Waveform generation | Peak calculation, normalization, JSON output |
| DLQ message construction | Schema compliance, field population |
| Failure reason mapping | Exception → failure reason translation |
| Temp file cleanup | Verify cleanup in success/failure paths |

### Test Fixtures

```csharp
public static class TestAudioFiles
{
    public static readonly byte[] ValidMp3 = LoadResource("valid.mp3");
    public static readonly byte[] ValidFlac = LoadResource("valid.flac");
    public static readonly byte[] CorruptedFile = LoadResource("corrupted.bin");
    public static readonly byte[] TooLongTrack = LoadResource("3hour.mp3");
}

public static class FfprobeOutputs
{
    public const string ValidMp3Output = """
        {
          "format": {
            "duration": "180.5",
            "bit_rate": "320000"
          },
          "streams": [{
            "codec_name": "mp3",
            "sample_rate": "44100",
            "channels": 2
          }]
        }
        """;
}
```

---

## 13. Implementation Tasks

### Worker Project

- [ ] Create `NovaTuneApp.Workers.AudioProcessor` project
- [ ] Add Kafka consumer for `{env}-audio-events`
- [ ] Implement `AudioProcessorHandler` with processing flow
- [ ] Add `FfprobeService` for metadata extraction
- [ ] Add `WaveformService` for peak generation
- [ ] Add temp file management with cleanup
- [ ] Add health checks (Redpanda, RavenDB, MinIO, ffprobe, ffmpeg)

### Models

- [ ] Add `AudioMetadata` record to domain models
- [ ] Add `TrackStatus` enum transitions validation
- [ ] Add `ProcessingFailureReason` constants
- [ ] Add `DlqMessage` record

### Infrastructure

- [ ] Configure `{env}-audio-events-dlq` topic
- [ ] Add ffprobe/ffmpeg to Docker images
- [ ] Configure temp storage volume in Kubernetes/Docker

### Observability

- [ ] Add structured logging with correlation ID propagation
- [ ] Add Prometheus metrics
- [ ] Add OpenTelemetry tracing spans
- [ ] Create Grafana dashboard for processing metrics
- [ ] Configure alerts for DLQ and processing failures

### Testing

- [ ] Unit tests for ffprobe output parsing
- [ ] Unit tests for metadata validation rules
- [ ] Unit tests for status transition logic
- [ ] Unit tests for idempotency scenarios
- [ ] Unit tests for waveform generation
- [ ] Unit tests for DLQ message construction
- [ ] Unit tests for temp file cleanup

---

## Requirements Covered

- `Req 3.1` — Asynchronous audio processing (metadata extraction, waveform generation)
- `Req 3.2` — Consume `AudioUploadedEvent` and invoke processing logic
- `Req 3.3` — Extract metadata via ffprobe, compute duration, persist to RavenDB
- `Req 3.4` — Transition track to `Ready` or `Failed`
- `Req 3.5` — Idempotent processing per `TrackId`
- `NF-1.2` — Health checks for worker readiness
- `NF-1.4` — Resilience (timeouts, retries, circuit breakers)
- `NF-2.1` — Horizontal scaling with bounded concurrency
- `NF-2.4` — Bounded memory usage, streaming IO
- `NF-4.1` — Structured logging with `CorrelationId`
- `NF-4.2` — Metrics for consumption lag, success/failure counts
- `NF-4.3` — Traceability across API → event → worker
- `NF-6.2` — Optimistic concurrency, monotonic state transitions

---

## Open Items

- [ ] Finalize waveform format (WAV vs JSON peaks) based on frontend requirements
- [ ] Define manual retry flow for `Failed` tracks (admin endpoint or requeue)
- [ ] Determine if embedded metadata (title/artist) should auto-populate track fields
- [ ] Evaluate ffmpeg alternatives (e.g., audiowaveform library) for performance
- [ ] Define SLO for "time to ready" (upload → ready latency)
- [ ] Configure alert thresholds for `prod` vs `staging`
