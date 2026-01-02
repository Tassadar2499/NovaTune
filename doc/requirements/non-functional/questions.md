# NovaTune Backend Service — Non-Functional Questions

This document captures open questions needed to finalize the non-functional requirements in `doc/requirements/non-functional/main.md`.

## NF-1.x — Availability, Resilience, and Deployability

- **Q (NF-1.5)** What are the target SLOs per environment for:
  - API availability (monthly)?
    - **A:** `dev`: best-effort (no SLO). `staging`: 99.0%. `prod`: 99.5% (initial target; revisit after real traffic).
  - p95/p99 latency for critical endpoints (login, upload initiation, streaming URL issuance)?
    - **A:** `staging`: p95 750ms / p99 2000ms. `prod`: p95 500ms / p99 1500ms.
  - Worker processing time-to-ready (from `AudioUploadedEvent` to `Track.Status=Ready`)?
    - **A:** `staging`: p95 5 min / p99 30 min. `prod`: p95 2 min / p99 15 min.
- **Q (NF-1.2)** What should readiness mean for the API and workers?
  - Must RavenDB/MinIO/Redpanda/Garnet be reachable to be “ready”, or should the API come up “ready” and degrade specific endpoints?
    - **A:** API readiness requires RavenDB + Redpanda; MinIO required for upload/stream URL issuance; Garnet is optional (degrade to non-cached behavior). Worker readiness requires Redpanda and any dependency required for that worker’s role (e.g., MinIO + RavenDB for processing workers).
- **Q (NF-1.3)** What is the expected deployment strategy?
  - Single-region only, or multi-region active/active or active/passive?
    - **A:** Single-region for MVP. Revisit multi-region (active/passive) only after event schema evolution, replication, and DR strategy are defined.
  - Are planned maintenance windows allowed (and if so, when)?
    - **A:** Allowed for `staging` at any time; for `prod` only by exception with advance notice (prefer off-peak hours).
- **Q (NF-1.4)** What are the standard timeout and retry budgets for each dependency call class (RavenDB, MinIO, Redpanda, Garnet)?
  - Should retries be per-request, per-operation, or handled via background retry jobs?
    - **A:** Use per-operation timeouts with bounded jittered retries for idempotent operations; background retries for asynchronous work:
      - Garnet: 250–500ms timeout, 1 retry.
      - RavenDB: 2–5s timeout, 1 retry for reads; writes retry only if idempotent via optimistic concurrency.
      - MinIO: 5–10s timeout for presign/metadata, 1 retry; do not retry large stream transfers in-process.
      - Redpanda produce: 2–5s timeout, up to 2 retries; for “must publish” events prefer outbox.
- **Q (NF-1.4)** Should the system require circuit-breaking / bulkheads for dependency failures, and what endpoints must fail closed vs fail open?
  - **A:** Yes:
    - Bulkheads per dependency (bounded concurrency) to avoid cascading failure.
    - Circuit breakers for repeated dependency failures.
    - Fail closed: authentication, token refresh, upload initiation, streaming URL issuance, admin mutations.
    - Fail open (best-effort): cache reads/writes, analytics dashboards, non-critical telemetry ingestion (with sampling/backpressure).

## NF-2.x — Performance and Scalability

- **Q (NF-2.1)** What is the expected scale for MVP and target:
  - Concurrent active users and peak requests/sec for the API?
    - **A:** MVP envelope: ~200 concurrent users; peak ~50 RPS (bursty).
  - Peak uploads/sec and average upload size?
    - **A:** Peak ~2 uploads/sec; average 15–30MB per track.
  - Peak streaming URL issuance/sec?
    - **A:** Peak ~20 requests/sec (bursty).
  - Worker throughput (events/sec) and acceptable backlog recovery time?
    - **A:** Sustained ~5 events/sec per consumer group; recover 1 hour of backlog within 4 hours.
- **Q (NF-2.2)** What latency budgets should be used per endpoint class (auth, upload initiation, track browsing, admin queries)?
  - **A:** Initial p95 / p99 budgets (excluding client upload/stream transfer):
    - Auth (login/refresh): 400ms / 1200ms.
    - Upload initiation: 600ms / 1500ms.
    - Streaming URL issuance: 300ms / 1000ms.
    - Track browsing/details: 700ms / 2000ms.
    - Admin queries: 1000ms / 3000ms.
- **Q (NF-2.4)** What are the hard limits to enforce (per environment) for:
  - Max upload size (bytes)?
    - **A:** Default: 200MB (`dev`/`staging`), 500MB (`prod`) (configurable).
  - Max track duration?
    - **A:** Default: 2 hours (configurable).
  - Per-user quotas (storage, track count, playlists)?
    - **A:** Default: 10GB storage, 5,000 tracks, 200 playlists, 10,000 tracks per playlist (configurable).
- **Q (NF-2.5 / Req 8.2)** What are the concrete rate limits for:
  - Login attempts (per IP, per account)?
    - **A:** Default: 10/min per IP and 5/min per account; return 429 + `Retry-After` (configurable).
  - Upload initiation (per user)?
    - **A:** Default: 20/hour per user (burst 5/min).
  - Streaming URL issuance (per user/track)?
    - **A:** Default: 60/min per user and 10/min per track.
  - Telemetry ingestion (per user/device)?
    - **A:** Default: 120/min per device; allow server-side sampling/backpressure.

## NF-3.x — Security and Privacy

- **Q (NF-3.1)** Is internal encryption required (mTLS between services), or is TLS termination at ingress sufficient for MVP?
  - **A:** TLS termination at ingress is sufficient for MVP; internal mTLS is recommended for `prod` when platform support exists (service mesh or mTLS-enabled ingress-to-pod).
- **Q (NF-3.2 / Req 10.3)** What is the required approach for “encrypted at rest” cache entries that include presigned URLs?
  - Application-layer encryption vs platform-level disk encryption?
    - **A:** Application-layer encryption for cached presigned URL payloads (encrypt before writing to Garnet/Redis).
  - Key management: where do keys live (Kubernetes secrets vs external KMS), and what is the rotation policy?
    - **A:** Keys in external KMS for `prod` where available; otherwise Kubernetes secrets for `dev`/`staging`. Rotation: quarterly or on incident; support decrypt-with-previous-key during rotation.
- **Q (NF-3.4)** What is the chosen secret-management approach in Kubernetes (e.g., sealed secrets, external secret operator, cloud KMS integration)?
  - **A:** External Secrets Operator (or equivalent) backed by a secret manager/KMS for `prod`; sealed secrets acceptable for `dev`/`staging` if a secret manager is not available.
- **Q (NF-3.3)** What are the required TTLs for presigned upload and streaming URLs, and should TTLs vary by environment?
  - **A:** Upload presign: 15 min (`dev`/`staging`), 10 min (`prod`). Streaming presign: 2 min (`dev`/`staging`), 60–120s (`prod`). Cache TTL should be slightly shorter than presign expiry.
- **Q (NF-3.5 / Req 11.2)** Are there compliance requirements (e.g., GDPR/CCPA) affecting:
  - Audit log retention and access controls?
    - **A:** Assume GDPR-aligned posture: retain audit logs 1 year; restrict access to Admin role; record actor, timestamp, action, target, and reason.
  - Right-to-delete workflows and retention exceptions?
    - **A:** Support user deletion requests with documented exceptions (e.g., security/audit retention) and explicit retention periods per data class.

## NF-4.x — Observability and Diagnostics

- **Q (NF-4.2)** What is the observability stack target (OpenTelemetry, Prometheus/Grafana, Jaeger/Tempo, etc.)?
  - **A:** OpenTelemetry for instrumentation; Prometheus + Grafana for metrics; Tempo (or Jaeger) for traces; Loki (or equivalent) for logs.
- **Q (NF-4.1 / Req 9.3)** What is the source of `CorrelationId` (API gateway vs API-generated), and what is the expected propagation format (header name, event field name)?
  - **A:** Gateway-originated when present; otherwise generated by API. Accept `X-Correlation-Id` and `traceparent`; propagate `CorrelationId` field in all emitted events and logs.
- **Q (NF-4.2)** Which metrics are required for MVP dashboards and alerting, and what are the alert thresholds (SLO-based vs static)?
  - **A:** MVP dashboards: API RPS/latency/error rate; worker lag/success-failure/retry/time-to-ready; cache hit ratio/latency/error rate. Alerting: SLO-based in `prod`; static thresholds in `staging` (e.g., 5xx > 2% for 5 min; lag > 10k messages).
- **Q (NF-4.4)** What is considered sensitive/PII for logging and telemetry?
  - Are emails allowed in logs?
    - **A:** Treat emails as PII and do not log them (use `UserId` instead).
  - Should track titles/artists be treated as sensitive?
    - **A:** Treat titles/artists as user content; do not log by default (allow opt-in debug in `dev` only). Never log passwords, tokens, refresh tokens, presigned URLs, or raw object keys.

## NF-5.x — Maintainability and Operability

- **Q (NF-5.3)** Where should operational runbooks live (repo `doc/` vs external wiki), and what minimum scenarios must be covered for MVP?
  - **A:** Keep runbooks in repo under `doc/` for MVP; cover dependency outages, worker backlog/lag, replay procedures, stuck tracks remediation, and key rotation steps.
- **Q (NF-5.2)** What are the required semantics for event processing under failures?
  - At-least-once is assumed with Kafka; is “effectively-once” required via idempotency only, or is an outbox/inbox pattern required end-to-end?
  - **A:** “Effectively-once” via idempotent consumers plus an outbox for critical producer events (e.g., `AudioUploadedEvent`, `TrackDeletedEvent`).
- **Q (NF-5.4)** Are feature flags required for MVP, and if so what mechanism is preferred (config-only vs dedicated feature-flag service)?
  - **A:** Config-only feature flags for MVP (app configuration / env vars) for cache/rate limit/concurrency/telemetry knobs; dedicated feature-flag service deferred.

## NF-6.x — Data Management, Integrity, and Lifecycle

- **Q (NF-6.1 / Req 4.4)** What are the desired deletion semantics during the grace period?
  - Should users be able to restore a deleted track (and how should streaming behave during the grace window)?
  - **A:** Soft-delete immediately blocks streaming and presign issuance; user may restore within grace window if object still exists. After physical deletion, restore is not possible.
- **Q (NF-6.2)** What is the desired RavenDB concurrency strategy for track updates?
  - Strict optimistic concurrency for all writes vs selective use (e.g., only for status transitions)?
  - **A:** Use optimistic concurrency for all potentially conflicting writes; define merge policy for user edits vs worker metadata updates (user wins for title/artist, worker wins for technical metadata).
- **Q (NF-6.4 / Req 9.1)** What are the schema evolution rules for events?
  - Allowed changes (add optional fields) vs forbidden changes (rename/remove fields)?
    - **A:** Allowed: additive optional fields, adding enum values with safe defaults. Forbidden: rename/remove fields, changing requiredness/meaning.
  - How is `SchemaVersion` incremented and validated?
    - **A:** Increment integer only on breaking changes; producers set `SchemaVersion`; consumers validate and either DLQ or ignore per configured policy.
- **Q (NF-6.3)** Are backups/restore and disaster recovery requirements in scope for MVP?
  - RPO/RTO targets per environment (TBD)?
    - **A:** Yes for `staging`/`prod`. Initial `prod` targets: RPO 24h, RTO 4h (tighten later). RavenDB nightly backups; MinIO bucket versioning + lifecycle; Redpanda retention sized to allow replay (e.g., 7 days).
