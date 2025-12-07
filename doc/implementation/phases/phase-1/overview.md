# Phase 1: Infrastructure & Domain Foundation

> **Status:** Pending
> **Dependencies:** None (foundational phase)
> **Milestone:** M1 - Foundation

## Objective

Configure the existing Aspire project structure with infrastructure dependencies and define core domain entities. Keep it simple - avoid premature abstraction.

---

## Quick Links

| Task | Title | Priority |
|------|-------|----------|
| [Task 1.1](task-1.1-project-structure.md) | Project Structure Setup | P1 (Must-have) |
| [Task 1.2](task-1.2-domain-entities.md) | Core Domain Entities | P1 (Must-have) |
| [Task 1.3](task-1.3-docker-compose.md) | Docker Compose Infrastructure | P1 (Must-have) |
| [Task 1.4](task-1.4-aspire-apphost.md) | Aspire AppHost Configuration | P1 (Must-have) |
| [Task 1.5](task-1.5-service-defaults.md) | ServiceDefaults Configuration | P1 (Must-have) |
| [Task 1.6](task-1.6-security-headers.md) | HTTP Security Headers | P1 (Must-have) |
| [Task 1.7](task-1.7-api-foundation.md) | API Foundation | P1 (Must-have) |
| [Task 1.8](task-1.8-secrets-management.md) | Secrets Management | P1 (Must-have) |
| [Task 1.9](task-1.9-ffmpeg-image.md) | FFmpeg Base Image | P2 (Should-have) |
| [Task 1.10](task-1.10-ci-pipeline.md) | CI Pipeline Foundation | P2 (Should-have) |

---

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-3.1 | Secrets Management | Configure `dotnet user-secrets` for local dev |
| NF-3.6 | HTTP Security Headers | Configure HSTS, CSP, X-Frame-Options, X-Content-Type-Options |
| NF-8.1 | Solution Hygiene | Organize folders within existing Aspire projects |
| NF-8.4 | API Documentation | Set up Scalar OpenAPI infrastructure |
| NF-9.1 | API Versioning | Establish `/api/v1/` convention |
| NF-9.3 | Service Discovery | Configure Dotnet Aspire orchestration |

---

## Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Entity validation | Core validation rules |
| Integration | Aspire orchestration | All services start and communicate |
| Integration | Health endpoints | Return 200 when healthy |

---

## Exit Criteria

- [ ] `dotnet build` succeeds with warnings-as-errors
- [ ] `dotnet test` passes all tests
- [ ] `dotnet format --verify-no-changes` passes
- [ ] Aspire dashboard shows all services healthy
- [ ] API returns 200 on `/health` endpoint
- [ ] Scalar UI accessible at `/docs`
- [ ] Docker Compose starts all infrastructure services
- [ ] Security headers present in all responses

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Aspire version incompatibility | High | Pin Aspire 13.0 in `global.json` |
| Docker resource constraints | Medium | Document minimum resource requirements (8GB RAM) |
| Infrastructure service startup order | Medium | Use Aspire health checks and wait strategies |

---

## Future Considerations

If codebase complexity grows significantly (Phase 6+), consider extracting to layered architecture:
- `NovaTune.Domain` - Pure domain entities
- `NovaTune.Application` - Use cases and abstractions
- `NovaTune.Infrastructure` - External adapters

For now, keeping everything in `NovaTuneApp.ApiService` with clear folder boundaries is sufficient.

---

## Navigation

[Overview](../overview.md) | [Phase 2: User Management](../phase-2-user-management.md)
