# Active Context

## Snapshot Date
- `2026-03-13`

## Branch / Repo State
- Branch: `main`
- Working tree is dirty, but the visible changes are outside `.memory-bank` and appear unrelated to this refresh

## Current Implementation State
- The backend is implemented as one API service, four worker services, a shared defaults project, and a static web host
- The AppHost already orchestrates cache, RavenDB, messaging, storage, workers, and frontend hosting behavior by environment
- The frontend monorepo already contains concrete player and admin apps with router, layouts, stores, and feature pages
- Unit and integration test suites exist for backend scenarios; reserved `component_tests` and `functional_tests` folders are still unused

## What Changed In This Refresh
- Replaced stale planning-era `.memory-bank` content with repo-backed notes
- Aligned frontend status with the actual Vue apps that exist under `src/NovaTuneClient/apps/*`
- Recorded version and topology details from project files instead of carrying earlier assumptions forward

## Immediate Risks / Open Questions
- Frontend testing remains light or absent despite package scripts advertising it
- Release hosting of the SPAs depends on prebuilt frontend `dist` output being present before `NovaTuneApp.Web` builds
- Version skew exists across some backend dependencies, especially `RavenDB.Client` and `KafkaFlow`
- `src/integration_tests/NovaTuneApp.IntegrationTests/TelemetryEndpointTests.cs.bak` is still present and may be leftover noise

## Good Starting Points
- API composition and cross-cutting setup: `src/NovaTuneApp/NovaTuneApp.ApiService/Program.cs`
- Environment orchestration: `src/NovaTuneApp/NovaTuneApp.AppHost/AppHost.cs`
- Player routing surface: `src/NovaTuneClient/apps/player/src/router/index.ts`
- Admin routing surface: `src/NovaTuneClient/apps/admin/src/router/index.ts`
