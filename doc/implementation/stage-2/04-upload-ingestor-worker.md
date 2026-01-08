# 4. Upload Ingestor Worker

## Project: `NovaTuneApp.Workers.UploadIngestor`

Separate deployment per NF-1.1. Consumes from `{env}-minio-events` topic.

## Processing Flow

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

## Checksum Computation (Req 2 clarifications)

After validation, compute checksum for deduplication:
- Algorithm: SHA-256 (configurable)
- Store in Track.Checksum
- **Deduplication logic** (TBD): Check for existing Track with same UserId + Checksum; if found, consider marking as duplicate

## Error Handling

| Scenario | Action |
|----------|--------|
| UploadSession not found | Log warning, ack (no retry) |
| UploadSession expired | Log, mark Failed, ack |
| Validation failure | Log, mark Failed, delete MinIO object, ack |
| RavenDB unavailable | Retry with exponential backoff (max 3 attempts), then DLQ |
| Outbox write fails | Included in RavenDB transaction; rolls back together |

## Health Check (NF-1.2)

Readiness requires:
- Redpanda connectivity
- RavenDB connectivity
- MinIO connectivity (for object deletion on validation failure)
