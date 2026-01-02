# Req 9.x — Analytics & Events

- **Req 9.1** The system shall use Redpanda topics:
  - `{env}-audio-events`
  - `{env}-track-deletions`
  and include schema-versioning metadata for forwards/backwards compatibility.
- **Req 9.2** The system shall store analytics aggregates in RavenDB for Admin review.
- **Req 9.3** The system shall propagate and store `CorrelationId` across upload/processing/telemetry for tracing and debugging; `CorrelationId` originates in the API gateway.
- **Req 9.4** The system shall encode events as JSON.
- **Req 9.5** The system should use `TrackId` as the topic partition key for ordering/partitioning (other ordering guarantees TBD).
