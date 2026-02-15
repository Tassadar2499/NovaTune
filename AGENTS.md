# Repository Guidelines

## Project Structure & Module Organization
- `src/NovaTuneApp/` contains the .NET solution (`NovaTuneApp.sln`).
- Core backend projects:
  - `NovaTuneApp.ApiService`: API endpoints, domain models, validation, and services.
  - `NovaTuneApp.AppHost`: .NET Aspire composition for local orchestration.
  - `NovaTuneApp.ServiceDefaults`: shared hosting/logging defaults.
  - `NovaTuneApp.Workers.*`: background workers (`AudioProcessor`, `UploadIngestor`, `Lifecycle`, `Telemetry`).
  - `NovaTuneApp.Web`: ASP.NET Core web host and static assets.
- Frontend workspace: `src/NovaTuneClient/` (`apps/admin`, `apps/player`, shared `packages/*`).
- Tests:
  - .NET unit tests in `src/unit_tests/`
  - .NET integration tests in `src/integration_tests/`
  - Reserved folders: `src/component_tests/`, `src/functional_tests/`
- Docs live in `doc/`.

## Build, Test, and Development Commands
- Restore/build .NET:
  - `dotnet restore src/NovaTuneApp/NovaTuneApp.sln`
  - `dotnet build src/NovaTuneApp/NovaTuneApp.sln -c Release`
- Run backend stack:
  - `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost/NovaTuneApp.AppHost.csproj`
- Run .NET tests:
  - `dotnet test src/NovaTuneApp/NovaTuneApp.sln -c Debug`
- Frontend workspace (`src/NovaTuneClient`):
  - `pnpm dev:admin` / `pnpm dev:player`
  - `pnpm build`, `pnpm test`, `pnpm lint`, `pnpm typecheck`

## Coding Style & Naming Conventions
- Follow `.editorconfig`: 4-space indent, LF, UTF-8, final newline.
- C# uses Allman braces and prefers `var` when type is obvious.
- Naming:
  - constants/static readonly: `PascalCase`
  - private instance fields: `_camelCase`
- Nullable safety is strict; CS8600-CS8604 are treated as errors.
- Use 2-space indentation for JSON/YAML.

## Testing Guidelines
- .NET tests use xUnit + Shouldly.
- Name files `*Tests.cs`; use behavior-style test names (for example, `Should_Reject_InvalidMetadata()`).
- Keep tests deterministic and isolated; add coverage for new domain logic and endpoint validation.

## Commit & Pull Request Guidelines
- Commit messages should be imperative, present tense, and concise (for example, `Add telemetry aggregation endpoint`).
- PRs should include:
  - change summary
  - test evidence (`dotnet test`, `pnpm test`, etc.)
  - linked issue/ticket (if available)
  - screenshots or API examples for UI/contract changes
- Keep PRs focused; avoid mixing refactors with feature work.

## Security & Configuration Tips
- Do not commit secrets, credentials, or connection strings.
- Prefer environment variables and local secrets over editing committed config files.
- Add validation attributes for new request models to keep model binding and tests consistent.
