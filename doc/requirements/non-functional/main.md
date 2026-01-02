# NovaTune Backend Service — Non-Functional Requirements

This document captures non-functional requirements (NFRs) for the NovaTune backend service, derived from:
- `doc/diagrams/context.puml`
- `doc/diagrams/component.puml`
- `doc/requirements/functional/`

Unless explicitly stated otherwise, these requirements apply to both the **API** and **background workers**.

## Conventions

- Identifiers use the format `NF-<area>.<number>`.
- “Shall” indicates a hard requirement; “should” indicates a recommended default for MVP that may be relaxed with justification.
- Where values are marked **TBD**, they must be made configurable and set per environment (`dev`/`staging`/`prod`).
- Where environment-specific numeric targets are listed, they are initial targets for MVP and should be revisited with real traffic.

## NF-1.x — Availability, Resilience, and Deployability

- **NF-1.1** The system shall be deployable to Kubernetes and support running the API and worker workloads as separate, independently scalable deployments.
- **NF-1.2** The API and workers shall expose health endpoints suitable for Kubernetes liveness and readiness probes, distinguishing “alive” from “ready to serve traffic”.
  - API readiness requires RavenDB + Redpanda connectivity.
  - MinIO connectivity is required for upload initiation and streaming URL issuance readiness.
  - Garnet (cache) is optional for readiness; cache failures must degrade to non-cached behavior.
  - Worker readiness requires Redpanda plus the dependencies required for that worker’s role (e.g., processing workers require MinIO + RavenDB).
- **NF-1.3** Deployments shall support rolling updates without requiring downtime for the API, and shall allow worker updates without breaking event consumption semantics (e.g., safe restarts).
  - Single-region deployment is the default for MVP; multi-region deployment is out of scope until event schema evolution and DR strategy are mature.
  - Planned maintenance windows are allowed for `staging` at any time; for `prod` only by exception with advance notice (prefer off-peak hours).
- **NF-1.4** The system shall tolerate transient dependency failures (RavenDB/MinIO/Redpanda/Garnet) via bounded timeouts, retries, and isolation; failure behavior must prefer safe degradation over corrupting state.
  - Timeouts and retry budgets (initial defaults):
    - Garnet: 250–500ms timeout, 1 retry.
    - RavenDB: 2–5s timeout, 1 retry for reads; writes retry only if idempotent (e.g., via optimistic concurrency).
    - MinIO: 5–10s timeout for presign/metadata, 1 retry; do not retry large stream transfers in-process.
    - Redpanda produce: 2–5s timeout, up to 2 retries; for “must publish” events prefer outbox (see NF-5.2).
  - Bulkheads (bounded concurrency) per dependency shall be implemented to avoid cascading failure.
  - Circuit breakers shall be implemented for repeated dependency failures.
  - Fail closed: authentication, token refresh, upload initiation, streaming URL issuance, admin mutations.
  - Fail open (best-effort): cache reads/writes, analytics dashboards, non-critical telemetry ingestion (with sampling/backpressure).
- **NF-1.5** The system shall define and measure SLOs per environment based on server-side request outcomes for critical endpoints and worker “time-to-ready”.
  - API availability SLO (monthly): `dev` best-effort; `staging` 99.0%; `prod` 99.5% (initial target).
  - Critical endpoint latency targets are defined in NF-2.2; additionally the overall critical-endpoint envelope target is: `staging` p95 750ms / p99 2000ms; `prod` p95 500ms / p99 1500ms.
    - Note: Upload initiation has an explicit higher p95 budget (600ms) due to MinIO presign; this is an accepted exception tracked separately from the overall envelope.
  - Worker time-to-ready target (from `AudioUploadedEvent` to `Track.Status=Ready`): `staging` p95 5 min / p99 30 min; `prod` p95 2 min / p99 15 min.

## NF-2.x — Performance and Scalability

- **NF-2.1** The system shall support horizontal scaling for:
  - API request handling (stateless scaling behind an ingress/load balancer).
  - Worker processing throughput (parallel consumers with bounded concurrency).
  - Initial MVP scale envelope (targets): ~200 concurrent users; peak ~50 RPS (bursty); peak ~2 uploads/sec; average upload size 15–30MB; peak streaming URL issuance ~20 req/sec (bursty).
  - Worker throughput target: sustained ~5 events/sec per consumer group; recover 1 hour of backlog within 4 hours.
- **NF-2.2** API endpoints shall have explicit latency budgets per endpoint class and enforce timeouts on calls to external dependencies.
  - Initial p95 / p99 budgets (excluding client upload/stream transfer):
    - Auth (login/refresh): 400ms / 1200ms.
    - Upload initiation: 600ms / 1500ms.
    - Streaming URL issuance: 300ms / 1000ms.
    - Track browsing/details: 700ms / 2000ms.
    - Admin queries: 1000ms / 3000ms.
- **NF-2.3** Streaming shall not proxy audio bytes through the API; the API shall only issue short-lived presigned URLs (per Req 5.x).
- **NF-2.4** Upload and processing paths shall be designed to avoid unbounded memory usage (streaming IO; bounded buffers) and must tolerate large audio files within configured limits.
  - Default hard limits (configurable):
    - Max upload size: 200MB (`dev`/`staging`), 500MB (`prod`).
    - Max track duration: 2 hours.
    - Per-user quotas: 10GB storage, 5,000 tracks, 200 playlists, 10,000 tracks per playlist.
- **NF-2.5** The system shall enforce rate limits as required by functional requirements (Req 8.2) and provide configuration knobs per environment.
  - Default limits (configurable); on limit exceed return HTTP 429 with `Retry-After` where applicable:
    - Login attempts: 10/min per IP and 5/min per account.
    - Upload initiation: 20/hour per user (burst 5/min).
    - Streaming URL issuance: 60/min per user and 10/min per track.
    - Telemetry ingestion: 120/min per device; allow server-side sampling/backpressure.

## NF-3.x — Security and Privacy

- **NF-3.1** All external HTTP traffic shall be served over TLS (termination at ingress is acceptable); internal mTLS is recommended for `prod` when platform support exists (service mesh or mTLS-enabled ingress-to-pod).
- **NF-3.2** Authentication tokens, refresh tokens, and any session artifacts shall be stored and transmitted securely:
  - Refresh tokens must be stored hashed (per functional requirements).
  - Cache entries that include full presigned URLs must be protected with application-layer encryption before writing to Garnet/Redis (Req 10.3).
  - Encryption key management:
    - `prod`: external KMS where available.
    - `dev`/`staging`: Kubernetes secrets are acceptable when KMS is not available.
    - Rotation: quarterly or on incident; support decrypt-with-previous-key during rotation.
- **NF-3.3** Object storage access shall be strictly scoped:
  - Presigned URLs shall be short-lived and user+track scoped (Req 4.3).
  - Object keys must be guess-resistant and user-scoped (Req 2.3).
  - Presigned URL TTL defaults (configurable):
    - Upload presign: 15 minutes (`dev`/`staging`), 10 minutes (`prod`).
    - Streaming presign: 2 minutes (`dev`/`staging`), 60–120 seconds (`prod`).
    - Cache TTL should be slightly shorter than presign expiry to reduce issuance of near-expired URLs.
- **NF-3.4** The system shall implement least-privilege service credentials for RavenDB/MinIO/Redpanda/Garnet and must not embed secrets in repository-tracked configuration files.
  - Kubernetes secret management target:
    - `prod`: External Secrets Operator (or equivalent) backed by a secret manager/KMS.
    - `dev`/`staging`: sealed secrets acceptable if a secret manager is not available.
- **NF-3.5** Admin operations shall be audited with actor identity, timestamp, action, target, and reason codes (Req 11.2).
  - Audit logs shall be retained for 1 year and access restricted to Admin role.
  - Right-to-delete workflows shall be supported with documented exceptions (e.g., security/audit retention) and explicit retention periods per data class.
  - Audit records should be tamper-evident (mechanism TBD).

## NF-4.x — Observability and Diagnostics

- **NF-4.1** The system shall emit structured logs for API requests and background jobs, including `CorrelationId` where applicable (Req 9.3).
  - Correlation source/propagation:
    - Use gateway-provided `X-Correlation-Id` when present; otherwise generate in the API.
    - Accept `traceparent` for distributed tracing.
    - Propagate a `CorrelationId` field in all emitted events and logs.
- **NF-4.2** The system shall expose metrics for:
  - Request rate, latency, error rate (API).
  - Event consumption lag, processing success/failure counts, retry counts (workers).
  - Cache hit ratio for presigned URLs and token/session cache.
- **NF-4.3** The system shall provide traceability across API → event publication → worker processing using a propagated correlation identifier (Req 9.3), enabling per-track investigation.
- **NF-4.4** The system shall publish MVP dashboards and alerts for core system health.
  - Dashboards: API RPS/latency/error rate; worker lag/success/failure/retry/time-to-ready; cache hit ratio/latency/error rate.
  - Alerting: SLO-based in `prod`; static thresholds in `staging` (e.g., 5xx > 2% for 5 min; lag > 10k messages).
- **NF-4.5** Sensitive fields shall not be logged; logs must be redactable and safe for centralized aggregation.
  - Never log: passwords, tokens, refresh tokens, presigned URLs, or raw object keys.
  - Treat emails as PII; do not log them (use `UserId` instead).
  - Treat track titles/artists as user content; do not log by default (allow opt-in debug in `dev` only).
- **NF-4.6** The observability stack target is:
  - OpenTelemetry for instrumentation.
  - Prometheus + Grafana for metrics.
  - Tempo (or Jaeger) for traces.
  - Loki (or equivalent) for logs.

## NF-5.x — Maintainability and Operability

- **NF-5.1** Configuration shall be environment-driven and support `{env}`-prefixed topic naming for Redpanda (functional decision), with validation at startup to prevent misconfiguration.
- **NF-5.2** Background processing shall be idempotent per `TrackId` (Req 3.5) and safe under replay, duplication, and reordering patterns expected in Kafka-compatible systems.
  - Delivery semantics target: “effectively-once” via idempotent consumers, plus an outbox for critical producer events (e.g., `AudioUploadedEvent`, `TrackDeletedEvent`).
- **NF-5.3** The system shall define operational runbooks under `doc/` for MVP, covering at minimum:
  - Dependency outages.
  - Worker backlog/lag and replay procedures.
  - Stuck tracks remediation.
  - Key rotation steps.
- **NF-5.4** The system should support safe feature rollout controls via configuration-only feature flags (app configuration / env vars) for cache/rate limit/concurrency/telemetry knobs; dedicated feature-flag service is deferred.

## NF-6.x — Data Management, Integrity, and Lifecycle

- **NF-6.1** The system shall ensure deleted tracks do not remain accessible:
  - Cached presigned URLs must be invalidated promptly on track deletion (Req 4.4).
  - Storage objects must be deleted after the configured grace period (Req 4.4), and cleanup must be repeatable and safe to run multiple times.
  - The system shall prevent new streaming URL issuance for tracks that are `Deleted` (or otherwise not permitted), even if cached URLs exist.
  - Soft-delete immediately blocks streaming and presign issuance; user may restore within the grace window if the object still exists. After physical deletion, restore is not possible.
- **NF-6.2** Persistence operations against RavenDB shall prevent lost updates and inconsistent state transitions:
  - Track status transitions must be monotonic with respect to processing flow (e.g., `Ready` must not return to `Processing`).
  - Concurrent updates (e.g., user edits vs worker updates) must use optimistic concurrency or equivalent safeguards.
  - Merge policy: user edits win for title/artist; workers win for technical metadata.
- **NF-6.3** Data retention policies shall be explicit and enforceable:
  - Analytics retention is 30 days (Req 5.x clarification) unless configured otherwise.
  - Deleted-track grace period is 30 days (Req 4.x clarification) unless configured otherwise.
- **NF-6.4** Event schemas shall be versioned and backwards/forwards compatible to support phased deployments (Req 9.1).
  - Allowed: additive optional fields, adding enum values with safe defaults.
  - Forbidden: rename/remove fields, changing requiredness/meaning.
  - `SchemaVersion` shall be incremented only on breaking changes; producers set `SchemaVersion`; consumers validate and either DLQ or ignore per configured policy.
- **NF-6.5** Backups/restore and disaster recovery shall be supported for `staging`/`prod`.
  - Initial `prod` targets: RPO 24h, RTO 4h (tighten later).
  - RavenDB: nightly backups.
  - MinIO: bucket versioning + lifecycle policies.
  - Redpanda: retention sized to allow replay (e.g., 7 days).

## Open Items (TBD)

- Concrete retry/backoff/DLQ semantics for workers and event publication (beyond the timeout/retry budgets above).
- Audit log storage mechanism and tamper-evidence strategy.
- Concrete bulkhead/circuit breaker configuration per dependency and endpoint class.
- Concrete concurrency limits for worker processing and telemetry ingestion.
