# Stage 0 — Infrastructure & Local Dev Composition

**Goal:** Make local runs mirror production topology early, establishing the dependency graph, observability spine, and configuration validation that all later stages depend on.

## Current State

The codebase already includes:
- Garnet (Redis-compatible cache) via `Aspire.StackExchange.Redis`
- Redpanda (Kafka-compatible) via KafkaFlow with `{prefix}-audio-events` and `{prefix}-track-deletions` topics
- Basic health endpoints (`/health`, `/alive`) in `ServiceDefaults`

## Aspire Host Composition

Extend `NovaTuneApp.AppHost` to add missing dependencies (`NF-1.1`):

| Dependency | Integration                                    | Notes                                                 |
|------------|------------------------------------------------|-------------------------------------------------------|
| RavenDB    | `Aspire.Hosting.RavenDB` or custom container   | System of record; configure single-node for local dev |
| MinIO      | Custom container resource                      | S3-compatible; create default bucket on startup       |
| Redpanda   | Already present                                | Verify topic auto-creation and notification wiring    |
| Garnet     | Already present                                | Verify AOF persistence enabled                        |

Tasks:
- Add RavenDB container resource with health check.
- Add MinIO container resource with default bucket provisioning.
- Verify existing Garnet and Redpanda configurations meet requirements.
- Ensure all services are independently startable for `NF-1.1` compliance.

## Health & Readiness Checks (`NF-1.2`)

Implement granular readiness logic in `ServiceDefaults` or per-service:

| Service | Liveness      | Readiness (required)          | Readiness (optional)        |
|---------|---------------|-------------------------------|-----------------------------|
| API     | Process alive | RavenDB, Redpanda, MinIO      | Garnet (degrade gracefully) |
| Workers | Process alive | Redpanda + role-specific deps | —                           |

Tasks:
- Add dependency-specific health checks (RavenDB connectivity, MinIO bucket access, Redpanda broker availability).
- Implement cache-optional degradation: API must remain ready if Garnet is unavailable.
- Wire checks to `/health` (liveness) and `/ready` (readiness) for Kubernetes probes.

## Configuration Validation (`NF-5.1`)

Validate required configuration at startup; fail fast on misconfiguration:

| Setting                | Validation                                         |
|------------------------|----------------------------------------------------|
| `{env}` topic prefix   | Non-empty; matches environment name                |
| Presigned URL TTL      | Positive duration; ≤ 1 hour                        |
| Cache encryption key   | Present for non-dev; meets minimum entropy         |
| Rate limit settings    | Valid numeric thresholds                           |
| Quota limits           | Positive integers for upload size, playlist count  |

Tasks:
- Implement `IStartupFilter` or `IHostedService` that validates configuration on boot.
- Log validation failures with actionable messages.
- Expose validated configuration via `/debug/config` in dev only (redacted).

## OpenAPI & Documentation

Tasks:
- Add Scalar UI for OpenAPI documentation (`stack.md`).
- Ensure OpenAPI spec is generated and accessible at `/openapi/v1.json`.
- Configure Scalar to be available in all environments (production may restrict to authenticated admins).

## Observability Baseline (`NF-4.x`)

Establish the instrumentation spine before feature work:

| Concern             | Implementation                                                       |
|---------------------|----------------------------------------------------------------------|
| Structured logging  | Serilog with JSON output; `CorrelationId` enricher                   |
| Distributed tracing | OpenTelemetry via Aspire; `traceparent` propagation                  |
| Metrics             | Request rate/latency/error via OpenTelemetry                         |
| Redaction           | Never log passwords, tokens, presigned URLs, object keys (`NF-4.5`)  |

Tasks:
- Configure Serilog with JSON formatter and correlation enrichment.
- Add OpenTelemetry exporters (Aspire dashboard for local; configurable for prod).
- Implement log redaction middleware or destructuring policy.
- Verify `X-Correlation-Id` header propagation from gateway.

## Security Baseline (`NF-3.x`)

| Concern     | Implementation                                                  |
|-------------|-----------------------------------------------------------------|
| TLS         | Aspire handles locally; document prod ingress requirements      |
| Secrets     | No secrets in repo; use environment variables or secret store   |
| Credentials | Least-privilege service accounts for each dependency            |

Tasks:
- Audit `appsettings.json` for any hardcoded secrets; move to environment variables.
- Document required secrets in `doc/runbooks/secrets.md` (placeholder).
- Ensure Docker Compose / Aspire manifest does not expose credentials.

## Resilience Scaffolding (`NF-1.4`)

Establish baseline resilience patterns (detailed tuning in later stages):

Tasks:
- Add Polly or `Microsoft.Extensions.Http.Resilience` for HTTP clients.
- Configure default timeouts per dependency class (cache: 500ms, DB: 5s, storage: 10s).
- Scaffold circuit breaker policies (can be refined per-endpoint later).
- Add bulkhead policies for concurrent dependency access.

## Acceptance Criteria

- [ ] `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost` starts all dependencies (RavenDB, MinIO, Redpanda, Garnet).
- [ ] API service becomes ready only when required dependencies are healthy.
- [ ] API service remains ready (degraded) when Garnet is unavailable.
- [ ] Invalid configuration causes startup failure with clear error message.
- [ ] Scalar UI is accessible at `/scalar` (or configured path).
- [ ] Logs are JSON-formatted with `CorrelationId` present.
- [ ] Traces appear in Aspire dashboard for API requests.
- [ ] No secrets are committed to the repository.

## Open Items

- Aspire community package for RavenDB: evaluate `Aspire.Hosting.RavenDB` maturity or implement custom container.
- MinIO bucket notification → Redpanda wiring: decide if configured in AppHost or via MinIO admin CLI on startup.
- Circuit breaker thresholds: defer concrete values to Stage 2+ when usage patterns are clearer.

## Requirements Covered

- `NF-1.1` — Kubernetes-ready, independently scalable deployments
- `NF-1.2` — Health and readiness endpoints
- `NF-1.4` — Resilience scaffolding (timeouts, circuit breakers, bulkheads)
- `NF-3.4` — Secrets management baseline
- `NF-4.1` — Structured logging with correlation
- `NF-4.2` — Metrics baseline
- `NF-4.3` — Distributed tracing
- `NF-4.5` — Log redaction
- `NF-4.6` — Observability stack (OpenTelemetry)
- `NF-5.1` — Configuration validation
