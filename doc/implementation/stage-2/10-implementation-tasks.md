# 10. Implementation Tasks

## API Service

- [ ] Add `POST /tracks/upload/initiate` endpoint
- [ ] Add `UploadSession` document and RavenDB index
- [ ] Add `UploadSessionService` with quota checks
- [ ] Add MinIO presigned URL generation
- [ ] Add rate limiting policy `upload-initiate`
- [ ] Add health check for MinIO connectivity
- [ ] Migrate `AudioUploadedEvent` to ULID strings

## Worker Project

- [ ] Create `NovaTuneApp.Workers.UploadIngestor` project
- [ ] Add Kafka consumer for `{env}-minio-events`
- [ ] Implement notification validation logic
- [ ] Add Track creation with outbox write
- [ ] Add checksum computation
- [ ] Add health checks (Redpanda, RavenDB, MinIO)

## Infrastructure

- [ ] Add MinIO bucket notification configuration to AppHost
- [ ] Add `{env}-minio-events` topic to Redpanda setup
- [ ] Add `OutboxMessage` document and background processor
- [ ] Add UploadSession cleanup background job

## Testing

- [ ] Unit tests for validation and quota logic
