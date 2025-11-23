# Repository Guidelines

## Project Structure & Module Organization
`doc/requirements/functional.md`, `doc/requirements/non_functional.md`, and `doc/requirements/stack.md` are authoritative specs—cite their IDs in work notes. The solution is at `src/NovaTuneApp/NovaTuneApp.sln` using the standard Aspire template:
- `NovaTuneApp.ApiService` – API endpoints, entities, services, infrastructure adapters
- `NovaTuneApp.Web` – Blazor frontend
- `NovaTuneApp.AppHost` – Aspire orchestration
- `NovaTuneApp.ServiceDefaults` – Shared configuration
- `NovaTuneApp.Tests` – xUnit tests

Within `ApiService`, organize code in folders: `Models/`, `Services/`, `Endpoints/`, `Infrastructure/`.

## Build, Test, and Development Commands
Run everything from the repo root:
- `dotnet restore` – restores NuGet packages for the entire solution.
- `dotnet build` – compiles all projects with nullable + warnings-as-errors enabled.
- `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost` – launches the Aspire orchestration (all services).
- `dotnet run --project src/NovaTuneApp/NovaTuneApp.ApiService` – launches just the API.
- `dotnet test /p:CollectCoverage=true` – executes the xUnit suites and emits coverage for CI.
- `dotnet format` – enforces the shared `.editorconfig` before pushing.
Run `docker compose up infra` to start MinIO, RavenDB, NCache, and Kafka for integration tests.

## Coding Style & Naming Conventions
Target C# 12 with nullable reference types on, four-space indentation, braces on new lines, PascalCase namespaces/classes, camelCase locals, and UPPER_SNAKE_CASE constants. Keep files focused on a single class/record. Infrastructure adapters in `Infrastructure/` should implement interfaces (e.g., `IStorageService`, `ITrackRepository`) to enable mocking in tests.

## Testing Guidelines
xUnit is the standard. Name files `{Target}.Tests.cs` for units and `{Target}.IntegrationTests.cs` for scenarios that hit MinIO, RavenDB, or Kafka. Use Docker fixtures (or Dotnet Aspire) for dependencies and configure them via `appsettings.Test.json`. Mock caches/queues with in-memory doubles unless a regression requires the real service. Maintain ≥80% line coverage for `Services/` and all auth middleware, and exercise signed-URL caching logic with deterministic clock abstractions.

## Commit & Pull Request Guidelines
Follow Conventional Commits (`feat: add MinIO upload pipeline`, `fix: refresh presigned url cache`) scoped to a single requirement ID. PRs must include a summary, linked issue, screenshots or `curl` transcripts for API changes, config/env updates, and the output of `dotnet test` + `dotnet format --verify-no-changes`. GitHub Actions enforces the same pipeline, so keep workflows green before requesting review and wait for at least one reviewer familiar with the affected area.

## Security & Configuration Tips
Use `.env.example` to document required settings (MinIO, RavenDB, Kafka, NCache, JWT). Never commit real secrets—leverage `dotnet user-secrets` locally and GitHub Actions OIDC in CI. All MinIO buckets stay private; presigned URLs must have short TTLs and are cached in NCache keyed by user + track. Describe IAM/role needs in PRs whenever you add infrastructure automation.
