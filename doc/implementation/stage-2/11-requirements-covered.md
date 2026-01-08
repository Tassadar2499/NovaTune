# Requirements Covered

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
