# Stage 2 — Upload Initiation + MinIO Notification Ingestion

**Goal:** Allow direct-to-MinIO uploads with MinIO as the source of truth for completion.

## Overview

```
┌─────────┐  POST /tracks/upload/initiate   ┌─────────────┐
│ Client  │ ───────────────────────────────►│ API Service │
└────┬────┘ ◄─────────────────────────────── └──────┬──────┘
     │       presigned URL + UploadSession          │
     │                                              │ Create UploadSession
     │                                              ▼
     │       PUT (presigned)              ┌─────────────────┐
     │ ──────────────────────────────────►│     MinIO       │
     │                                    └────────┬────────┘
     │                                             │ Bucket notification
     │                                             ▼
     │                                    ┌─────────────────┐
     │                                    │   Redpanda      │
     │                                    │ (minio-events)  │
     │                                    └────────┬────────┘
     │                                             │
     │                                             ▼
     │                                    ┌─────────────────┐
     │                                    │ Upload Ingestor │
     │                                    │    Worker       │
     │                                    └────────┬────────┘
     │                                             │ 1. Validate UploadSession
     │                                             │ 2. Create Track record
     │                                             │ 3. Publish AudioUploadedEvent (via outbox)
     │                                             ▼
     │                                    ┌─────────────────┐
     │                                    │    RavenDB      │
     │                                    └─────────────────┘
```

---

## 1. API Endpoint: `POST /tracks/upload/initiate`

### Request Schema

```json
{
  "fileName": "my-track.mp3",        // Required; used for title default
  "mimeType": "audio/mpeg",          // Required; validated against allow-list
  "fileSizeBytes": 15728640,         // Required; validated against max size
  "title": "My Track",               // Optional; defaults to fileName sans extension
  "artist": "Artist Name"            // Optional
}
```

### Response Schema (Success: 200 OK)

```json
{
  "uploadId": "01HXK...",            // ULID
  "trackId": "01HXK...",             // ULID (reserved, Track record not yet created)
  "presignedUrl": "https://...",     // PUT URL; expires in ~15 min
  "expiresAt": "2025-01-08T12:30:00Z",
  "objectKey": "audio/{userId}/{trackId}/{randomSuffix}"  // For reference only
}
```

### Validation Rules (Req 2.2, NF-2.4)

| Field | Rule | Error Code |
|-------|------|------------|
| `mimeType` | Must be in allowed list (configurable) | `UNSUPPORTED_MIME_TYPE` |
| `fileSizeBytes` | ≤ `MaxUploadSizeBytes` (default: 100 MB) | `FILE_TOO_LARGE` |
| `fileSizeBytes` | User quota not exceeded | `QUOTA_EXCEEDED` |
| `fileName` | Non-empty, ≤ 255 chars | `INVALID_FILE_NAME` |

**Supported MIME types** (initial, configurable via `appsettings`):
- `audio/mpeg` (.mp3)
- `audio/mp4` (.m4a)
- `audio/flac` (.flac)
- `audio/wav`, `audio/x-wav` (.wav)
- `audio/ogg` (.ogg)

### Rate Limiting (Req 8.2, NF-2.5)

- Policy: `upload-initiate`
- Default: 10 requests/minute per user
- Response on limit: `429 Too Many Requests` with `Retry-After` header

### Error Responses (RFC 7807)

```json
{
  "type": "https://novatune.dev/errors/quota-exceeded",
  "title": "Storage quota exceeded",
  "status": 400,
  "detail": "You have used 950 MB of your 1 GB storage quota.",
  "instance": "/tracks/upload/initiate",
  "extensions": {
    "usedBytes": 996147200,
    "quotaBytes": 1073741824
  }
}
```

---

## 2. Server-Side Correlation State: UploadSession

### Document Schema (RavenDB)

```csharp
public sealed class UploadSession
{
    public string Id { get; init; }                  // RavenDB doc ID (internal)
    public required string UploadId { get; init; }   // ULID (external identifier)
    public required string UserId { get; init; }     // ULID
    public required string ReservedTrackId { get; init; } // ULID (pre-allocated)
    public required string ObjectKey { get; init; }  // MinIO object key
    public required string ExpectedMimeType { get; init; }
    public required long MaxAllowedSizeBytes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Pending;

    // Optional: client-provided metadata to carry forward to Track
    public string? Title { get; init; }
    public string? Artist { get; init; }
}

public enum UploadSessionStatus
{
    Pending,    // Awaiting upload
    Completed,  // MinIO notification received, Track created
    Expired,    // TTL passed without completion
    Failed      // Validation failed on notification
}
```

### ObjectKey Generation (Req 2.3)

Format: `audio/{userId}/{trackId}/{randomSuffix}`

- `randomSuffix`: 16-byte base64url-encoded value for guess-resistance
- Example: `audio/01HXK.../01HXK.../a1B2c3D4e5F6g7H8`

### Session Expiry & Cleanup

- Default TTL: 15 minutes (configurable)
- **Cleanup job**: Background task marks expired sessions as `Expired` and deletes them after 24 hours
- RavenDB index: `UploadSessions_ByStatusAndExpiry` for efficient queries

---

## 3. MinIO Integration

### Bucket Configuration

- Bucket name: `{env}-audio-uploads` (e.g., `dev-audio-uploads`)
- Enable versioning (for NF-6.5 DR requirements)
- Configure lifecycle policy to abort incomplete multipart uploads after 24 hours

### Bucket Notification → Redpanda

Configure MinIO to publish `s3:ObjectCreated:*` events to Redpanda:

```bash
mc admin config set myminio notify_kafka:novatune \
  brokers="redpanda:9092" \
  topic="{env}-minio-events" \
  queue_dir="/tmp/minio/events"

mc event add myminio/{env}-audio-uploads arn:minio:sqs::novatune:kafka \
  --event put --prefix "audio/"
```

### Event Payload (MinIO → Redpanda)

```json
{
  "EventName": "s3:ObjectCreated:Put",
  "Key": "audio/01HXK.../01HXK.../a1B2c3D4e5F6g7H8",
  "Records": [{
    "s3": {
      "bucket": { "name": "dev-audio-uploads" },
      "object": {
        "key": "audio/...",
        "size": 15728640,
        "contentType": "audio/mpeg",
        "eTag": "abc123..."
      }
    }
  }]
}
```

---

## 4. Upload Ingestor Worker

### Project: `NovaTuneApp.Workers.UploadIngestor`

Separate deployment per NF-1.1. Consumes from `{env}-minio-events` topic.

### Processing Flow

```
1. Receive MinIO notification
2. Extract ObjectKey from event
3. Parse userId and trackId from ObjectKey
4. Load UploadSession by ObjectKey
   └─ If not found → log warning, ack message (orphan upload)
   └─ If expired → log warning, mark Failed, ack message
5. Validate:
   - Content-Type matches ExpectedMimeType
   - Size ≤ MaxAllowedSizeBytes
   └─ If invalid → mark session Failed, delete object, ack message
6. Begin RavenDB transaction:
   a. Create Track record (Status=Processing)
   b. Update UploadSession (Status=Completed)
   c. Insert OutboxMessage for AudioUploadedEvent
   d. SaveChanges (single transaction)
7. Ack Kafka message
```

### Checksum Computation (Req 2 clarifications)

After validation, compute checksum for deduplication:
- Algorithm: SHA-256 (configurable)
- Store in Track.Checksum
- **Deduplication logic** (TBD): Check for existing Track with same UserId + Checksum; if found, consider marking as duplicate

### Error Handling

| Scenario | Action |
|----------|--------|
| UploadSession not found | Log warning, ack (no retry) |
| UploadSession expired | Log, mark Failed, ack |
| Validation failure | Log, mark Failed, delete MinIO object, ack |
| RavenDB unavailable | Retry with exponential backoff (max 3 attempts), then DLQ |
| Outbox write fails | Included in RavenDB transaction; rolls back together |

### Health Check (NF-1.2)

Readiness requires:
- Redpanda connectivity
- RavenDB connectivity
- MinIO connectivity (for object deletion on validation failure)

---

## 5. Event Publication: Outbox Pattern (NF-5.2, Req 2.6)

### Outbox Document Schema

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

### Outbox Processor (Background Service)

- Polls `OutboxMessages` with `Status=Pending` every 1 second
- Publishes to Redpanda topic `{env}-audio-events`
- On success: Update `Status=Published`, set `ProcessedAt`
- On failure: Increment `RetryCount`, exponential backoff
- After 5 failures: `Status=Failed`, alert via logging

### AudioUploadedEvent Schema (Req 2.6)

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

---

## 6. Quota Enforcement (Req 2 clarifications, NF-2.4)

### Per-User Quotas (Configurable)

| Quota | Default | Enforcement Point |
|-------|---------|-------------------|
| `MaxStorageBytes` | 1 GB | Upload initiation |
| `MaxTrackCount` | 500 | Upload initiation |
| `MaxFileSizeBytes` | 100 MB | Upload initiation + worker validation |

### Storage Tracking

- Maintain `User.UsedStorageBytes` aggregate in RavenDB
- Increment on Track creation (worker)
- Decrement on Track physical deletion (lifecycle worker)
- Use optimistic concurrency for updates

---

## 7. Observability (NF-4.x)

### Logging

| Event | Level | Fields |
|-------|-------|--------|
| Upload initiated | Info | `UserId`, `UploadId`, `TrackId`, `MimeType`, `FileSize` |
| MinIO notification received | Debug | `ObjectKey`, `Size` |
| Session not found | Warning | `ObjectKey` |
| Validation failed | Warning | `UploadId`, `Reason` |
| Track created | Info | `TrackId`, `UserId`, `ObjectKey` |
| Outbox published | Debug | `EventType`, `TrackId` |

**Redaction (NF-4.5):** Never log presigned URLs or full object keys in production.

### Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `upload_initiate_total` | Counter | `status` (success/error) |
| `upload_initiate_duration_ms` | Histogram | |
| `upload_session_created_total` | Counter | |
| `minio_notification_received_total` | Counter | |
| `track_created_total` | Counter | |
| `outbox_published_total` | Counter | `event_type` |
| `outbox_failed_total` | Counter | `event_type` |

### Tracing

- Propagate `CorrelationId` from API → UploadSession → Worker → AudioUploadedEvent
- Use `traceparent` header for distributed trace continuity

---

## 8. Resilience (NF-1.4)

### Timeouts

| Operation | Timeout | Retries |
|-----------|---------|---------|
| MinIO presign generation | 5s | 1 |
| RavenDB read (UploadSession) | 2s | 1 |
| RavenDB write (Track + Outbox) | 5s | 0 (idempotent via outbox) |
| Redpanda produce (outbox) | 2s | 2 |

### Circuit Breaker

- MinIO: Open after 5 consecutive failures, half-open after 30s
- RavenDB: Open after 5 consecutive failures, half-open after 30s

### Fail-Closed Behavior

Upload initiation fails closed:
- If MinIO unavailable → `503 Service Unavailable`
- If RavenDB unavailable → `503 Service Unavailable`
- If quota check fails → `503 Service Unavailable` (not silent pass)

---

## 9. Test Strategy

### Unit Tests

- UploadSession expiry logic
- ObjectKey generation format
- Quota calculation
- MIME type validation
- Checksum computation

---

## 10. Implementation Tasks

### API Service

- [ ] Add `POST /tracks/upload/initiate` endpoint
- [ ] Add `UploadSession` document and RavenDB index
- [ ] Add `UploadSessionService` with quota checks
- [ ] Add MinIO presigned URL generation
- [ ] Add rate limiting policy `upload-initiate`
- [ ] Add health check for MinIO connectivity
- [ ] Migrate `AudioUploadedEvent` to ULID strings

### Worker Project

- [ ] Create `NovaTuneApp.Workers.UploadIngestor` project
- [ ] Add Kafka consumer for `{env}-minio-events`
- [ ] Implement notification validation logic
- [ ] Add Track creation with outbox write
- [ ] Add checksum computation
- [ ] Add health checks (Redpanda, RavenDB, MinIO)

### Infrastructure

- [ ] Add MinIO bucket notification configuration to AppHost
- [ ] Add `{env}-minio-events` topic to Redpanda setup
- [ ] Add `OutboxMessage` document and background processor
- [ ] Add UploadSession cleanup background job

### Testing

- [ ] Unit tests for validation and quota logic

---

## Requirements Covered

- `Req 2.1` — Presigned URL for direct upload
- `Req 2.2` — MIME type validation
- `Req 2.3` — Guess-resistant, user-scoped ObjectKey
- `Req 2.4` — Track created only after MinIO confirms upload
- `Req 2.5` — New tracks start with `Status=Processing`
- `Req 2.6` — `AudioUploadedEvent` published exactly-once (via outbox)
- `NF-1.4` — Resilience (timeouts, retries, circuit breakers)
- `NF-2.4` — Quota enforcement
- `NF-2.5` — Rate limiting
- `NF-5.2` — Idempotent processing, outbox for durability
- `NF-6.2` — Optimistic concurrency, monotonic state transitions

---

## Open Items

- [ ] Finalize supported MIME types list
- [ ] Define deduplication behavior when checksum matches existing track
- [ ] Determine max upload duration limit (needs ffprobe, may move to Stage 3)
- [ ] Define alerting thresholds for outbox failures
