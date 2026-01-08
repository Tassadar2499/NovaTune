# 7. Observability (NF-4.x)

## Logging

| Event | Level | Fields |
|-------|-------|--------|
| Upload initiated | Info | `UserId`, `UploadId`, `TrackId`, `MimeType`, `FileSize` |
| MinIO notification received | Debug | `ObjectKey`, `Size` |
| Session not found | Warning | `ObjectKey` |
| Validation failed | Warning | `UploadId`, `Reason` |
| Track created | Info | `TrackId`, `UserId`, `ObjectKey` |
| Outbox published | Debug | `EventType`, `TrackId` |

**Redaction (NF-4.5):** Never log presigned URLs or full object keys in production.

## Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `upload_initiate_total` | Counter | `status` (success/error) |
| `upload_initiate_duration_ms` | Histogram | |
| `upload_session_created_total` | Counter | |
| `minio_notification_received_total` | Counter | |
| `track_created_total` | Counter | |
| `outbox_published_total` | Counter | `event_type` |
| `outbox_failed_total` | Counter | `event_type` |

## Tracing

- Propagate `CorrelationId` from API → UploadSession → Worker → AudioUploadedEvent
- Use `traceparent` header for distributed trace continuity
