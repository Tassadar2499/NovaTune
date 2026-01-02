# Req 3.x — Post-upload Processing (workers)

- **Req 3.1** The system shall asynchronously process uploaded audio (metadata extraction; waveform generation).
- **Req 3.2** The system shall consume `AudioUploadedEvent` messages and invoke track processing logic for the referenced `TrackId`.
- **Req 3.3** The processor shall fetch audio from MinIO by `ObjectKey`, extract `AudioMetadata`, compute duration, and persist results to RavenDB.
- **Req 3.4** The processor shall transition track `Status`:
  - `Ready` on successful processing.
  - `Failed` on unrecoverable processing error.
- **Req 3.5** Processing shall be idempotent per `TrackId` (replayed events must not corrupt state).

## Clarifications

- Retries: 3 processing retries (backoff and DLQ semantics TBD).
- Idempotency: safe to repeat overwriting `AudioMetadata` and regenerating waveform artifacts; processing must not transition `Ready` back to `Processing`.
- Waveform artifacts: stored as a MinIO sidecar object in WAV format.
- Concurrency: processing should be limited globally.
