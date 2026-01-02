# Req 2.x — Track Upload (create + store audio + metadata record)

- **Req 2.1** The system shall allow a Listener to initiate an upload for an audio file by returning a presigned MinIO URL for direct upload (PUT/POST).
- **Req 2.2** The system shall validate supported audio formats using MIME type and reject unsupported formats (exact supported types TBD/configured).
- **Req 2.3** The system shall store uploaded audio content in MinIO and associate it with a stable `ObjectKey` that is guess-resistant and user-scoped.
- **Req 2.4** The system shall create a track metadata record in RavenDB only after the upload succeeds.
- **Req 2.5** The system shall set new tracks to `Status=Processing` by default.
- **Req 2.6** The system shall publish an `AudioUploadedEvent` exactly-once after successful upload including:
  - `TrackId`, `UserId`, `ObjectKey`, `MimeType`, `FileSizeBytes`, `CorrelationId`, `Timestamp`, `SchemaVersion`.

## Clarifications

- Upload completion source of truth is MinIO event notification (not a client callback).
- Upload initiation must create enough server-side state to correlate the MinIO completion notification back to the initiating user and object key.
- Deduplication: compute checksum after upload completion (checksum algorithm and comparison rules TBD).
- Limits/quotas: enforce max upload size, max duration, and per-user quotas (storage, track count); values TBD/configurable.
- Exactly-once event publication (`AudioUploadedEvent`) requires durable semantics (e.g., outbox); concrete mechanism is TBD.
