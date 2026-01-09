# 13. Implementation Tasks

## Worker Project

- [ ] Create `NovaTuneApp.Workers.AudioProcessor` project
- [ ] Add Kafka consumer for `{env}-audio-events`
- [ ] Implement `AudioProcessorHandler` with processing flow
- [ ] Add `FfprobeService` for metadata extraction
- [ ] Add `WaveformService` for peak generation
- [ ] Add temp file management with cleanup
- [ ] Add health checks (Redpanda, RavenDB, MinIO, ffprobe, ffmpeg)

## Models

- [ ] Add `AudioMetadata` record to domain models
- [ ] Add `TrackStatus` enum transitions validation
- [ ] Add `ProcessingFailureReason` constants
- [ ] Add `DlqMessage` record

## Infrastructure

- [ ] Configure `{env}-audio-events-dlq` topic
- [ ] Add ffprobe/ffmpeg to Docker images
- [ ] Configure temp storage volume in Kubernetes/Docker

## Observability

- [ ] Add structured logging with correlation ID propagation
- [ ] Add Prometheus metrics
- [ ] Add OpenTelemetry tracing spans
- [ ] Create Grafana dashboard for processing metrics
- [ ] Configure alerts for DLQ and processing failures

## Testing

- [ ] Unit tests for ffprobe output parsing
- [ ] Unit tests for metadata validation rules
- [ ] Unit tests for status transition logic
- [ ] Unit tests for idempotency scenarios
- [ ] Unit tests for waveform generation
- [ ] Unit tests for DLQ message construction
- [ ] Unit tests for temp file cleanup
