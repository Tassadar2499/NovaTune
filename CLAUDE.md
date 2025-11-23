# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NovaTune is a distributed audio management platform built on .NET 9.0 using ASP.NET Core microservices orchestrated via Dotnet Aspire. The project uses the standard Aspire template structure with clear folder organization within ApiService.

## Build and Development Commands

Run all commands from the repository root:

```bash
# Restore packages
dotnet restore

# Build solution (nullable + warnings-as-errors enabled)
dotnet build

# Run the Aspire orchestration host (starts all services locally)
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost

# Run API service standalone
dotnet run --project src/NovaTuneApp/NovaTuneApp.ApiService

# Run tests with coverage
dotnet test /p:CollectCoverage=true

# Format code
dotnet format

# Verify formatting (used in CI)
dotnet format --verify-no-changes

# Start infrastructure dependencies for integration tests
docker compose up infra
```

## Solution Structure

The solution is located at `src/NovaTuneApp/NovaTuneApp.sln` with these projects:

- **NovaTuneApp.ApiService** - ASP.NET Core REST API endpoints
- **NovaTuneApp.Web** - Blazor interactive web frontend
- **NovaTuneApp.AppHost** - Dotnet Aspire orchestration host
- **NovaTuneApp.ServiceDefaults** - Shared configuration (OpenTelemetry, service discovery, resilience, health checks)
- **NovaTuneApp.Tests** - xUnit integration tests using Aspire testing infrastructure

## Architecture

The project uses a simplified structure within `NovaTuneApp.ApiService`:

```
NovaTuneApp.ApiService/
├── Models/           # Entities (User, Track, AudioMetadata)
├── Services/         # Business logic (AuthService, TrackService, etc.)
├── Endpoints/        # Minimal API route definitions
└── Infrastructure/   # External adapters (MinIO, RavenDB, Kafka, NCache)
```

This approach avoids premature abstraction while maintaining clear boundaries. If complexity grows significantly, consider extracting to separate projects (Domain, Application, Infrastructure) later.

## Tech Stack

- **Runtime:** .NET 9.0 / C# 12
- **Orchestration:** Dotnet Aspire 13.0
- **Database:** RavenDB (sole document store; custom IUserStore/IRoleStore for ASP.NET Identity)
- **Object Storage:** MinIO (S3-compatible)
- **Caching:** NCache (presigned URLs, session state)
- **Messaging:** Apache Kafka (event streaming, analytics), RabbitMQ (task queues)
- **Auth:** ASP.NET Identity with JWT + refresh tokens
- **Audio:** FFmpeg/FFprobe (via base Docker image)
- **Gateway:** YARP (reverse proxy)
- **API Docs:** Scalar (OpenAPI UI)
- **Logging:** Serilog (structured JSON with correlation IDs)
- **Observability:** OpenTelemetry (metrics, traces via Aspire)
- **Testing:** xUnit, Testcontainers
- **Frontend:** Vue.js + TypeScript

## Code Style

- C# 12 with nullable reference types enabled
- Four-space indentation, braces on new lines (Allman style)
- PascalCase for namespaces/classes, camelCase for locals, UPPER_SNAKE_CASE for constants
- One class/record per file
- Warnings treated as errors

## Testing

- Name unit tests: `{Target}.Tests.cs`
- Name integration tests: `{Target}.IntegrationTests.cs`
- Use Docker fixtures or Dotnet Aspire for external dependencies
- Configure tests via `appsettings.Test.json`
- Target: ≥80% line coverage for Services and auth middleware

## Key Documentation

- [AGENTS.md](AGENTS.md) - Authoritative repository guidelines
- [Functional Requirements](doc/requirements/functional.md) - FR 1-11
- [Non-Functional Requirements](doc/requirements/non_functional.md) - NF-1 to NF-8
- [Technology Stack](doc/requirements/stack.md) - Stack specification
- [Implementation Plan](doc/implementation/init.md) - 8-phase roadmap

## Commit Guidelines

Follow Conventional Commits scoped to a single requirement ID:
- `feat: add MinIO upload pipeline`
- `fix: refresh presigned url cache`

PRs require: test output, format verification, and curl transcripts/screenshots for API changes.