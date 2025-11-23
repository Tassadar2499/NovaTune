# NovaTune – Non-Functional Requirements

These guardrails complement `functional.md` and the stack defined in `stack.md` (ASP.NET Core, RavenDB, MinIO, NCache, Kafka/RabbitMQ, Dotnet Aspire, Docker/Kubernetes, GitHub Actions). Cite IDs (e.g., NF-4.2) alongside functional references in tickets and PRs.

## NF-1 Performance & Scalability
- **NF-1.1 Throughput:** Each API node must sustain 50 concurrent uploads and 200 concurrent stream requests without exceeding 70% CPU; scale out via Kubernetes HPA.
- **NF-1.2 Latency:** Upload confirmations (reqs. 2.x) should finish within 3s for <50 MB files; streaming start (reqs. 5.x) must deliver a signed URL in <500ms at p95.
- **NF-1.3 Statelessness:** ASP.NET Core services remain stateless, persisting long-lived data in RavenDB/MinIO and using NCache for ephemeral tokens so pods can be rescheduled freely.

## NF-2 Reliability & Availability
- **NF-2.1 Uptime:** Upload, streaming, and track-management endpoints target 99.5% monthly availability backed by multi-replica Kubernetes deployments.
- **NF-2.2 Resilience:** Wrap MinIO, RavenDB, and Kafka clients with exponential backoff retries and circuit breakers (Polly); surface graceful error messages if retries fail.
- **NF-2.3 Disaster recovery:** Keep infrastructure-as-code (Helm/Bicep/Terraform) committed and guarantee RavenDB snapshots (RPO 15 min) plus MinIO replication with an RTO of 4 hours.

## NF-3 Security & Compliance
- **NF-3.1 Secrets:** Store MinIO, RavenDB, Kafka, and JWT secrets in Kubernetes secrets and GitHub Actions OIDC workflows; rotate quarterly.
- **NF-3.2 Data protection:** Enforce TLS 1.2+ everywhere, enable RavenDB encryption at rest, MinIO SSE, and audit reads of private tracks.
- **NF-3.3 Privacy:** Honor data-subject deletion by cascading removals from RavenDB documents, MinIO objects, caches, and Kafka topics (compaction) within 30 days.

## NF-4 Observability & Monitoring
- **NF-4.1 Metrics:** Expose latency, throughput, cache hit rate, queue depth, and storage consumption via Dotnet Aspire/OTEL exporters.
- **NF-4.2 Logging:** Emit structured JSON logs with correlation IDs and requirement references; scrub tokens, object keys, and PII.
- **NF-4.3 Alerting:** Trigger alerts when error rate >2% over 5 minutes, MinIO disk usage hits 80%, or streaming latency doubles versus baseline; deliver alerts to on-call channels.

## NF-5 DevOps & Deployment
- **NF-5.1 CI/CD:** GitHub Actions must run `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`, container builds, and security scans before deploying.
- **NF-5.2 Environments:** Maintain dev, staging, and prod Kubernetes namespaces with equivalent configuration toggled via ConfigMaps/feature flags.
- **NF-5.3 Rollbacks:** Support blue/green or canary rollouts in Kubernetes with rollback taking <5 minutes, including cache invalidation and MinIO version pinning.

## NF-6 Data Management & Storage Hygiene
- **NF-6.1 Lifecycle rules:** Configure MinIO lifecycle policies to remove orphaned uploads within 7 days and archive analytics topics per retention policy.
- **NF-6.2 RavenDB integrity:** Any schema/index change must include backward-compatible migrations, tests, and documented rollback steps.
- **NF-6.3 Event streams:** Kafka/RabbitMQ topics require retention/compaction policies documented per environment and monitored for lag.

## NF-7 User Experience & Accessibility
- **NF-7.1 Responsiveness:** UI modules must remain usable on ≥320px screens and ensure audio controls work with keyboard/screen readers.
- **NF-7.2 Feedback:** Provide progress indicators for uploads exceeding 1s, spinner states while caches warm, and actionable errors for throttled or failed operations.
- **NF-7.3 Localization-ready:** Keep UI copy in resource files and avoid hardcoded date/time formats to ease localization.

## NF-8 Maintainability & Documentation
- **NF-8.1 Solution hygiene:** Organize code into a single `.sln` with `NovaTune.Api`, `NovaTune.Application`, `NovaTune.Domain`, `NovaTune.Infrastructure`, and `tests/NovaTune.Tests`. Keep functions under ~40 lines and enforce dependency boundaries.
- **NF-8.2 Testing:** All new features require xUnit coverage (unit + integration) with deterministic fakes for MinIO/RavenDB/Kafka. Collect coverage via `dotnet test /p:CollectCoverage=true` in CI.
- **NF-8.3 Documentation freshness:** Update `README.md`, `functional.md`, and `stack.md` when dependencies or architecture change, and attach architecture decision records for major choices.
