# Task 1.3: Docker Compose Infrastructure

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Pending
> **Estimated Effort:** 4-6 hours

## Overview

This task establishes the local development infrastructure using Docker Compose. It creates a consistent, reproducible environment for all NovaTune dependencies, enabling developers to quickly spin up the required services without manual installation.

## Objectives

- Containerize all infrastructure dependencies (RavenDB, MinIO, Kafka, RabbitMQ, NCache)
- Provide consistent development environment across team members
- Enable rapid onboarding for new developers
- Support integration testing with real services

## Prerequisites

- Docker Desktop 4.x+ or Docker Engine 24.x+
- Docker Compose V2 (included with Docker Desktop)
- Minimum 8 GB RAM available for containers
- 20 GB free disk space

## Steps

| Step | Title | Description | File |
|------|-------|-------------|------|
| 1.3.1 | [Docker Compose File](step-1.3.1-docker-compose.md) | Create main `docker-compose.yml` with all services | `docker-compose.yml` |
| 1.3.2 | [Docker Compose Override](step-1.3.2-docker-compose-override.md) | Create development-specific overrides | `docker-compose.override.yml` |
| 1.3.3 | [Environment Variables](step-1.3.3-environment-variables.md) | Create `.env.example` template | `.env.example` |
| 1.3.4 | [Resource Requirements](step-1.3.4-resource-requirements.md) | Document system requirements | Documentation |
| 1.3.5 | [Health Check Scripts](step-1.3.5-health-check-scripts.md) | Create service health verification | `scripts/healthcheck.sh` |
| 1.3.6 | [Wait Scripts](step-1.3.6-wait-scripts.md) | Create startup synchronization | `scripts/wait-for-services.sh` |
| 1.3.7 | [Dev Containers](step-1.3.7-dev-containers.md) | Configure JetBrains Rider Dev Containers | `.devcontainer/` |

## Acceptance Criteria

- [ ] `docker compose up` starts all services without errors
- [ ] All services pass health checks within 60 seconds
- [ ] `.env.example` documents all required variables
- [ ] README includes startup instructions
- [ ] Dev container works with JetBrains Rider

## Verification Commands

```bash
# Start all services in detached mode
docker compose up -d

# Check service status
docker compose ps

# Run health checks
./scripts/healthcheck.sh

# View service logs
docker compose logs -f

# Stop all services
docker compose down

# Stop and remove volumes (clean slate)
docker compose down -v
```

## File Checklist

| File | Location | Status |
|------|----------|--------|
| `docker-compose.yml` | Repository root | [ ] |
| `docker-compose.override.yml` | Repository root | [ ] |
| `.env.example` | Repository root | [ ] |
| `healthcheck.sh` | `scripts/` | [ ] |
| `wait-for-services.sh` | `scripts/` | [ ] |
| `devcontainer.json` | `.devcontainer/` | [ ] |

## Dependencies

- **Depends on:** Task 1.1 (Solution Structure) - Solution must exist
- **Blocks:** Task 1.4 (Aspire AppHost) - Aspire will reference these services

## Related Requirements

- **NF-8.1:** Development Environment
- **NF-8.3:** Container Support

## Navigation

[Step 1.3.1: Docker Compose File](step-1.3.1-docker-compose.md) | [Phase 1 Overview](../overview.md) | [Task 1.4: Aspire AppHost](../task-1.4-aspire-apphost.md)
