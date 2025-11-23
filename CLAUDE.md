# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NovaTune is a distributed audio management platform built on .NET 9.0 using ASP.NET Core microservices orchestrated via Dotnet Aspire. The project follows a layered architecture with clean separation between API, Application, Domain, and Infrastructure layers.

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

The planned architecture follows layered design:
- `NovaTune.Api` - ASP.NET Core endpoints
- `NovaTune.Application` - Use cases and business logic
- `NovaTune.Domain` - Entities and validation rules
- `NovaTune.Infrastructure` - External service adapters (RavenDB, MinIO, NCache, Kafka)

Infrastructure adapters should expose interfaces under `NovaTune.Application.Abstractions` for testability.

## Tech Stack

- **Runtime:** .NET 9.0 / C# 12
- **Orchestration:** Dotnet Aspire 13.0
- **Test Framework:** xUnit 2.9.3
- **Database:** RavenDB (document store)
- **Object Storage:** MinIO (S3-compatible)
- **Caching:** NCache
- **Messaging:** Apache Kafka, RabbitMQ
- **Observability:** OpenTelemetry with OTLP export
- **Frontend:** Vue.js + TypeScript (planned)

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
- Target: ≥80% line coverage for Application layer and auth middleware

## Key Documentation

- `AGENTS.md` - Authoritative repository guidelines
- `doc/requirements/functional.md` - Functional requirements (FR 1-11)
- `doc/requirements/non_functional.md` - Non-functional requirements (NF-1 to NF-8)
- `doc/requirements/stack.md` - Technology stack specification

## Commit Guidelines

Follow Conventional Commits scoped to a single requirement ID:
- `feat: add MinIO upload pipeline`
- `fix: refresh presigned url cache`

PRs require: test output, format verification, and curl transcripts/screenshots for API changes.