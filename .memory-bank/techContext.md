# Technical Context

## Tech Stack

### Backend (.NET 9)
| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | ASP.NET Core (Minimal APIs) | 9.0 |
| Orchestration | .NET Aspire | 9.0 |
| Database | RavenDB | 6.2.4 |
| Cache | Garnet (Redis-compatible) | via StackExchange.Redis 2.8.16 |
| Messaging | Redpanda (Kafka-compatible) | via KafkaFlow 3.0.10 |
| Storage | MinIO (S3-compatible) | Minio SDK 6.0.3 |
| Auth | ASP.NET Identity + JWT Bearer | 9.0.0 |
| Password Hashing | Argon2id | Isopoh.Cryptography.Argon2 2.0.0 |
| Logging | Serilog | 9.0.0 |
| Observability | OpenTelemetry | via Aspire |
| API Docs | Scalar | 2.0.36 |
| Resilience | Polly | via Microsoft.Extensions.Http.Resilience 10.0.0 |

### Frontend (Vue.js 3)
| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | Vue.js | 3.5 |
| Language | TypeScript | 5.6 |
| State | Pinia | 2.2 |
| Data Fetching | TanStack Query | 5.0 |
| Routing | Vue Router | 4.4 |
| UI | Headless UI + TailwindCSS | 1.7 / 3.4 |
| Build | Vite | 6.0 |
| Testing | Vitest + Playwright + Testing Library | 2.0 / 1.45 |
| Package Manager | pnpm | workspace |

## Project Structure
```
NovaTune/
├── src/
│   ├── NovaTuneApp/
│   │   ├── NovaTuneApp.sln
│   │   ├── NovaTuneApp.AppHost/         # Aspire orchestration
│   │   ├── NovaTuneApp.ServiceDefaults/  # Shared config
│   │   ├── NovaTuneApp.ApiService/       # REST API
│   │   ├── NovaTuneApp.Web/             # Static file server
│   │   ├── NovaTuneApp.Workers.UploadIngestor/
│   │   ├── NovaTuneApp.Workers.AudioProcessor/
│   │   ├── NovaTuneApp.Workers.Lifecycle/
│   │   └── NovaTuneApp.Workers.Telemetry/
│   ├── NovaTuneClient/                   # Vue.js monorepo
│   │   ├── apps/player/                  # Listener SPA
│   │   ├── apps/admin/                   # Admin SPA
│   │   ├── packages/api-client/          # Generated TS client
│   │   ├── packages/core/               # Shared utilities
│   │   └── packages/ui/                 # Shared components
│   ├── unit_tests/
│   └── integration_tests/
├── doc/
│   ├── requirements/                     # Functional & non-functional specs
│   └── implementation/                   # Stage-by-stage implementation docs
└── .claude/                              # Claude Code skills & agents
```

## Build Commands
```bash
# Backend
dotnet build src/NovaTuneApp/NovaTuneApp.sln
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost
dotnet test src/NovaTuneApp/NovaTuneApp.sln

# Frontend
cd src/NovaTuneClient && pnpm install
pnpm --filter @novatune/player dev
pnpm --filter @novatune/admin dev
```

## Configuration
- **Connection strings**: cache, messaging, storage, novatune (RavenDB)
- **JWT**: Issuer, AccessTokenExpirationMinutes (15), RefreshTokenExpirationMinutes (60)
- **Argon2**: MemoryCostKb (65536), Iterations (3), Parallelism (4)
- **Rate limiting**: Auth LoginPerIp (10/min)
- **Track management**: DeletionGracePeriod (30 days), MaxPageSize (100)
- **Environment toggles**: Features__MessagingEnabled, Features__StorageEnabled

## Infrastructure Ports
- API Service: configured by Aspire
- Garnet: 6379
- RavenDB: 8080
- Redpanda: 19092 (Kafka), 8085 (Console)
- MinIO: 9000 (API), 9001 (Console)
- Player dev: 25173
- Admin dev: 25174
