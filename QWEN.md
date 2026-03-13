# NovaTune - Project Context

## Project Overview

NovaTune is a self-hosted audio streaming platform built with a **.NET 9 backend** and **Vue 3 frontend**. The platform supports:

- **Authentication**: JWT-based auth with refresh token rotation and role separation
- **Audio Upload & Processing**: Direct-to-MinIO uploads with event-driven ingestion and background transcoding
- **Track Management**: CRUD operations with soft delete and lifecycle cleanup
- **Streaming**: Presigned URLs with encrypted cache storage
- **Playlists**: Full CRUD with ordered track membership
- **Telemetry**: Playback analytics and aggregate metrics
- **Admin Dashboard**: User management, track moderation, and audit logging

## Memory Bank
- Use `.memory-bank/` as the fast project snapshot before making changes or answering repo-level questions.
- Start with:
    - `.memory-bank/projectbrief.md`
    - `.memory-bank/systemPatterns.md`
    - `.memory-bank/techContext.md`
    - `.memory-bank/activeContext.md`
    - `.memory-bank/progress.md`
- Keep `.memory-bank` aligned with the live codebase; update it when architecture, workflow, or implementation status changes materially.
- `AGENTS.md` remains the authoritative source for repository-specific working rules; `.memory-bank` is the current codebase context layer.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Frontend (Vue 3 + Vite)                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│  │   Player    │  │    Admin    │  │  Shared Packages        │ │
│  │   (port     │  │   (port     │  │  - api-client (Orval)   │ │
│  │   25173)    │  │   25174)    │  │  - core (auth, http)    │ │
│  └─────────────┘  └─────────────┘  │  - ui (components)      │ │
│                                     └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              Backend (.NET 9 + Aspire Orchestration)            │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  ApiService (Minimal APIs, JWT Auth, RavenDB, Kafka)    │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐           │
│  │ Upload       │ │ Audio        │ │ Lifecycle    │           │
│  │ Ingestor     │ │ Processor    │ │ Worker       │           │
│  └──────────────┘ └──────────────┘ └──────────────┘           │
│  ┌──────────────┐                                             │
│  │ Telemetry    │                                             │
│  │ Worker       │                                             │
│  └──────────────┘                                             │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   ┌─────────┐          ┌─────────┐          ┌─────────┐
   │RavenDB  │          │  MinIO  │          │ Garnet  │
   │(Docs)   │          │(Storage)│          │ (Cache) │
   └─────────┘          └─────────┘          └─────────┘
        ▲
        │
   ┌─────────┐
   │Redpanda │
   │ (Kafka) │
   └─────────┘
```

## Repository Structure

```
NovaTune/
├── src/NovaTuneApp/              # .NET solution
│   ├── NovaTuneApp.sln
│   ├── NovaTuneApp.ApiService/   # API endpoints, domain models, services
│   ├── NovaTuneApp.AppHost/      # Aspire orchestration
│   ├── NovaTuneApp.ServiceDefaults/  # Shared hosting/logging defaults
│   ├── NovaTuneApp.Web/          # ASP.NET Core web host for static assets
│   ├── NovaTuneApp.Workers.*     # Background workers:
│   │   ├── AudioProcessor/       # FFmpeg/FFprobe transcoding
│   │   ├── UploadIngestor/       # Upload session processing
│   │   ├── Lifecycle/            # Track lifecycle cleanup
│   │   └── Telemetry/            # Playback telemetry aggregation
│   └── NovaTuneApp.Tests/        # Unit tests
├── src/NovaTuneClient/           # Frontend pnpm monorepo
│   ├── apps/player/              # Listener-facing SPA
│   ├── apps/admin/               # Admin/moderation SPA
│   └── packages/                 # Shared packages:
│       ├── api-client/           # Orval-generated API client
│       ├── core/                 # Auth, HTTP, telemetry utilities
│       └── ui/                   # Shared UI components
├── src/unit_tests/               # .NET unit tests (xUnit + Shouldly)
├── src/integration_tests/        # Aspire-backed integration tests
├── src/component_tests/          # Reserved for frontend component tests
├── src/functional_tests/         # Reserved for functional tests
├── doc/                          # Documentation, requirements, diagrams
└── .memory-bank/                 # Fast codebase snapshot (read first)
```

## Building and Running

### Prerequisites

- **.NET 9 SDK** (Aspire AppHost SDK 13.0.0)
- **Node.js >= 20** and **pnpm >= 9**
- **Docker** (for Aspire to spin up infrastructure containers)

### Backend Commands

```bash
# Restore and build
dotnet restore src/NovaTuneApp/NovaTuneApp.sln
dotnet build src/NovaTuneApp/NovaTuneApp.sln -c Release

# Run with Aspire (local orchestration)
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost/NovaTuneApp.AppHost.csproj

# Run tests
dotnet test src/NovaTuneApp/NovaTuneApp.sln -c Debug
```

### Frontend Commands

```bash
cd src/NovaTuneClient

# Development servers
pnpm dev:player    # Player SPA at http://localhost:25173
pnpm dev:admin     # Admin SPA at http://localhost:25174

# Build, lint, test, typecheck
pnpm build
pnpm lint
pnpm test
pnpm typecheck

# Regenerate API client from OpenAPI spec
pnpm generate
```

### Environment Configuration

1. Copy `.env.example` to `.env` and configure:
   - RavenDB, MinIO, Redpanda, Garnet connection strings
   - JWT signing keys and expiration
   - Audio processing paths (FFmpeg/FFprobe)

2. For local development with Aspire, infrastructure containers are auto-provisioned.

## Development Conventions

### C# Coding Style

- **Indentation**: 4 spaces, LF line endings, UTF-8, final newline
- **Braces**: Allman style
- **Naming**:
  - Constants/static readonly: `PascalCase`
  - Private instance fields: `_camelCase`
- **Nullable**: Strict mode; CS8600-CS8604 are errors
- **Preferences**: `var` when type is obvious

### Testing Practices

- **Framework**: xUnit + Shouldly
- **File naming**: `*Tests.cs`
- **Test naming**: Behavior-style (e.g., `Should_Reject_InvalidMetadata()`)
- **Coverage**: Add tests for new domain logic and endpoint validation
- **Integration tests**: Use `Aspire.Hosting.Testing` for realistic scenarios

### Frontend Conventions

- **TypeScript**: Strict mode, 2-space indentation for JSON/YAML
- **API Client**: Generated via Orval from `http://localhost:5000/openapi/v1.json`
- **State Management**: Pinia stores for auth and application state
- **Routing**: Vue Router with guards based on auth state

## Key Configuration Files

| File | Purpose |
|------|---------|
| `.env.example` | Environment variable templates |
| `.editorconfig` | Code formatting rules |
| `src/NovaTuneApp/NovaTuneApp.sln` | .NET solution file |
| `src/NovaTuneClient/package.json` | Frontend workspace root |
| `src/NovaTuneClient/pnpm-workspace.yaml` | pnpm monorepo config |

## Memory Bank

Always consult `.memory-bank/` before making changes:

1. `projectbrief.md` - High-level overview
2. `systemPatterns.md` - Architecture and patterns
3. `techContext.md` - Stack versions and dependencies
4. `activeContext.md` - Current implementation state
5. `progress.md` - What's implemented vs. remaining work

## Security Notes

- Never commit secrets, credentials, or connection strings
- Use environment variables or local `.env` for sensitive config
- JWT signing keys should be generated securely for production
- Rate limiting is enabled for login endpoints and broader API usage

## Common Workflows

### Adding a new API endpoint

1. Add model/DTO in `ApiService/Models/` with validation attributes
2. Add endpoint in `ApiService/Endpoints/{Domain}/`
3. Wire up service layer if business logic is needed
4. Add unit tests in `src/unit_tests/`
5. Add integration tests in `src/integration_tests/`
6. Regenerate frontend API client: `pnpm generate`

### Adding a new frontend feature

1. Update shared package (`@novatune/core` or `@novatune/ui`) if needed
2. Add route, page component, and Pinia store in `apps/player` or `apps/admin`
3. Use `@novatune/api-client` for API calls
4. Test with development server: `pnpm dev:player` or `pnpm dev:admin`

### Running the full stack

1. Ensure Docker is running
2. Start Aspire AppHost: `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost/`
3. Access player at `http://localhost:25173` or admin at `http://localhost:25174`
4. Aspire Dashboard available for observability (URL shown in console)
