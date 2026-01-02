# Stage 0 — Infrastructure & Local Dev Composition

**Goal:** Make local runs mirror production topology early.

## Tasks

- Extend `NovaTuneApp.AppHost` to include RavenDB + MinIO + Redpanda + Garnet.
- Add per-service health endpoints and readiness checks (`NF-1.2`):
  - API ready: RavenDB + Redpanda required; MinIO required for upload initiation/stream URL issuance; cache optional.
  - Worker ready: Redpanda + worker-specific dependencies.
- Add configuration validation at startup (`NF-5.1`): `{env}` topic prefix, TTLs, quotas, crypto keys, rate limit settings.
- Add OpenAPI surface and UI (Scalar per `stack.md`) and ensure it's wired in non-dev as appropriate.

## Requirements Covered

- `NF-1.1`
- `NF-1.2`
- `NF-5.1`
- `NF-4.x`
