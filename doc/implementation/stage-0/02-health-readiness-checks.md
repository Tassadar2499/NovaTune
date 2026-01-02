# Health & Readiness Checks (`NF-1.2`)

Implement granular readiness logic in `ServiceDefaults` or per-service:

| Service | Liveness      | Readiness (required)          | Readiness (optional)        |
|---------|---------------|-------------------------------|-----------------------------|
| API     | Process alive | RavenDB, Redpanda, MinIO      | Garnet (degrade gracefully) |
| Workers | Process alive | Redpanda + role-specific deps | —                           |

## Tasks

- Add dependency-specific health checks (RavenDB connectivity, MinIO bucket access, Redpanda broker availability).
- Implement cache-optional degradation: API must remain ready if Garnet is unavailable.
- Wire checks to `/health` (liveness) and `/ready` (readiness) for Kubernetes probes.
