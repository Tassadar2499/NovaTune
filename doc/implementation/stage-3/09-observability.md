# 9. Observability (NF-4.x)

## Logging

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

## Metrics

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

## Tracing

- Propagate `CorrelationId` from `AudioUploadedEvent` to all logs and spans
- Create spans for:
  - `audio.download` (MinIO fetch)
  - `audio.ffprobe` (metadata extraction)
  - `audio.waveform` (waveform generation)
  - `audio.persist` (RavenDB update)

## SLO Targets (NF-4.4)

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Processing success rate | ≥ 99% | < 95% over 15 min |
| p95 processing time | < 60s | > 120s |
| Consumer lag | < 100 messages | > 1000 messages |
| DLQ rate | 0 | > 0 per hour |
