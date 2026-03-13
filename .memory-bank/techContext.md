# Technical Context

## Backend Stack
- `.NET`: `net9.0`
- `Aspire AppHost SDK`: `13.0.0`
- `ASP.NET Core OpenAPI`: `9.0.11`
- `Scalar.AspNetCore`: `2.0.36`
- `Serilog.AspNetCore`: `9.0.0`
- `KafkaFlow`: API `3.0.10`, workers `3.1.0`
- `RavenDB.Client`: API/tests `6.2.4`, workers `7.0.2`
- `Minio`: `6.0.3`
- `StackExchange.Redis`: API `2.8.16`, integration tests `2.9.32`
- `Isopoh.Cryptography.Argon2`: `2.0.0`
- `Microsoft.Extensions.Http.Resilience`: `10.0.0`
- `OpenTelemetry`: `1.13.x` via `NovaTuneApp.ServiceDefaults`

## Frontend Stack
- `Node`: `>=20`
- `pnpm`: `>=9`
- `Vue`: `3.5.x`
- `Vue Router`: `4.4.x`
- `Pinia`: `2.2.x`
- `@tanstack/vue-query`: `5.x`
- `Vite`: `6.x`
- `TypeScript`: `5.6.x`
- `TailwindCSS`: `3.4.x`
- `Headless UI`: `1.7.x`
- `Orval`: `7.x`
- `Axios`: `1.7.x`
- `Vitest`: `2.x`
- `Playwright`: `1.45.x` in `apps/player`
- `Playwright UI tests`: standalone workspace package at `src/ui_tests/host`

## Commands
```bash
# Backend
dotnet restore src/NovaTuneApp/NovaTuneApp.sln
dotnet build src/NovaTuneApp/NovaTuneApp.sln -c Release
dotnet test src/NovaTuneApp/NovaTuneApp.sln -c Debug
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost/NovaTuneApp.AppHost.csproj

# Frontend
cd src/NovaTuneClient
pnpm dev:player
pnpm dev:admin
pnpm build
pnpm test
pnpm lint
pnpm typecheck
pnpm generate
```

## Repository Layout
```text
src/NovaTuneApp/
  NovaTuneApp.AppHost/
  NovaTuneApp.ApiService/
  NovaTuneApp.ServiceDefaults/
  NovaTuneApp.Web/
  NovaTuneApp.Workers.UploadIngestor/
  NovaTuneApp.Workers.AudioProcessor/
  NovaTuneApp.Workers.Lifecycle/
  NovaTuneApp.Workers.Telemetry/
src/NovaTuneClient/
  apps/player
  apps/admin
  packages/api-client
  packages/core
  packages/ui
src/ui_tests/
  host
src/unit_tests/
src/integration_tests/
doc/
```

## Operational Notes
- API OpenAPI target for Orval is currently `http://localhost:5000/openapi/v1.json`
- Dev frontend ports are `25173` for player and `25174` for admin
- `NovaTuneApp.Web` copies `apps/player/dist` into `wwwroot` and `apps/admin/dist` into `wwwroot/admin` for Release builds
- Testing mode disables messaging and keeps storage enabled with a MinIO test container

## Notable Drift / Follow-Up
- RavenDB client versions differ between API/tests and worker projects
- KafkaFlow versions also differ between API and workers
- Frontend scripts advertise tests, but matching frontend test files were not found during this refresh
