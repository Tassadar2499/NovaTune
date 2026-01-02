# Functional Requirements — Open Questions

This file captures clarifying questions raised while reviewing `doc/requirements/functional/main.md`.

## Cross-cutting

1. **Identifiers**: should external/API identifiers for `UserId`/`TrackId` be `Guid`, Raven-style string IDs (e.g., `tracks/1`), or something else (e.g., ULID)? How should events and API payloads align?
A: Use ULID
2. **Multi-environment naming**: what is the authoritative mapping for `{env}` in topic names (e.g., `dev`, `staging`, `prod`), and should it be required/configured per deployment?
A: Use `{env}` as the authoritative mapping for topic names, and it should be required/configured per deployment.
3. **Consistency model**: are clients expected to see eventual consistency (e.g., track appears immediately as `Processing`, then later becomes `Ready`), or are there flows that must be strongly consistent?
A: Clients should see eventual consistency.
4. **Error contract**: should the API use a standard error shape (e.g., RFC 7807 problem details), and what error codes are expected for common failures (auth, validation, not found, forbidden)?
A: Use RFC 7807 problem details.
5. **Rate limits / abuse**: do we need explicit rate limits for login, upload initiation, playback URL issuance, and telemetry ingestion?
A: Yes, Use explicit rate limits for login, upload initiation, playback URL issuance, and telemetry ingestion.

## Authentication & Authorization (Req 1.x)

1. **Password policy**: required minimum length/complexity, and should breached-password checks be in scope?
A: No required minimum length/complexity,
2. **Password hashing**: preferred algorithm/parameters (Argon2id/bcrypt/PBKDF2), and do we need per-user salt + global pepper?
A: Prefer Argon2id/bcrypt
3. **Email verification**: is email confirmation required before `Status=Active`, and what is the expected flow for resending/expiry?
A: No email confirmation required before `Status=Active`
4. **Refresh tokens**: rotation strategy (one-time use vs reusable), TTLs, max concurrent sessions/devices per user, and whether refresh tokens are stored hashed in RavenDB vs cached in Garnet.
A: Use one-time use rotation strategy, TTLs of 1 hour, max 5 concurrent sessions/devices per user, and refresh tokens are stored hashed in RavenDB.
5. **Revocation semantics (Req 1.5)**: should logout revoke only the current session or all sessions? On password change/admin disable, should all sessions be revoked immediately?
A: Logout revoke only the current session,
6. **Roles/scopes (Req 1.4)**: how are Admins represented (separate user type vs role claim), and what claim(s) should be used for authorization?
A: Admins should be represented as a separate user type, and use `admin` role claim for authorization.
7. **`PendingDeletion` policy (Req 1.3)**: which operations remain allowed (login, streaming, metadata edits, delete requests), and what cleanup timeline is expected?
A: Only login and streaming are allowed, and cleanup timeline is expected within 30 days.

## Upload (Req 2.x)

1. **Upload mechanics (Req 2.1/2.3)**: is the intended approach direct-to-MinIO via presigned PUT/POST, a proxied upload through the API, or multipart/chunked uploads?
A: Use direct-to-MinIO via presigned PUT/POST.
2. **Upload completion**: if uploads are direct-to-MinIO, what is the source of truth that the upload completed successfully (client callback to API, MinIO event notification, periodic reconciliation)?
A: Use MinIO event notification.
3. **Supported formats (Req 2.2)**: what formats/codecs are supported initially, and should validation be based on MIME type, file extension, magic bytes, or a decoding attempt?
A: Use MIME type,
4. **Limits**: max upload size, max duration, and any per-user quotas (storage, track count).
A: Use max upload size, max duration, and any per-user quotas (storage, track count).
5. **Deduplication**: should the system dedupe uploads via checksum (Req 2.6 includes `Checksum` as optional on Track), and if so when/how is checksum computed?
A: Use checksum computation after upload completion.
6. **ObjectKey scheme (Req 2.3)**: should `ObjectKey` be guess-resistant and user-scoped (e.g., include user ID), and do we need bucket-per-env or bucket-per-user?
A: Use guess-resistant and user-scoped ObjectKey.
7. **Track record timing (Req 2.4/2.5)**: is the track metadata record created before the upload (to reserve an ID/object key) or after the upload succeeds?
A: Use track metadata record created after the upload succeeds.
8. **Event emission contract (Req 2.6)**: should `AudioUploadedEvent` be emitted exactly-once, at-least-once, or best-effort? What are the expected retry/outbox semantics?
A: Use exactly-once.

## Processing / Workers (Req 3.x)

1. **Retry policy**: how many processing retries, what backoff strategy, and do we need a DLQ/topic for poison messages?
A: Use 3 retries,
2. **Idempotency definition (Req 3.5)**: what should be considered safe to repeat (e.g., overwrite `AudioMetadata`, regenerate waveform), and what should be guarded (e.g., avoid re-transitioning `Ready` back to `Processing`)?
A: Safe to repeat: `AudioMetadata`, Regenerate waveform. Safe to guard: avoid re-transitioning `Ready` back to `Processing`. 
3. **Failure classification (Req 3.4)**: what constitutes an “unrecoverable processing error” vs transient (network, object missing, decoder errors)?
A: Unrecoverable processing error.
4. **Waveform output**: where should waveform artifacts live (RavenDB document, MinIO sidecar object), and what format is desired?
A: Waveform artifacts should live in MinIO sidecar object, and use WAV format.
5. **Concurrency limits**: should processing be limited per user or globally (to protect MinIO/CPU)?
A: Processing should be limited globally.

## Storage, Presigned URLs, Lifecycle (Req 4.x / Req 10.x)

1. **TTL values (Req 4.2/Req 10.3)**: expected TTLs for presigned upload URLs, presigned streaming URLs, refresh tokens, and revocation flags.
A: Expected TTLs for presigned upload URLs,
2. **Cache keying (Req 10.2)**: is user+track sufficient, or do we need to include MIME/variant/bitrate/format version in the key?
A: User+track sufficient.
3. **Security for cached URLs**: is it acceptable to cache full presigned URLs in Garnet, or should they be encrypted at rest / avoided in favor of caching inputs and regenerating?
A: Encrypted at rest.
4. **Invalidation triggers (Req 4.4/Req 10.2)**: besides track deletion, should cached URLs be invalidated on logout, password change, user disable, and permission changes?
A: Cached URLs should be invalidated on logout,
5. **Deletion grace period (Req 4.4)**: how long is the grace period, and can a user undo/restore a deletion during that window?
A: 30 days, can a user undo/restore a deletion during that window.

## Streaming & Telemetry (Req 5.x)

1. **Streaming path**: is streaming always direct-from-MinIO via presigned GET, or will the API ever proxy streaming (e.g., for DRM, watermarking, analytics)?
A: Streaming always direct-from-MinIO via presigned GET.
2. **Range requests (Req 5.3)**: any specific client compatibility requirements (iOS/Android/web) that imply constraints on MinIO configuration or headers?
A: No specific client compatibility requirements.
3. **Telemetry capture (Req 5.4)**: since the backend may not see streamed bytes, what is the desired telemetry mechanism (client-reported events vs storage access logs), and what is the minimum schema?
A: Client-reported events.
4. **Privacy/retention**: analytics retention period and any privacy constraints (e.g., opt-out, deletion on account removal).
A: Analytics retention period 30 days.

## Track Management (Req 6.x)

1. **Sorting/filtering**: required sort orders (recent, title, artist) and filter semantics (case-insensitive search, partial matches, status filters).
A: Required sort orders (recent, title, artist) and filter semantics (case-insensitive search, partial matches, status filters).
2. **Editable fields (Req 6.2)**: confirm allowed updates (Title/Artist only?), and whether editing is allowed while `Status=Processing`.
A: Confirm allowed updates (Title/Artist only?).
3. **Deletion model (Req 6.3/Req 4.4)**: is deletion soft-delete (status change) vs hard-delete of the RavenDB record, and should deletes be idempotent?
A: Soft-delete (status change).
4. **Track sharing**: is there any notion of sharing tracks across users, or strictly per-user ownership only?
A: No notion of sharing tracks across users, strictly per-user ownership only.

## Playlists (Req 7.x)

1. **Playlist constraints**: max playlists per user, max tracks per playlist, duplicate tracks allowed or not.
A: Max playlists per user, max tracks per playlist, duplicate tracks allowed.
2. **Ordering semantics**: do we require stable ordering with explicit positions, and how should concurrent edits be handled?
A: Require stable ordering with explicit positions.
3. **Future sharing**: should the model anticipate playlist sharing/collaboration, or keep it strictly private for now?
A: Model should anticipate playlist sharing/collaboration.

## Analytics, Events, Admin (Req 9.x / Req 11.x)

1. **Event format**: JSON vs Avro/Protobuf, and is there a schema registry/versioning system expected beyond a `SchemaVersion` field?
A: JSON.
2. **Topic partitioning/keying**: what key should be used for partitioning (TrackId, UserId), and what ordering guarantees are required?
A: TrackId.
3. **CorrelationId propagation (Req 9.3)**: where does `CorrelationId` originate (API gateway, client, backend), and should it be required on inbound requests?
A: API gateway.
4. **Admin auditing**: do admin actions (disable user, delete track) require audit logs and/or reason codes?
A: Require audit logs and reason codes.
5. **Moderation semantics**: difference between “delete”, “moderate”, and “disable”, and how should these affect streaming URL issuance and processing workers?
A: “Delete” removes the track from all playlists and prevents further streaming, “moderate” marks the track for review, “disable” prevents streaming but keeps the track accessible for review.
