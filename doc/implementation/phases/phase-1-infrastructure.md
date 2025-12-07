# Phase 1: Infrastructure & Domain Foundation

> **Status:** ⏳ Pending
> **Dependencies:** None (foundational phase)
> **Milestone:** M1 - Foundation

## Objective

Configure the existing Aspire project structure with infrastructure dependencies and define core domain entities. Keep it simple - avoid premature abstraction.

---

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-3.1 | Secrets Management | Configure `dotnet user-secrets` for local dev |
| NF-3.6 | HTTP Security Headers | Configure HSTS, CSP, X-Frame-Options, X-Content-Type-Options |
| NF-8.1 | Solution Hygiene | Organize folders within existing Aspire projects |
| NF-8.4 | API Documentation | Set up Scalar OpenAPI infrastructure |
| NF-9.1 | API Versioning | Establish `/api/v1/` convention |
| NF-9.3 | Service Discovery | Configure Dotnet Aspire orchestration |

---

## Tasks

### Task 1.1: Project Structure Setup

**Priority:** P1 (Must-have)

Verify and organize the existing Aspire project structure.

#### Subtasks

- [ ] **1.1.1** Verify existing project structure matches expected layout:
  - `NovaTuneApp.ApiService` - API endpoints, entities, services
  - `NovaTuneApp.Web` - Blazor frontend
  - `NovaTuneApp.AppHost` - Aspire orchestration
  - `NovaTuneApp.ServiceDefaults` - Shared configuration
  - `NovaTuneApp.Tests` - All tests

- [ ] **1.1.2** Create folder structure within `NovaTuneApp.ApiService`:
  ```
  NovaTuneApp.ApiService/
  ├── Models/           # Entities (User, Track, AudioMetadata)
  ├── Services/         # Business logic (AuthService, TrackService, etc.)
  ├── Endpoints/        # Minimal API route definitions
  └── Infrastructure/   # External adapters (MinIO, RavenDB, Kafka, NCache)
  ```

- [ ] **1.1.3** Update namespace conventions to match folder structure

- [ ] **1.1.4** Add `.editorconfig` rules for code style enforcement

#### Acceptance Criteria
- All folders exist and are properly organized
- Namespaces follow folder structure
- Build succeeds with no warnings

---

### Task 1.2: Core Domain Entities

**Priority:** P1 (Must-have)

Define the foundational domain entities in the `Models/` folder.

#### Subtasks

- [ ] **1.2.1** Create `User` entity:
  ```csharp
  public sealed class User
  {
      public string Id { get; init; } = string.Empty;
      public string Email { get; set; } = string.Empty;
      public string DisplayName { get; set; } = string.Empty;
      public string PasswordHash { get; set; } = string.Empty;
      public DateTimeOffset CreatedAt { get; init; }
      public DateTimeOffset UpdatedAt { get; set; }
      public UserStatus Status { get; set; } = UserStatus.Active;
  }

  public enum UserStatus { Active, Disabled, PendingDeletion }
  ```

- [ ] **1.2.2** Create `Track` entity:
  ```csharp
  public sealed class Track
  {
      public string Id { get; init; } = string.Empty;
      public string UserId { get; init; } = string.Empty;
      public string Title { get; set; } = string.Empty;
      public string? Artist { get; set; }
      public TimeSpan Duration { get; set; }
      public string ObjectKey { get; set; } = string.Empty;
      public string? Checksum { get; set; }
      public AudioMetadata? Metadata { get; set; }
      public TrackStatus Status { get; set; } = TrackStatus.Processing;
      public DateTimeOffset CreatedAt { get; init; }
      public DateTimeOffset UpdatedAt { get; set; }
  }

  public enum TrackStatus { Processing, Ready, Failed, Deleted }
  ```

- [ ] **1.2.3** Create `AudioMetadata` value object:
  ```csharp
  public sealed record AudioMetadata
  {
      public string Format { get; init; } = string.Empty;
      public int Bitrate { get; init; }
      public int SampleRate { get; init; }
      public int Channels { get; init; }
      public long FileSizeBytes { get; init; }
      public string? MimeType { get; init; }
  }
  ```

- [ ] **1.2.4** Add validation attributes and data annotations

- [ ] **1.2.5** Create unit tests for entity validation rules

#### Acceptance Criteria
- All entities are defined with proper nullability annotations
- Validation rules are implemented and tested
- Entities follow C# 12 conventions

---

### Task 1.3: Docker Compose Infrastructure

**Priority:** P1 (Must-have)

Set up Docker Compose for local infrastructure dependencies.

#### Subtasks

- [ ] **1.3.1** Create `docker-compose.yml` with all services:
  ```yaml
  services:
    ravendb:
      image: ravendb/ravendb:6.0-ubuntu-latest
      ports:
        - "8080:8080"    # Studio
        - "38888:38888"  # Database
      environment:
        - RAVEN_Setup_Mode=None
        - RAVEN_License_Eula_Accepted=true
        - RAVEN_Security_UnsecuredAccessAllowed=PrivateNetwork
      volumes:
        - ravendb-data:/opt/RavenDB/Server/RavenData

    minio:
      image: minio/minio:latest
      ports:
        - "9000:9000"   # API
        - "9001:9001"   # Console
      environment:
        - MINIO_ROOT_USER=minioadmin
        - MINIO_ROOT_PASSWORD=minioadmin
      command: server /data --console-address ":9001"
      volumes:
        - minio-data:/data

    kafka:
      image: confluentinc/cp-kafka:7.5.0
      ports:
        - "9092:9092"
      environment:
        - KAFKA_NODE_ID=1
        - KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT
        - KAFKA_LISTENERS=PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093
        - KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092
        - KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093
        - KAFKA_PROCESS_ROLES=broker,controller
        - KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER
        - KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1
      volumes:
        - kafka-data:/var/lib/kafka/data

    rabbitmq:
      image: rabbitmq:3-management
      ports:
        - "5672:5672"   # AMQP
        - "15672:15672" # Management
      environment:
        - RABBITMQ_DEFAULT_USER=guest
        - RABBITMQ_DEFAULT_PASS=guest
      volumes:
        - rabbitmq-data:/var/lib/rabbitmq

    ncache:
      image: alachisoft/ncache:latest
      ports:
        - "8250:8250"   # Management
        - "9800:9800"   # Client
      volumes:
        - ncache-data:/opt/ncache
  ```

- [ ] **1.3.2** Create `docker-compose.override.yml` for development-specific settings

- [ ] **1.3.3** Create `.env.example` with all required environment variables:
  ```bash
  # RavenDB
  RAVENDB_URL=http://localhost:8080
  RAVENDB_DATABASE=NovaTune

  # MinIO
  MINIO_ENDPOINT=localhost:9000
  MINIO_ACCESS_KEY=minioadmin
  MINIO_SECRET_KEY=minioadmin
  MINIO_BUCKET=novatune-dev-audio

  # Kafka
  KAFKA_BOOTSTRAP_SERVERS=localhost:9092

  # RabbitMQ
  RABBITMQ_HOST=localhost
  RABBITMQ_USERNAME=guest
  RABBITMQ_PASSWORD=guest

  # NCache
  NCACHE_SERVER=localhost:9800
  NCACHE_CACHE_NAME=novatune-cache

  # JWT
  JWT_ISSUER=https://novatune.local
  JWT_AUDIENCE=novatune-api
  JWT_SIGNING_KEY_PATH=./keys/signing.pem
  ```

- [ ] **1.3.4** Document minimum resource requirements (8GB RAM)

- [ ] **1.3.5** Create health check scripts for each service

- [ ] **1.3.6** Add startup wait scripts to ensure services are ready

#### Acceptance Criteria
- `docker compose up` starts all services
- All services pass health checks
- `.env.example` documents all variables
- README includes startup instructions

---

### Task 1.4: Aspire AppHost Configuration

**Priority:** P1 (Must-have)

Configure Dotnet Aspire orchestration for local development.

#### Subtasks

- [ ] **1.4.1** Update `NovaTuneApp.AppHost/Program.cs` with resource definitions:
  ```csharp
  var builder = DistributedApplication.CreateBuilder(args);

  // External resources (reference Docker Compose)
  var ravendb = builder.AddConnectionString("ravendb");
  var minio = builder.AddConnectionString("minio");
  var kafka = builder.AddConnectionString("kafka");
  var rabbitmq = builder.AddConnectionString("rabbitmq");
  var ncache = builder.AddConnectionString("ncache");

  // API Service
  var apiService = builder.AddProject<Projects.NovaTuneApp_ApiService>("apiservice")
      .WithReference(ravendb)
      .WithReference(minio)
      .WithReference(kafka)
      .WithReference(rabbitmq)
      .WithReference(ncache);

  // Web Frontend
  builder.AddProject<Projects.NovaTuneApp_Web>("webfrontend")
      .WithExternalHttpEndpoints()
      .WithReference(apiService);

  builder.Build().Run();
  ```

- [ ] **1.4.2** Configure connection strings in `appsettings.Development.json`

- [ ] **1.4.3** Configure connection strings in `appsettings.Test.json` for test containers

- [ ] **1.4.4** Set up environment-specific configuration transforms

- [ ] **1.4.5** Verify Aspire dashboard shows all services

#### Acceptance Criteria
- `dotnet run --project NovaTuneApp.AppHost` starts all services
- Aspire dashboard accessible and shows healthy services
- Connection strings properly resolved

---

### Task 1.5: ServiceDefaults Configuration

**Priority:** P1 (Must-have)

Configure shared service defaults for observability and resilience.

#### Subtasks

- [ ] **1.5.1** Configure OpenTelemetry in `ServiceDefaults`:
  ```csharp
  public static IHostApplicationBuilder AddServiceDefaults(
      this IHostApplicationBuilder builder)
  {
      builder.ConfigureOpenTelemetry();
      builder.AddDefaultHealthChecks();
      builder.Services.AddServiceDiscovery();
      builder.Services.ConfigureHttpClientDefaults(http =>
      {
          http.AddStandardResilienceHandler();
          http.AddServiceDiscovery();
      });
      return builder;
  }
  ```

- [ ] **1.5.2** Configure Serilog with structured logging:
  ```csharp
  Log.Logger = new LoggerConfiguration()
      .Enrich.WithCorrelationId()
      .Enrich.FromLogContext()
      .WriteTo.Console(new JsonFormatter())
      .CreateLogger();
  ```

- [ ] **1.5.3** Add correlation ID middleware for request tracing

- [ ] **1.5.4** Configure health check endpoints:
  - `/health` - Basic liveness check
  - `/ready` - Readiness with dependency checks

- [ ] **1.5.5** Add custom health checks for each infrastructure dependency

#### Acceptance Criteria
- Logs include correlation IDs
- Health endpoints return proper status
- OpenTelemetry exports to Aspire dashboard

---

### Task 1.6: HTTP Security Headers

**Priority:** P1 (Must-have)

Implement security headers middleware (NF-3.6).

#### Subtasks

- [ ] **1.6.1** Create `SecurityHeadersMiddleware`:
  ```csharp
  public class SecurityHeadersMiddleware
  {
      private readonly RequestDelegate _next;

      public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

      public async Task InvokeAsync(HttpContext context)
      {
          var headers = context.Response.Headers;

          // HSTS (only in production over HTTPS)
          headers["Strict-Transport-Security"] =
              "max-age=31536000; includeSubDomains; preload";

          // Content Security Policy
          headers["Content-Security-Policy"] =
              "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'";

          // Prevent clickjacking
          headers["X-Frame-Options"] = "DENY";

          // Prevent MIME type sniffing
          headers["X-Content-Type-Options"] = "nosniff";

          // Referrer Policy
          headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

          // Permissions Policy
          headers["Permissions-Policy"] =
              "geolocation=(), microphone=(), camera=()";

          await _next(context);
      }
  }
  ```

- [ ] **1.6.2** Configure HSTS to only apply in production

- [ ] **1.6.3** Add CSP configuration options for different environments

- [ ] **1.6.4** Write tests to verify headers are present

#### Acceptance Criteria
- All security headers present in responses
- Headers configurable per environment
- Tests verify header presence

---

### Task 1.7: API Foundation

**Priority:** P1 (Must-have)

Set up API infrastructure including versioning, CORS, and documentation.

#### Subtasks

- [ ] **1.7.1** Configure API versioning with `/api/v1/` prefix:
  ```csharp
  app.MapGroup("/api/v1")
      .MapHealthEndpoints()
      .MapAuthEndpoints()
      .MapTrackEndpoints();
  ```

- [ ] **1.7.2** Configure CORS policy:
  ```csharp
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("Default", policy =>
      {
          policy.WithOrigins(
                  "https://localhost:5001",
                  "https://novatune.local")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
      });
  });
  ```

- [ ] **1.7.3** Set up Scalar OpenAPI documentation at `/docs`:
  ```csharp
  builder.Services.AddOpenApi();

  app.MapScalarApiReference(options =>
  {
      options.Title = "NovaTune API";
      options.Theme = ScalarTheme.Purple;
  });
  ```

- [ ] **1.7.4** Create health check endpoints:
  ```csharp
  app.MapHealthChecks("/health", new HealthCheckOptions
  {
      Predicate = _ => false // Liveness only
  });

  app.MapHealthChecks("/ready", new HealthCheckOptions
  {
      Predicate = check => check.Tags.Contains("ready")
  });
  ```

- [ ] **1.7.5** Add request/response logging middleware

- [ ] **1.7.6** Configure JSON serialization options (camelCase, etc.)

#### Acceptance Criteria
- `/health` returns 200
- `/docs` shows Scalar UI
- CORS allows configured origins
- API uses `/api/v1/` prefix

---

### Task 1.8: Secrets Management

**Priority:** P1 (Must-have)

Configure secure secrets management for local development (NF-3.1).

#### Subtasks

- [ ] **1.8.1** Initialize user secrets for ApiService:
  ```bash
  dotnet user-secrets init --project src/NovaTuneApp/NovaTuneApp.ApiService
  ```

- [ ] **1.8.2** Document required secrets:
  - JWT signing key path
  - MinIO credentials
  - RavenDB connection
  - External API keys

- [ ] **1.8.3** Create `secrets.json.example` template

- [ ] **1.8.4** Add secrets validation on startup (fail fast if missing)

- [ ] **1.8.5** Document production secrets strategy (Azure Key Vault / AWS Secrets Manager)

#### Acceptance Criteria
- User secrets configured
- Application fails fast on missing secrets
- Documentation for secrets setup

---

### Task 1.9: FFmpeg Base Image

**Priority:** P2 (Should-have)

Configure Docker image with FFmpeg/FFprobe for audio processing.

#### Subtasks

- [ ] **1.9.1** Create Dockerfile for ApiService with FFmpeg:
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
  RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*
  WORKDIR /app

  FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
  # ... build steps

  FROM base AS final
  COPY --from=build /app/publish .
  ENTRYPOINT ["dotnet", "NovaTuneApp.ApiService.dll"]
  ```

- [ ] **1.9.2** Verify FFmpeg and FFprobe are accessible

- [ ] **1.9.3** Add integration test for FFprobe execution

- [ ] **1.9.4** Document FFmpeg version requirements

#### Acceptance Criteria
- Docker image includes FFmpeg
- FFprobe can analyze audio files
- Integration test passes

---

### Task 1.10: CI Pipeline Foundation

**Priority:** P2 (Should-have)

Set up initial CI pipeline for build and test.

#### Subtasks

- [ ] **1.10.1** Create GitHub Actions workflow for build:
  ```yaml
  name: Build and Test
  on: [push, pull_request]
  jobs:
    build:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '9.0.x'
        - run: dotnet restore
        - run: dotnet build --no-restore /p:TreatWarningsAsErrors=true
        - run: dotnet format --verify-no-changes
        - run: dotnet test --no-build
  ```

- [ ] **1.10.2** Add secret scanning (gitleaks)

- [ ] **1.10.3** Configure branch protection rules

- [ ] **1.10.4** Add build status badge to README

#### Acceptance Criteria
- CI runs on every push
- Build fails on warnings
- Format check enforced

---

## Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Entity validation | Core validation rules |
| Integration | Aspire orchestration | All services start and communicate |
| Integration | Health endpoints | Return 200 when healthy |

---

## Exit Criteria

- [ ] `dotnet build` succeeds with warnings-as-errors
- [ ] `dotnet test` passes all tests
- [ ] `dotnet format --verify-no-changes` passes
- [ ] Aspire dashboard shows all services healthy
- [ ] API returns 200 on `/health` endpoint
- [ ] Scalar UI accessible at `/docs`
- [ ] Docker Compose starts all infrastructure services
- [ ] Security headers present in all responses

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Aspire version incompatibility | High | Pin Aspire 13.0 in `global.json` |
| Docker resource constraints | Medium | Document minimum resource requirements (8GB RAM) |
| Infrastructure service startup order | Medium | Use Aspire health checks and wait strategies |

---

## Future Considerations

If codebase complexity grows significantly (Phase 6+), consider extracting to layered architecture:
- `NovaTune.Domain` - Pure domain entities
- `NovaTune.Application` - Use cases and abstractions
- `NovaTune.Infrastructure` - External adapters

For now, keeping everything in `NovaTuneApp.ApiService` with clear folder boundaries is sufficient.

---

## Navigation

← [Overview](../overview.md) | [Phase 2: User Management →](phase-2-user-management.md)
