# Stage 2 — Upload Initiation + MinIO Notification Ingestion

**Goal:** Allow direct-to-MinIO uploads with MinIO as the source of truth for completion.

## API Endpoint

- `POST /tracks/upload/initiate` returns presigned upload URL + upload metadata (Req 2.1).
- Validate supported MIME types and file size limits (Req 2.2, `NF-2.4`).

## Server-side Correlation State

Required per Req 2 clarifications:

- Create an `UploadSession` (or equivalent) document in RavenDB containing:
  - `UploadId`
  - `UserId`
  - Reserved `TrackId` (ULID)
  - `ObjectKey`
  - Expected `MimeType`
  - Max allowed size
  - Creation time
  - Expiry
- Do not create the Track record until MinIO confirms upload completion (`Req 2.4`).

## MinIO Integration

- Configure bucket notifications to Redpanda (or a worker-consumable queue).
- Implement a dedicated worker that consumes object-created events, validates them against the `UploadSession`, and then:
  - Creates the `Track` record (`Status=Processing`, `ObjectKey`, etc.).
  - Publishes `AudioUploadedEvent` durably (`Req 2.6`).

## Event Publication Durability

- Implement an outbox in RavenDB for "must publish" events (`NF-5.2`, `Req 2.6` exactly-once intent).

## Requirements Covered

- `Req 2.x`
- `NF-5.2`
- `NF-6.2`
- `NF-1.4`
