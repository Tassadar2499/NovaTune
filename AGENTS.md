# Repository Guidelines

## Project Structure & Modules
- `src/NovaTuneApp/NovaTuneApp.AppHost`: Aspire host that wires the API and web app together for local runs.
- `src/NovaTuneApp/NovaTuneApp.ApiService`: Core API/domain models; validation attributes live here.
- `src/NovaTuneApp/NovaTuneApp.Web`: ASP.NET Core front end; UI components in `Components/`, static assets in `wwwroot/`, configuration in `appsettings*.json`.
- `src/NovaTuneApp/NovaTuneApp.ServiceDefaults`: Shared service defaults (logging, hosting).
- Tests: unit specs in `src/unit_tests/` (xUnit + Shouldly); integration stubs in `src/integration_tests/`; functional/component test folders are reserved for future suites.
- Docs: high-level notes live in `doc/`; repository guide is `AGENTS.md`.

## Build, Test, and Development Commands
- Restore deps: `dotnet restore src/NovaTuneApp/NovaTuneApp.sln`
- Build all projects: `dotnet build src/NovaTuneApp/NovaTuneApp.sln -c Release`
- Run the composed app: `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost/NovaTuneApp.AppHost.csproj`
- Run only the web front end: `dotnet run --project src/NovaTuneApp/NovaTuneApp.Web/NovaTuneApp.Web.csproj`
- Execute tests: `dotnet test src/NovaTuneApp/NovaTuneApp.sln -c Debug`

## Coding Style & Naming
- Indent with 4 spaces; LF line endings; UTF-8; final newline (see `.editorconfig`).
- Allman braces; prefer `var` when the type is apparent.
- Constants and static readonly fields use PascalCase; private instance fields use `_camelCase`.
- Enable nullable reference warnings; treat CS8600–CS8604 as errors.
- JSON/YAML files use 2-space indents. Avoid trailing whitespace; Markdown may retain it for formatting.

## Testing Guidelines
- Frameworks: xUnit with Shouldly assertions.
- Test file pattern: `*Tests.cs` grouped by subject (e.g., `Models/TrackTests.cs`).
- Name tests in behavior style: `Should_<expected_behavior>()`.
- Add unit coverage for new domain rules; prefer integration tests under `src/integration_tests/` when exercising the Aspire host. Keep tests deterministic and data-independent.

## Commit & Pull Request Guidelines
- Commit messages follow the recent history: imperative, present tense, capitalized first word; keep them concise (e.g., “Add validation for track metadata”).
- PRs should include: summary of change, testing notes (`dotnet test` output or failures), linked issue/clubhouse ticket if applicable, and screenshots or cURL snippets for UI/API changes.
- Keep PRs small and focused; separate refactors from feature work when practical. Request review once the branch builds and tests are clean locally.

## Security & Configuration Tips
- Do not commit secrets or connection strings; prefer user secrets or environment variables over editing `appsettings*.json`.
- When adding new endpoints, ensure validation attributes cover required fields to keep model binding consistent with existing tests. 
