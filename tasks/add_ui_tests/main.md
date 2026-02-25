# UI Tests Implementation Plan (C# + Selenium + xUnit)

## Task Summary
Implement UI tests in `src/ui_tests` using C#, Selenium WebDriver, and xUnit for the NovaTune player and admin applications.

## Current State Analysis
- `src/ui_tests` exists but is empty.
- The solution already has a logical `ui_tests` solution folder in `src/NovaTuneApp/NovaTuneApp.sln`, but no UI test project is attached.
- Player and admin frontends run as Vite apps from `src/NovaTuneClient/apps/player` and `src/NovaTuneClient/apps/admin`.
- Existing backend integration tests already use `Aspire.Hosting.Testing` and RavenDB seeding patterns that can be reused.

## Technology Decision
Use:
- C# (`net9.0`)
- xUnit test framework
- Selenium WebDriver (Chrome/Chromium)
- Aspire test host for backend API orchestration

Why:
- Aligns with repository backend test conventions.
- Keeps UI tests in a .NET test project (`dotnet test` workflow).
- Avoids introducing a second UI test runtime stack for this task.

## Target Project Layout

### New project
- `src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj`

### Suggested folders/files
- `src/ui_tests/NovaTuneApp.UiTests/Fixtures/UiTestHostFixture.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Fixtures/WebDriverFixture.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Infrastructure/UiTestDataSeeder.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Infrastructure/WaitHelpers.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Infrastructure/ViteProcessManager.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Player/PlayerAuthTests.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Player/PlayerLibraryTests.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Player/PlayerPlaylistsTests.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Admin/AdminAuthTests.cs`
- `src/ui_tests/NovaTuneApp.UiTests/Admin/AdminDashboardTests.cs`
- `src/ui_tests/NovaTuneApp.UiTests/xunit.runner.json`

## Test Environment Strategy

### Backend
- Start backend through `Aspire.Hosting.Testing` with `--environment=Testing`.
- Reuse the same test-safe app host argument approach as integration tests (high rate limits, test JWT config, etc.).

### Frontend
- Start Vite apps as child processes in fixture setup:
  - Player: port `25173`
  - Admin: port `25174`
- Inject env var `VITE_API_BASE_URL` to target the test API endpoint from Aspire.

### Browser
- Use Selenium ChromeDriver via Selenium Manager (headless by default in CI).
- Keep a non-headless switch through environment variable for local debugging.

## Project Dependencies (`.csproj`)
Recommended package references:
- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `Selenium.WebDriver`
- `Selenium.Support`
- `Aspire.Hosting.Testing`
- `Shouldly` (optional but consistent with existing tests)

Recommended project references:
- `src/NovaTuneApp/NovaTuneApp.AppHost/NovaTuneApp.AppHost.csproj`
- `src/NovaTuneApp/NovaTuneApp.ApiService/NovaTuneApp.ApiService.csproj`

## Test Scenario Matrix

### Player base scenarios
1. Unauthenticated user navigating to `/` is redirected to `/auth/login`.
2. Valid login redirects to `/` and renders `My Library`.
3. Invalid login keeps user on login page and shows `Login failed`.
4. Library page renders seeded tracks list for authenticated user.
5. Playlists page renders empty state for new user.
6. Create playlist flow creates playlist and renders it in the list.

### Admin base scenarios
1. Unauthenticated user navigating to `/admin/` is redirected to `/admin/auth/login`.
2. Admin login redirects to dashboard route (`/admin/`).
3. Non-admin login attempt shows `Admin access required`.
4. Dashboard page renders analytics cards with seeded metrics.

## Implementation Phases

### Phase 1: Bootstrap UI Test Project
Files:
- `src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj`
- `src/ui_tests/NovaTuneApp.UiTests/xunit.runner.json`

Tasks:
- Create `net9.0` xUnit project in `src/ui_tests`.
- Add Selenium and Aspire test dependencies.
- Add project to solution and nest under `ui_tests` solution folder.

Acceptance:
- `dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj --list-tests` succeeds.

### Phase 2: Test Host and Browser Fixtures
Files:
- `Fixtures/UiTestHostFixture.cs`
- `Fixtures/WebDriverFixture.cs`
- `Infrastructure/ViteProcessManager.cs`
- `Infrastructure/WaitHelpers.cs`

Tasks:
- Build shared fixture that:
  - starts Aspire test app host in Testing mode,
  - starts player/admin Vite processes,
  - waits for HTTP readiness,
  - provides base URLs and API client.
- Build WebDriver fixture with reliable waits and cleanup.

Acceptance:
- A smoke test can open both app URLs and assert login page renders.

### Phase 3: Deterministic Data Seeding
Files:
- `Infrastructure/UiTestDataSeeder.cs`

Tasks:
- Implement test data seeding helpers for:
  - users (regular + admin),
  - tracks for player library,
  - analytics-friendly admin data.
- Reuse RavenDB document seeding patterns from existing integration tests.
- Ensure per-test cleanup/reset to keep tests isolated.

Acceptance:
- Tests can run repeatedly with stable results and no cross-test leakage.

### Phase 4: Player UI Tests
Files:
- `Player/PlayerAuthTests.cs`
- `Player/PlayerLibraryTests.cs`
- `Player/PlayerPlaylistsTests.cs`

Tasks:
- Implement player scenario matrix.
- Use robust selectors (`id`, `data-testid`, visible text) and explicit waits.
- Avoid brittle timing assumptions (`Thread.Sleep` not allowed).

Acceptance:
- Player test suite passes consistently in local headless runs.

### Phase 5: Admin UI Tests
Files:
- `Admin/AdminAuthTests.cs`
- `Admin/AdminDashboardTests.cs`

Tasks:
- Implement admin scenario matrix.
- Validate role-gated login behavior.
- Validate dashboard loads post-auth and displays seeded values.

Acceptance:
- Admin test suite passes consistently in local headless runs.

### Phase 6: Workflow Integration
Files:
- `src/NovaTuneClient/package.json` (optional convenience script)
- `.gitignore`
- `src/ui_tests/NovaTuneApp.UiTests/README.md`

Tasks:
- Add docs for local execution and troubleshooting.
- Ignore Selenium artifacts (screenshots/logs on failure) and temp outputs.
- Optionally add root-level convenience command for UI tests.

Acceptance:
- One documented command path for developers and CI.

## Dependency Graph
- Phase 1 is prerequisite for all phases.
- Phase 2 depends on Phase 1.
- Phase 3 depends on Phase 2.
- Phase 4 and Phase 5 depend on Phase 3 and can run in parallel.
- Phase 6 depends on completion of Phases 4 and 5.

## Execution Order
1. Phase 1
2. Phase 2
3. Phase 3
4. Phase 4 + Phase 5 (parallel)
5. Phase 6

## Validation Commands
- `dotnet restore src/NovaTuneApp/NovaTuneApp.sln`
- `dotnet build src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug`
- `dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug`
- `dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug --filter "FullyQualifiedName~Player"`
- `dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug --filter "FullyQualifiedName~Admin"`

## Risks and Mitigations
- Risk: flaky Selenium timing due to dynamic SPA rendering.
  - Mitigation: centralize explicit wait helpers and URL/DOM readiness checks.
- Risk: local environment differences (Chrome path, pnpm availability).
  - Mitigation: validate prerequisites in fixture startup and fail fast with clear errors.
- Risk: Vite process leaks on test failures.
  - Mitigation: fixture-level process tracking and guaranteed teardown in `DisposeAsync`.
- Risk: slow execution if full backend is initialized per test class.
  - Mitigation: use shared collection fixture and reset data between tests rather than restarting host.

## Definition of Done
- `src/ui_tests` contains a runnable C# Selenium xUnit UI test project.
- Player and admin base UI scenarios are covered.
- Tests execute via `dotnet test` with deterministic setup/teardown.
- Documentation explains local run steps and prerequisites.
