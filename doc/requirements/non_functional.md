# NovaTune – Non-Functional Requirements

> **Version:** 2.0
> **Last Updated:** 2025-11-23
> **Status:** Active

These guardrails complement [functional.md](functional.md) and the stack defined in [stack.md](stack.md) (ASP.NET Core, RavenDB, MinIO, NCache, Kafka/RabbitMQ, YARP, Dotnet Aspire, Docker/Kubernetes, GitHub Actions). Cite IDs (e.g., NF-4.2) alongside functional references in tickets and PRs.

## Legend

| Symbol | Meaning |
|--------|---------|
| **P1** | Must-have – blocks release if unmet |
| **P2** | Should-have – degrades quality if unmet |
| **P3** | Nice-to-have – enhances quality |
| `[Test]` | Verified via automated tests in CI |
| `[Monitor]` | Verified via real-time observability |
| `[Audit]` | Verified via periodic manual review |
| `[Review]` | Verified via code review checklist |

---

## NF-1 Performance & Scalability

- **NF-1.1 Throughput** `[Monitor]` **P1**
  Each API node must sustain 50 concurrent uploads and 200 concurrent stream requests without exceeding 70% CPU or 80% memory; scale out via Kubernetes HPA when thresholds breach.
  *Supports: FR 2.x, FR 5.x*

- **NF-1.2 Latency** `[Monitor]` **P1**
  - Upload confirmations (FR 2.x): <3s p50, <5s p95, <8s p99 for files ≤50 MB; <10s p95 for files 50–200 MB.
  - Streaming URL delivery (FR 5.x): <200ms p50, <500ms p95, <1s p99.
  *Supports: FR 2.5, FR 5.2*

- **NF-1.3 Statelessness** `[Review]` **P1**
  ASP.NET Core services remain stateless, persisting long-lived data in RavenDB/MinIO and using NCache for ephemeral tokens (TTL ≤15 min) so pods can be rescheduled freely.
  *Supports: FR 4.2, FR 5.2, FR 10.5*

- **NF-1.4 Metadata Query Latency** `[Test]` **P2**
  Track browsing and search queries (FR 6.1, FR 6.4) must return results in <300ms p95 for datasets ≤10,000 tracks per user; RavenDB indexes must be optimized accordingly.
  *Supports: FR 6.1, FR 6.4, FR 6.5*

- **NF-1.5 API Gateway Performance** `[Monitor]` **P2**
  YARP routing decisions must add <10ms overhead at p95. Rate limiting: 100 requests/min per authenticated user, 20 requests/min per anonymous IP; return 429 with `Retry-After` header when exceeded.
  *Supports: FR 11.4, FR 10.2*

- **NF-1.6 Audio Processing Limits** `[Monitor]` **P2**
  FFmpeg/FFprobe operations must complete within 30s timeout; worker processes limited to 2 CPU cores and 1GB memory. Background processing queue (RabbitMQ) depth must not exceed 1,000 pending jobs; trigger scaling alerts at 500.
  *Supports: FR 3.1, FR 3.3*

---

## NF-2 Reliability & Availability

- **NF-2.1 Uptime** `[Monitor]` **P1**
  Upload, streaming, and track-management endpoints target 99.5% availability over rolling 30-day windows, measured excluding planned maintenance. Admin-only endpoints (FR 11.x) target 99.0%. Error budget: 3.6 hours/month for core endpoints.
  *Supports: FR 2.x, FR 5.x, FR 6.x*

- **NF-2.2 Resilience** `[Test]` **P1**
  Wrap MinIO, RavenDB, NCache, and Kafka clients with Polly policies: exponential backoff (initial 100ms, max 5 retries), circuit breaker (5 failures opens circuit for 30s). Surface graceful error messages with correlation IDs if retries exhausted.
  *Supports: FR 2.5, FR 2.6, FR 6.3*

- **NF-2.3 Disaster Recovery** `[Audit]` **P1**
  | Service | RPO | RTO |
  |---------|-----|-----|
  | RavenDB | 15 min (snapshots) | 2 hours |
  | MinIO | 1 hour (replication) | 4 hours |
  | Kafka | 1 hour (topic retention) | 2 hours |
  | NCache | N/A (ephemeral) | 15 min (cold start) |

  DR drills: quarterly in staging, annually in production. Infrastructure-as-code (Helm/Terraform) committed and tested per drill.
  *Supports: FR 4.1, FR 4.4*

- **NF-2.4 Graceful Degradation** `[Review]` **P2**
  If NCache is unavailable, fall back to direct MinIO presigned URL generation (bypassing cache). If Kafka is unavailable, queue events to RabbitMQ dead-letter exchange for replay. Document fallback behavior in runbooks.
  *Supports: FR 2.6, FR 5.2*

---

## NF-3 Security & Compliance

- **NF-3.1 Secrets Management** `[Audit]` **P1**
  Store MinIO, RavenDB, Kafka, NCache, and JWT signing keys in Kubernetes secrets (production) or `dotnet user-secrets` (development). Rotate credentials quarterly via automated rotation with 24-hour grace period for old credentials. Monitor rotation compliance via Aspire dashboard alerts.
  *Supports: FR 10.4*

- **NF-3.2 Data Protection** `[Audit]` **P1**
  - Transport: TLS 1.2+ enforced on all API, gRPC, and Kubernetes ingress surfaces.
  - Storage: RavenDB encryption at rest enabled; MinIO SSE-S3 with AES-256.
  - Audit: Log all reads of private tracks (FR 4.3) with user ID and timestamp; retain audit logs 90 days.
  *Supports: FR 4.1, FR 4.3, FR 10.1, FR 10.3, FR 10.4*

- **NF-3.3 Privacy & Data Subject Rights** `[Test]` **P1**
  Honor data-subject deletion requests (FR 1.4) within 30 days:
  1. Delete RavenDB user document and all owned track documents.
  2. Delete MinIO objects via batch deletion job.
  3. Invalidate NCache entries for user.
  4. Publish Kafka tombstone events for compaction.
  5. Purge user data from RavenDB snapshots within 90 days (next backup rotation).

  Maintain audit trail of deletion requests for 1 year. Provide verification endpoint for deletion confirmation.
  *Supports: FR 1.4*

- **NF-3.4 Authentication Performance** `[Test]` **P2**
  - JWT access token TTL: 15 minutes.
  - Refresh token TTL: 7 days (sliding expiration).
  - Maximum concurrent sessions per user: 5; oldest session revoked on new login if exceeded.
  - Login/token refresh latency: <200ms p95.
  - Token revocation propagation to NCache: <5 seconds.
  *Supports: FR 1.2, FR 10.2, FR 10.5*

- **NF-3.5 Input Validation** `[Test]` **P1**
  Validate all user inputs against OWASP Top 10: sanitize file names, reject path traversal attempts, parameterize RavenDB queries, enforce Content-Type validation on uploads. Maximum request body: 250 MB (configurable via FR 11.4).
  *Supports: FR 2.2, FR 11.4*

---

## NF-4 Observability & Monitoring

- **NF-4.1 Metrics** `[Monitor]` **P1**
  Expose via Dotnet Aspire/OTEL exporters:
  - Latency histograms (p50, p95, p99) per endpoint.
  - Throughput (requests/sec) per service.
  - NCache hit rate (target: >90% for presigned URLs).
  - Kafka/RabbitMQ queue depth and consumer lag.
  - MinIO storage consumption per bucket.
  - Active user sessions count.

  Retention: 15 days at 1-minute granularity, 90 days at 15-minute aggregations. Cardinality limit: 10,000 unique label combinations per metric.
  *Supports: FR 9.1, FR 9.2, FR 9.3*

- **NF-4.2 Logging** `[Review]` **P1**
  Emit structured JSON logs via Serilog with:
  - Correlation ID (propagated across services).
  - Requirement reference (e.g., `"req": "FR-2.3"`).
  - User ID (hashed for privacy in non-debug levels).
  - Timestamp (ISO 8601 UTC).

  Scrub: JWT tokens, MinIO object keys, PII fields. Log levels: Debug (dev only), Info, Warning, Error. Retention: 30 days hot storage, 1 year cold archive.
  *Supports: FR 2.5, FR 11.2*

- **NF-4.3 Alerting** `[Monitor]` **P1**
  | Severity | Condition | Response Time |
  |----------|-----------|---------------|
  | P1-Critical | Error rate >5% for 5 min, service down | 15 min |
  | P2-High | Error rate >2% for 5 min, latency 2x baseline | 1 hour |
  | P3-Medium | MinIO disk >80%, queue depth >500 | 4 hours |
  | P4-Low | Cache hit rate <80%, non-critical warnings | Next business day |

  Deliver alerts to PagerDuty/Slack. Implement alert deduplication (5-min window) and auto-resolve on recovery.
  *Supports: FR 9.3, FR 11.2*

- **NF-4.4 Distributed Tracing** `[Monitor]` **P2**
  All cross-service calls must propagate W3C trace context. Traces retained 7 days. Sample rate: 100% for errors, 10% for success in production.
  *Supports: FR 11.2*

---

## NF-5 DevOps & Deployment

- **NF-5.1 CI/CD Pipeline** `[Test]` **P1**
  GitHub Actions workflow must complete in <15 minutes and include:
  1. `dotnet format --verify-no-changes`
  2. `dotnet build` (warnings as errors)
  3. `dotnet test /p:CollectCoverage=true` (gate: ≥80% line coverage for Application layer)
  4. Container image build and push.
  5. SAST scan (e.g., CodeQL) – fail on high/critical findings.
  6. DAST scan on staging deployment – fail on critical findings.
  7. Dependency vulnerability scan (Dependabot/Snyk).
  *Supports: FR 11.4*

- **NF-5.2 Environments** `[Audit]` **P2**
  Maintain dev, staging, and prod Kubernetes namespaces with configuration parity via ConfigMaps and feature flags. Environment-specific values: MinIO bucket names, RavenDB database names, Kafka topic prefixes, log levels.
  *Supports: FR 11.4*

- **NF-5.3 Rollbacks** `[Test]` **P1**
  Support blue/green deployments with rollback completing in <5 minutes:
  1. Kubernetes deployment rollback.
  2. NCache invalidation for affected service keys.
  3. MinIO object versioning enables prior file restoration.
  4. RavenDB index rebuild if schema changed (max 10 min for <1M documents).
  *Supports: FR 4.3, FR 6.3*

- **NF-5.4 Infrastructure as Code** `[Review]` **P2**
  All infrastructure defined in Helm charts or Terraform modules. No manual cluster configuration; changes require PR review and terraform plan output.
  *Supports: NF-2.3*

---

## NF-6 Data Management & Storage Hygiene

- **NF-6.1 Lifecycle Rules** `[Audit]` **P2**
  - MinIO: Remove incomplete multipart uploads after 24 hours; delete orphaned objects (no RavenDB reference) within 7 days via background job consuming Kafka tombstones.
  - Kafka: Retain `audio-uploaded` topic 30 days, analytics topics 90 days, compact `user-events` topic.
  - RabbitMQ: Dead-letter queue retention 7 days; alert if DLQ depth >100.
  *Supports: FR 4.4, FR 6.3*

- **NF-6.2 RavenDB Integrity** `[Test]` **P1**
  - Schema/index changes require migration script with rollback procedure.
  - Index rebuild must complete in <10 min for <1M documents; test in staging before prod.
  - Verify query performance baseline (NF-1.4) after migrations.
  - Backup verification: restore test monthly in isolated environment.
  *Supports: FR 6.1, FR 6.4*

- **NF-6.3 Event Stream Governance** `[Audit]` **P2**
  Document per topic:
  - Retention policy (time or size based).
  - Compaction strategy (if applicable).
  - Consumer groups and their lag thresholds.
  - Schema (Avro/JSON schema registry or documented contract).

  Monitor consumer lag; alert if lag exceeds 10,000 messages.
  *Supports: FR 2.6, FR 6.2, FR 6.3*

- **NF-6.4 Cache Management** `[Monitor]` **P2**
  NCache configuration:
  - Presigned URL cache: TTL 10 minutes, LRU eviction when >80% capacity.
  - Session state: TTL matches refresh token (7 days sliding).
  - Token revocation list: TTL 24 hours, no eviction.

  Monitor eviction rate; alert if >1000 evictions/min.
  *Supports: FR 4.2, FR 5.2, FR 10.5*

---

## NF-7 User Experience & Accessibility

- **NF-7.1 Responsiveness** `[Test]` **P1**
  - UI functional on viewport widths ≥320px.
  - Touch targets minimum 44x44px.
  - Audio controls operable via keyboard (Tab, Enter, Space, Arrow keys).
  - Screen reader compatibility: all interactive elements have ARIA labels.
  - WCAG 2.1 Level AA compliance required.
  *Supports: FR 5.3*

- **NF-7.2 Feedback** `[Test]` **P2**
  - Progress indicator for uploads >1s (FR 2.5).
  - Loading spinner during cache warm-up or URL regeneration (FR 5.4).
  - Actionable error messages with retry option for throttled (429) or failed operations.
  - Toast notifications for background task completion.
  *Supports: FR 2.5, FR 5.4*

- **NF-7.3 Localization-Ready** `[Review]` **P3**
  - UI strings in resource files (Vue i18n).
  - No hardcoded date/time/number formats; use Intl API.
  - RTL layout support in CSS (future-proofing).
  *Supports: FR 11.4*

- **NF-7.4 Frontend Performance** `[Test]` **P2**
  Core Web Vitals targets:
  - LCP (Largest Contentful Paint): <2.5s.
  - FID (First Input Delay): <100ms.
  - CLS (Cumulative Layout Shift): <0.1.

  Initial JavaScript bundle: <300KB gzipped. Service worker caching for static assets and API responses (offline-capable player controls).
  *Supports: FR 5.3*

---

## NF-8 Maintainability & Documentation

- **NF-8.1 Solution Hygiene** `[Review]` **P1**
  Organize code into single `.sln` with:
  - `NovaTune.Api` – ASP.NET Core endpoints.
  - `NovaTune.Application` – Use cases, abstractions.
  - `NovaTune.Domain` – Entities, value objects, validation.
  - `NovaTune.Infrastructure` – RavenDB/MinIO/Kafka/NCache adapters.
  - `tests/NovaTune.Tests` – xUnit test projects.

  Functions ≤40 lines; classes ≤400 lines. Enforce dependency boundaries: Domain has no external references; Infrastructure implements Application abstractions.
  *Supports: FR 11.4*

- **NF-8.2 Testing Standards** `[Test]` **P1**
  - Unit tests: `{Target}.Tests.cs`, execute in <30s total.
  - Integration tests: `{Target}.IntegrationTests.cs`, execute in <5 min total using Testcontainers.
  - Coverage gate: ≥80% line coverage for `NovaTune.Application` and auth middleware.
  - Deterministic fakes for MinIO/RavenDB/Kafka; clock abstractions for time-sensitive logic.
  - Consider mutation testing (Stryker.NET) for critical paths.
  *Supports: FR 11.4*

- **NF-8.3 Documentation Freshness** `[Audit]` **P2**
  Update within 1 sprint of change:
  - `README.md` – Setup instructions, commands.
  - `functional.md` – Feature requirements.
  - `stack.md` – Technology decisions.
  - `non_functional.md` – Quality requirements (this document).

  Attach Architecture Decision Records (ADRs) in `doc/adr/` for major choices.
  *Supports: NF-8.1*

- **NF-8.4 API Documentation** `[Test]` **P2**
  - All public endpoints documented in OpenAPI 3.0 spec.
  - Scalar UI available at `/docs` in non-production environments.
  - Request/response schemas validated at runtime in development mode.
  - Breaking API changes require version bump and migration guide.
  *Supports: FR 11.4*

---

## NF-9 Integration & Interoperability

- **NF-9.1 API Versioning** `[Review]` **P2**
  - URL path versioning: `/api/v1/`, `/api/v2/`.
  - Deprecation policy: Maintain N-1 version for 6 months after N release.
  - Breaking changes documented in CHANGELOG.md with migration steps.
  *Supports: FR 11.4*

- **NF-9.2 Event Schema Evolution** `[Review]` **P2**
  Kafka/RabbitMQ message contracts:
  - Use JSON with explicit version field (`"schemaVersion": 1`).
  - Backward-compatible changes only (add fields, don't remove/rename).
  - Schema documentation in `doc/events/` per topic.
  - Consumer tolerance: Ignore unknown fields.
  *Supports: FR 2.6, FR 6.2*

- **NF-9.3 Service Discovery** `[Test]` **P2**
  Services discover dependencies via Dotnet Aspire service discovery. No hardcoded URLs; all endpoints resolved at runtime. Health checks exposed at `/health` and `/ready` for Kubernetes probes.
  *Supports: NF-2.1*

- **NF-9.4 External Integration Readiness** `[Review]` **P3**
  Architecture supports future integrations:
  - OAuth 2.0 provider federation (Google, GitHub).
  - Webhook notifications for third-party integrations.
  - Export API for data portability (FR 1.4 related).
  *Supports: FR 1.1, FR 8.x*

---

## Traceability Matrix

| NFR ID | Related FRs | Priority | Verification | Implementation Phase |
|--------|-------------|----------|--------------|---------------------|
| NF-1.1 | FR 2.x, FR 5.x | P1 | Monitor | Phase 3, 5 |
| NF-1.2 | FR 2.5, FR 5.2 | P1 | Monitor | Phase 3, 5 |
| NF-1.3 | FR 4.2, FR 5.2, FR 10.5 | P1 | Review | Phase 4 |
| NF-1.4 | FR 6.1, FR 6.4, FR 6.5 | P2 | Test | Phase 6 |
| NF-1.5 | FR 11.4, FR 10.2 | P2 | Monitor | Phase 8 |
| NF-1.6 | FR 3.1, FR 3.3 | P2 | Monitor | Phase 3 |
| NF-2.1 | FR 2.x, FR 5.x, FR 6.x | P1 | Monitor | Cross-cutting |
| NF-2.2 | FR 2.5, FR 2.6, FR 6.3 | P1 | Test | Phase 3, 6 |
| NF-2.3 | FR 4.1, FR 4.4 | P1 | Audit | Cross-cutting |
| NF-2.4 | FR 2.6, FR 5.2 | P2 | Review | Phase 4, 5 |
| NF-3.1 | FR 10.4 | P1 | Audit | Phase 1 |
| NF-3.2 | FR 4.1, FR 4.3, FR 10.x | P1 | Audit | Phase 2, 4 |
| NF-3.3 | FR 1.4 | P1 | Test | Phase 2 |
| NF-3.4 | FR 1.2, FR 10.2, FR 10.5 | P2 | Test | Phase 2 |
| NF-3.5 | FR 2.2, FR 11.4 | P1 | Test | Phase 3 |
| NF-4.1 | FR 9.1, FR 9.2, FR 9.3 | P1 | Monitor | Phase 8 |
| NF-4.2 | FR 2.5, FR 11.2 | P1 | Review | Cross-cutting |
| NF-4.3 | FR 9.3, FR 11.2 | P1 | Monitor | Phase 8 |
| NF-4.4 | FR 11.2 | P2 | Monitor | Cross-cutting |
| NF-5.1 | FR 11.4 | P1 | Test | Cross-cutting |
| NF-5.2 | FR 11.4 | P2 | Audit | Cross-cutting |
| NF-5.3 | FR 4.3, FR 6.3 | P1 | Test | Cross-cutting |
| NF-5.4 | NF-2.3 | P2 | Review | Cross-cutting |
| NF-6.1 | FR 4.4, FR 6.3 | P2 | Audit | Phase 4 |
| NF-6.2 | FR 6.1, FR 6.4 | P1 | Test | Phase 6 |
| NF-6.3 | FR 2.6, FR 6.2, FR 6.3 | P2 | Audit | Phase 3, 6 |
| NF-6.4 | FR 4.2, FR 5.2, FR 10.5 | P2 | Monitor | Phase 4 |
| NF-7.1 | FR 5.3 | P1 | Test | Phase 5 |
| NF-7.2 | FR 2.5, FR 5.4 | P2 | Test | Phase 3, 5 |
| NF-7.3 | FR 11.4 | P3 | Review | Phase 7 |
| NF-7.4 | FR 5.3 | P2 | Test | Phase 5 |
| NF-8.1 | FR 11.4 | P1 | Review | Phase 1 |
| NF-8.2 | FR 11.4 | P1 | Test | Cross-cutting |
| NF-8.3 | NF-8.1 | P2 | Audit | Cross-cutting |
| NF-8.4 | FR 11.4 | P2 | Test | Phase 1 |
| NF-9.1 | FR 11.4 | P2 | Review | Phase 1 |
| NF-9.2 | FR 2.6, FR 6.2 | P2 | Review | Phase 3 |
| NF-9.3 | NF-2.1 | P2 | Test | Phase 1 |
| NF-9.4 | FR 1.1, FR 8.x | P3 | Review | Phase 7 |

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 2.0 | 2025-11-23 | Comprehensive upgrade: added priorities, verification methods, NF-9 section, traceability matrix, measurable criteria |
| 1.0 | 2025-11-22 | Initial release |
