# Stage 3 — Post-Upload Audio Processing Worker (ffprobe/ffmpeg)

**Goal:** Process uploaded tracks asynchronously and transition to `Ready`.

## Event Consumption

- Consume `AudioUploadedEvent` (partition key: `TrackId`; `Req 9.5`).
- Implement bounded concurrency and retries with DLQ semantics (`Req 3.5`, `NF-2.1`, open items).

## Processing Pipeline

1. Fetch audio from MinIO by `ObjectKey`.
2. Use `ffprobe` to extract technical metadata and duration (`Req 3.3`).
3. Generate waveform sidecar object (store in MinIO; `Req 3 clarifications`).
4. Persist results to RavenDB with optimistic concurrency and monotonic state transitions (`NF-6.2`).

## Status Transitions

- Mark track `Ready` or `Failed` (`Req 3.4`).

## Requirements Covered

- `Req 3.x`
- `NF-2.1`
- `NF-6.2`
- `NF-4.2`
