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

## NF-1.x — Availability, Resilience, and Deployability

- **NF-1.1** The system shall be deployable to Kubernetes and support running the API and worker workloads as separate, independently scalable deployments.
- **NF-1.2** The API and workers shall expose health endpoints suitable for Kubernetes liveness and readiness probes, distinguishing “alive” from “ready to serve traffic”.
- **NF-1.3** Deployments shall support rolling updates without requiring downtime for the API, and shall allow worker updates without breaking event consumption semantics (e.g., safe restarts).
- **NF-1.4** The system shall tolerate transient dependency failures (RavenDB/MinIO/Redpanda/Garnet) via bounded retries and timeouts; failure behavior must prefer safe degradation over corrupting state.
- **NF-1.5** The system shall define availability objectives per environment (SLOs TBD) and measure them based on server-side request outcomes for critical endpoints (authentication, upload initiation, streaming URL issuance).

## NF-2.x — Performance and Scalability

- **NF-2.1** The system shall support horizontal scaling for:
  - API request handling (stateless scaling behind an ingress/load balancer).
  - Worker processing throughput (parallel consumers with bounded concurrency).
- **NF-2.2** API endpoints shall have explicit latency budgets per endpoint class (TBD) and enforce timeouts on calls to external dependencies.
- **NF-2.3** Streaming shall not proxy audio bytes through the API; the API shall only issue short-lived presigned URLs (per Req 5.x).
- **NF-2.4** Upload and processing paths shall be designed to avoid unbounded memory usage (streaming IO; bounded buffers) and must tolerate large audio files within configured limits.
- **NF-2.5** The system shall enforce rate limits as required by functional requirements (Req 8.2) and provide configuration knobs per environment.

## NF-3.x — Security and Privacy

- **NF-3.1** All external HTTP traffic shall be served over TLS (termination at ingress is acceptable), and internal service-to-service traffic should also be encrypted where supported by the platform.
- **NF-3.2** Authentication tokens, refresh tokens, and any session artifacts shall be stored and transmitted securely:
  - Refresh tokens must be stored hashed (per functional requirements).
  - Cache entries that include full presigned URLs must be encrypted at rest (Req 10.3).
- **NF-3.3** Object storage access shall be strictly scoped:
  - Presigned URLs shall be short-lived and user+track scoped (Req 4.3).
  - Object keys must be guess-resistant and user-scoped (Req 2.3).
- **NF-3.4** The system shall implement least-privilege service credentials for RavenDB/MinIO/Redpanda/Garnet and must not embed secrets in repository-tracked configuration files.
- **NF-3.5** Admin operations shall be audited with actor identity, timestamp, and reason codes (Req 11.2), and audit records shall be tamper-evident (mechanism TBD).

## NF-4.x — Observability and Diagnostics

- **NF-4.1** The system shall emit structured logs for API requests and background jobs, including `CorrelationId` where applicable (Req 9.3).
- **NF-4.2** The system shall expose metrics for:
  - Request rate, latency, error rate (API).
  - Event consumption lag, processing success/failure counts, retry counts (workers).
  - Cache hit ratio for presigned URLs and token/session cache.
- **NF-4.3** The system shall provide traceability across API → event publication → worker processing using a propagated correlation identifier (Req 9.3), enabling per-track investigation.
- **NF-4.4** Sensitive fields (tokens, presigned URLs, passwords) shall not be logged; logs must be redactable and safe for centralized aggregation.

## NF-5.x — Maintainability and Operability

- **NF-5.1** Configuration shall be environment-driven and support `{env}`-prefixed topic naming for Redpanda (functional decision), with validation at startup to prevent misconfiguration.
- **NF-5.2** Background processing shall be idempotent per `TrackId` (Req 3.5) and safe under replay, duplication, and reordering patterns expected in Kafka-compatible systems.
- **NF-5.3** The system shall define operational runbooks (TBD) for dependency outages, event backlog, and data lifecycle issues (e.g., stuck `Processing` tracks).
- **NF-5.4** The system should support safe feature rollout controls (e.g., configuration flags) for high-risk changes such as caching TTLs, rate limits, and processing concurrency.

## NF-6.x — Data Management, Integrity, and Lifecycle

- **NF-6.1** The system shall ensure deleted tracks do not remain accessible:
  - Cached presigned URLs must be invalidated promptly on track deletion (Req 4.4).
  - Storage objects must be deleted after the configured grace period (Req 4.4), and cleanup must be repeatable and safe to run multiple times.
  - The system shall prevent new streaming URL issuance for tracks that are `Deleted` (or otherwise not permitted), even if cached URLs exist.
- **NF-6.2** Persistence operations against RavenDB shall prevent lost updates and inconsistent state transitions:
  - Track status transitions must be monotonic with respect to processing flow (e.g., `Ready` must not return to `Processing`).
  - Concurrent updates (e.g., user edits vs worker updates) must use optimistic concurrency or equivalent safeguards (mechanism TBD).
- **NF-6.3** Data retention policies shall be explicit and enforceable:
  - Analytics retention is 30 days (Req 5.x clarification) unless configured otherwise.
  - Deleted-track grace period is 30 days (Req 4.x clarification) unless configured otherwise.
- **NF-6.4** Event schemas shall be versioned and backwards/forwards compatible to support phased deployments (Req 9.1), and schema evolution rules must be documented (TBD).

## Open Items (TBD)

- Quantitative SLOs (availability and latency) per environment.
- Concrete retry/backoff/DLQ semantics for workers and event publication.
- Audit log storage mechanism and tamper-evidence strategy.
- Concrete concurrency limits for processing and telemetry ingestion.
