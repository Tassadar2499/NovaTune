# Req 4.x — Storage Access & Lifecycle

- **Req 4.1** The system shall support reading/writing audio objects in MinIO and managing object lifecycle (delete/versioning/lifecycle rules).
- **Req 4.2** The system shall generate short-lived presigned MinIO URLs for upload and streaming, and cache them in Garnet/Redis with a TTL.
- **Req 4.3** Presigned URLs shall be scoped to the requesting user and track (no cross-user access).
- **Req 4.4** On track deletion, the system shall:
  - Invalidate cached presigned URLs for that user+track.
  - Schedule deletion of storage objects after a grace period.

## Clarifications

- Presigned URL TTL values (upload and streaming) are TBD/configurable.
- Cached URL keying: user+track is sufficient (variants/format/version keying is TBD).
- Deletion grace period: 30 days; users can undo/restore a deletion within the grace window.
