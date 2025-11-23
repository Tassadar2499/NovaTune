# Repository Guidelines

## Project Structure & Module Organization
`doc/requirements/functional.md`, `doc/requirements/non_functional.md`, and `doc/requirements/stack.md` are authoritative specs—cite their IDs in work notes. Place runtime code under `src/` with a `.sln` containing `NovaTune.Api` (ASP.NET Core endpoints), `NovaTune.Application` (use cases), `NovaTune.Domain` (entities/validation), and `NovaTune.Infrastructure` (RavenDB/MinIO/Kafka/NCache adapters). Keep xUnit projects inside `tests/` mirroring their namespace and log new modules in `README.md`.

## Build, Test, and Development Commands
Run everything from the repo root:
- `dotnet restore` – restores NuGet packages for the entire solution.
- `dotnet build` – compiles all projects with nullable + warnings-as-errors enabled.
- `dotnet run --project src/NovaTune.Api` – launches the API locally using `.env.development` or user-secrets for config.
- `dotnet test /p:CollectCoverage=true` – executes the xUnit suites and emits coverage for CI.
- `dotnet format` – enforces the shared `.editorconfig` before pushing.
Run `docker compose up infra` to start MinIO, RavenDB, NCache, and Kafka for integration tests.

## Coding Style & Naming Conventions
Target C# 12 with nullable reference types on, four-space indentation, braces on new lines, PascalCase namespaces/classes, camelCase locals, and UPPER_SNAKE_CASE constants. Keep files focused on a single class/record. Infrastructure adapters should expose interfaces under `NovaTune.Application.Abstractions` so implementations can swap between MinIO/RavenDB mocks and production drivers.

## Testing Guidelines
xUnit is the standard. Name files `{Target}.Tests.cs` for units and `{Target}.IntegrationTests.cs` for scenarios that hit MinIO, RavenDB, or Kafka. Use Docker fixtures (or Dotnet Aspire) for dependencies and configure them via `appsettings.Test.json`. Mock caches/queues with in-memory doubles unless a regression requires the real service. Maintain ≥80% line coverage for `NovaTune.Application` and all auth middleware, and exercise signed-URL caching logic with deterministic clock abstractions.

## Commit & Pull Request Guidelines
Follow Conventional Commits (`feat: add MinIO upload pipeline`, `fix: refresh presigned url cache`) scoped to a single requirement ID. PRs must include a summary, linked issue, screenshots or `curl` transcripts for API changes, config/env updates, and the output of `dotnet test` + `dotnet format --verify-no-changes`. GitHub Actions enforces the same pipeline, so keep workflows green before requesting review and wait for at least one reviewer familiar with the affected area.

## Security & Configuration Tips
Use `.env.example` to document required settings (MinIO, RavenDB, Kafka, NCache, JWT). Never commit real secrets—leverage `dotnet user-secrets` locally and GitHub Actions OIDC in CI. All MinIO buckets stay private; presigned URLs must have short TTLs and are cached in NCache keyed by user + track. Describe IAM/role needs in PRs whenever you add infrastructure automation.
