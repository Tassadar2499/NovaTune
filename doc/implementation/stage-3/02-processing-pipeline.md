# 2. Processing Pipeline

## Project: `NovaTuneApp.Workers.AudioProcessor`

Separate deployment per NF-1.1. Consumes from `{env}-audio-events` topic.

## Processing Flow

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

## Temp Storage Strategy

- Directory: `/tmp/novatune-processing/{TrackId}/`
- Cleanup: Always in `finally` block, regardless of success/failure
- Max concurrent temp files: Limited by `MaxConcurrency` setting
