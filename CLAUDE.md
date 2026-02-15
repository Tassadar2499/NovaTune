# CLAUDE.md

Guidance for Claude Code when working with the NovaTune repository.

## Build and Run Commands

```bash
# Build the .NET solution
dotnet build src/NovaTuneApp/NovaTuneApp.sln

# Run the full stack (Aspire orchestrates all services, infra, and Vite dev servers)
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost

# Run all backend tests
dotnet test src/NovaTuneApp/NovaTuneApp.sln

# Run unit tests only
dotnet test src/unit_tests/NovaTune.UnitTests.csproj

# Run integration tests only
dotnet test src/integration_tests/NovaTuneApp.IntegrationTests/NovaTuneApp.IntegrationTests.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TrackTests.Should_pass_with_valid_data"

# Frontend (from src/NovaTuneClient/)
pnpm install                              # install all workspace deps
pnpm --filter player dev                  # player dev server (port 25173)
pnpm --filter admin dev                   # admin dev server (port 25174)
pnpm lint                                 # lint all packages
pnpm typecheck                            # typecheck all packages
pnpm --filter @novatune/api-client generate  # regenerate API client (Orval)
pnpm test                                 # run all frontend tests (Vitest)
```

## Architecture Overview

NovaTune is an event-driven audio streaming platform built on .NET 9 Aspire. The API service handles HTTP requests and publishes events via Redpanda (Kafka). Four specialized workers consume those events for async processing (upload ingestion, audio processing, lifecycle management, telemetry aggregation). RavenDB is the sole database; Garnet provides caching; MinIO stores audio files.

```
                      ┌──────────────┐
                      │   Clients    │
                      │ Player/Admin │
                      └──────┬───────┘
                             │
                      ┌──────▼───────┐     ┌─────────┐
                      │  ApiService  ├────►│ Garnet  │ (cache)
                      │   (HTTP)     ├────►│ RavenDB │ (database)
                      │              ├────►│  MinIO  │ (storage)
                      └──────┬───────┘     └─────────┘
                             │ Kafka events
              ┌──────────────┼──────────────┬──────────────┐
              ▼              ▼              ▼              ▼
     ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐
     │  Upload    │ │   Audio    │ │ Lifecycle  │ │ Telemetry  │
     │ Ingestor   │ │ Processor  │ │  Worker    │ │  Worker    │
     └────────────┘ └────────────┘ └────────────┘ └────────────┘
```

### Projects

| Project | Description |
|---|---|
| `NovaTuneApp.AppHost` | Aspire orchestration — configures and launches all services and infra |
| `NovaTuneApp.ServiceDefaults` | Shared config: OpenTelemetry, health checks, service discovery, resilience |
| `NovaTuneApp.ApiService` | Main API: auth, uploads, streaming, tracks, playlists, telemetry, admin |
| `NovaTuneApp.Web` | Production static file server for the Vue frontends |
| `NovaTuneApp.Workers.UploadIngestor` | Consumes MinIO bucket notifications, creates Track documents |
| `NovaTuneApp.Workers.AudioProcessor` | Extracts metadata, generates waveforms from uploaded audio |
| `NovaTuneApp.Workers.Lifecycle` | Handles physical deletion of tracks and storage cleanup |
| `NovaTuneApp.Workers.Telemetry` | Aggregates playback telemetry into analytics documents |

### Infrastructure

| Component | Role | Connection |
|---|---|---|
| **RavenDB** | Document database (sole DB), 15 indexes | `novatune` |
| **Garnet** | Redis-compatible distributed cache | `cache` |
| **Redpanda** | Kafka-compatible event streaming | `messaging` |
| **MinIO** | S3-compatible object storage for audio files | `storage` |

**Kafka Topics** (prefixed by environment: `dev-`, `testing-`, `prod-`):
- `{prefix}-audio-events` — upload completed events
- `{prefix}-track-deletions` — track deletion events
- `{prefix}-telemetry-events` — playback telemetry events
- `{prefix}-minio-events` — MinIO bucket notifications
- `{prefix}-audio-events-dlq` — dead letter queue

## Project Structure

```
src/
├── NovaTuneApp/
│   ├── NovaTuneApp.AppHost/              # Aspire orchestration
│   ├── NovaTuneApp.ServiceDefaults/      # Shared Aspire config
│   ├── NovaTuneApp.ApiService/           # Main API service
│   ├── NovaTuneApp.Web/                  # Production frontend host
│   ├── NovaTuneApp.Workers.UploadIngestor/
│   ├── NovaTuneApp.Workers.AudioProcessor/
│   ├── NovaTuneApp.Workers.Lifecycle/
│   └── NovaTuneApp.Workers.Telemetry/
├── NovaTuneClient/                       # Frontend monorepo (pnpm)
│   ├── apps/player/                      # Player SPA (port 25173)
│   ├── apps/admin/                       # Admin SPA (port 25174)
│   ├── packages/core/                    # @novatune/core (auth, HTTP, telemetry)
│   ├── packages/api-client/              # @novatune/api-client (generated via Orval)
│   └── packages/ui/                      # @novatune/ui (shared components)
├── unit_tests/                           # xUnit + Shouldly
└── integration_tests/                    # Aspire.Hosting.Testing
doc/
├── requirements/                         # Stage requirement specs
└── implementation/                       # Implementation docs per stage
```

## Key File Locations

| What | Path (relative to `NovaTuneApp.ApiService/`) |
|---|---|
| Endpoints | `Endpoints/` |
| Services (interfaces + impls) | `Services/` |
| Models & DTOs | `Models/` |
| RavenDB indexes | `Infrastructure/RavenDb/Indexes/` |
| Middleware | `Infrastructure/Middleware/` |
| Extensions | `Extensions/` |
| Exceptions | `Exceptions/` |
| Configuration/Options | `Infrastructure/Configuration/` |
| Authorization policies | `Authorization/PolicyNames.cs` |

## API Endpoints

| File | Prefix | Auth | Description |
|---|---|---|---|
| `AuthEndpoints.cs` | `/auth` | Public (mostly) | register, login, refresh, logout |
| `UploadEndpoints.cs` | `/tracks/upload` | Listener | initiate, complete, cancel, status |
| `StreamEndpoints.cs` | `/tracks` | CanStream | `POST /{trackId}/stream` |
| `TrackEndpoints.cs` | `/tracks` | Listener | CRUD, search, soft-delete, restore |
| `PlaylistEndpoints.cs` | `/playlists` | Listener | CRUD, add/remove tracks, reorder |
| `TelemetryEndpoints.cs` | `/telemetry` | ActiveUser | playback events, batch |
| `AdminEndpoints.cs` | `/admin` | Admin | users, tracks, analytics, audit logs |

## Coding Conventions

### Naming

- **Endpoints**: `{Feature}Endpoints.cs` with `Map{Feature}Endpoints()` extension method
- **Services**: `I{Name}Service` interface + `{Name}Service` implementation
- **Models**: `sealed class` for entities, `record` for DTOs and requests/responses
- **Exceptions**: `{Domain}{Reason}Exception` grouped in `{Domain}Exceptions.cs`
- **RavenDB indexes**: `{Collection}_{Purpose}.cs` (e.g., `Tracks_ByUserForSearch.cs`)
- **Handlers**: `{EventName}Handler.cs` implementing `IMessageHandler<T>`
- **Options**: `{Feature}Options` with `SectionName` constant
- **Tests**: `Should_{behavior}` or `Should_{behavior}_when_{condition}`
- **Private fields**: `_camelCase`

### Code Style

- 4-space indent, LF line endings, UTF-8
- `var` preferred when type is apparent
- Nullable reference types enforced (errors, not warnings)
- Entity IDs: ULID format

## Patterns Reference

### Endpoint Pattern

```csharp
public static class FeatureEndpoints
{
    public static void MapFeatureEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/feature")
            .RequireAuthorization(PolicyNames.Listener)
            .WithTags("Feature")
            .WithOpenApi();

        group.MapGet("/", HandleGetAll);
        group.MapPost("/", HandleCreate);
    }

    private static async Task<IResult> HandleGetAll(
        IFeatureService service,
        ClaimsPrincipal user,
        CancellationToken ct) { ... }
}
```

Register in `Program.cs`: `app.MapFeatureEndpoints();`

### Service Pattern

- Constructor injection: `IAsyncDocumentSession`, `IOptions<T>`, `ILogger<T>`
- `CancellationToken ct = default` on all async methods
- RavenDB document IDs: `"{Collection}/{Ulid}"` (e.g., `"Tracks/{trackId}"`)
- Load: `_session.LoadAsync<Track>($"Tracks/{trackId}", ct)`
- Query: `_session.Query<Track, Tracks_ByUserForSearch>()`
- Always call `_session.SaveChangesAsync(ct)` after mutations

### Error Handling

- Custom domain exceptions with typed properties (e.g., `TrackNotFoundException`)
- Catch in endpoints → return RFC 7807 Problem Details via `Results.Problem()`
- Error type URLs: `https://novatune.dev/errors/{error-type}`

### Messaging (Outbox Pattern)

- Store `OutboxMessage` in same RavenDB transaction as the domain change
- `OutboxProcessorService` publishes pending messages to Kafka
- Workers: KafkaFlow consumers with retry middleware + DLQ

### Authentication

- JWT access tokens (15min) + refresh token rotation (1h)
- Policies: `ActiveUser`, `Admin`, `Listener`, `CanStream`, `AdminWithAuditAccess`
- Get user ID: `user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")`

### Resilience (Decorator Pattern)

- `ResilientTrackManagementService` wraps `TrackManagementService`
- Polly pipelines: circuit breaker + retry + timeout

## Adding New Code

### New Endpoint

1. Create `{Feature}Endpoints.cs` in `Endpoints/`
2. Add route group with `.RequireAuthorization()`, `.WithTags()`, `.WithOpenApi()`
3. Add private static async handler methods
4. Register in `Program.cs`: `app.Map{Feature}Endpoints();`

### New Service

1. Create `I{Name}Service.cs` interface in `Services/`
2. Create `{Name}Service.cs` implementation in `Services/`
3. Register in `Program.cs` — `Scoped` for DB-dependent, `Singleton` for stateless

### New RavenDB Index

1. Create `{Collection}_{Purpose}.cs` in `Infrastructure/RavenDb/Indexes/`
2. Inherit `AbstractIndexCreationTask<T>`
3. Indexes auto-deployed at startup via `IndexCreation.CreateIndexes()` in `RavenDbExtensions`

### New Worker

1. Create project `NovaTuneApp.Workers.{Name}`
2. Follow existing worker pattern: Serilog → ServiceDefaults → Options → RavenDB → Health Checks → KafkaFlow consumer → `KafkaFlowHostedService`
3. Register in `AppHost/Program.cs` with infrastructure references and environment variables

## Testing

### Unit Tests (`src/unit_tests/`)

- Framework: xUnit + Shouldly assertions
- Base class: `BaseTest` — provides DI via `UnitTestServiceProviderFactory`
- Override `StubServices(IServiceCollection)` for test-specific fakes
- Fakes live in `Fakes/` directory

### Integration Tests (`src/integration_tests/`)

- Framework: `Aspire.Hosting.Testing` for end-to-end scenarios
- Factory: `IntegrationTestsApiFactory` manages full app lifecycle
- Helpers: `CreateAuthenticatedClientWithUserAsync()`, `CreateAdminClientAsync()`, `SeedTrackAsync()`
- Collection: `[Collection("Integration Tests")]` shares factory instance
- Trait: `[Trait("Category", "Aspire")]` marks infrastructure-dependent tests
- Testing environment: API + cache + DB only (`Features:MessagingEnabled=false`, `Features:StorageEnabled=false`)

## Frontend

- **Monorepo**: pnpm workspace at `src/NovaTuneClient/`
- **Apps**: `player` (port 25173), `admin` (port 25174) — both Vue 3.5 SPAs
- **Packages**: `@novatune/core` (auth, HTTP, telemetry), `@novatune/api-client` (generated), `@novatune/ui` (shared components)
- **Stack**: Vue 3.5, TypeScript, Pinia, TanStack Query, Tailwind CSS, Headless UI
- **Build**: Vite 6.0, dev servers orchestrated by Aspire
- **Code style**: Prettier — semicolons, single quotes, 2-space indent, trailing commas
- **Testing**: Vitest (unit) + Playwright (e2e) + Testing Library
- **Env**: `VITE_API_BASE_URL` set by Aspire in dev; `PORT` for dev server binding

## Configuration

- **Options pattern**: `IOptions<T>` with `ValidateDataAnnotations()` and custom validators
- **Feature flags**: `Features__MessagingEnabled`, `Features__StorageEnabled`
- **Connection strings**: `cache`, `messaging`, `storage`, `novatune`
- **Environments**: Development (full stack + Vite), Testing (API + cache + DB only), Production (static files via Web project)
