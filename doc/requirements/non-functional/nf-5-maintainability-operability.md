# NF-5.x — Maintainability and Operability

- **NF-5.1** Configuration shall be environment-driven and support `{env}`-prefixed topic naming for Redpanda (functional decision), with validation at startup to prevent misconfiguration.
- **NF-5.2** Background processing shall be idempotent per `TrackId` (Req 3.5) and safe under replay, duplication, and reordering patterns expected in Kafka-compatible systems.
  - Delivery semantics target: "effectively-once" via idempotent consumers, plus an outbox for critical producer events (e.g., `AudioUploadedEvent`, `TrackDeletedEvent`).
- **NF-5.3** The system shall define operational runbooks under `doc/` for MVP, covering at minimum:
  - Dependency outages.
  - Worker backlog/lag and replay procedures.
  - Stuck tracks remediation.
  - Key rotation steps.
- **NF-5.4** The system should support safe feature rollout controls via configuration-only feature flags (app configuration / env vars) for cache/rate limit/concurrency/telemetry knobs; dedicated feature-flag service is deferred.
