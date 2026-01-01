# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the solution
dotnet build src/NovaTuneApp/NovaTuneApp.sln

# Run the application (starts all services via Aspire orchestration)
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost

# Run all tests
dotnet test src/NovaTuneApp/NovaTuneApp.sln

# Run unit tests only
dotnet test src/unit_tests/NovaTune.UnitTests.csproj

# Run integration tests only
dotnet test src/integration_tests/NovaTuneApp.IntegrationTests/NovaTuneApp.IntegrationTests.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TrackTests.Should_pass_with_valid_data"
```

## Architecture

NovaTune is a .NET 9 Aspire application for audio track management with distributed caching and messaging.

### Project Structure

- **NovaTuneApp.AppHost** - Aspire orchestration host that configures and launches all services with their dependencies (cache, messaging)
- **NovaTuneApp.ApiService** - Main API service handling audio tracks, caching, and event messaging
- **NovaTuneApp.Web** - Web frontend that communicates with the API service
- **NovaTuneApp.ServiceDefaults** - Shared Aspire configuration (OpenTelemetry, health checks, service discovery, resilience)

### Infrastructure

- **Distributed Caching**: Garnet (Redis-compatible) via `StackExchange.Redis` and `Aspire.StackExchange.Redis`
- **Messaging**: Redpanda (Kafka-compatible) via KafkaFlow for event-driven communication
- **Topics**: `{prefix}-audio-events` for uploads, `{prefix}-track-deletions` for deletions

### Key Patterns

**Service Registration** (ApiService/Program.cs):
- Services registered via DI: `ICacheService`, `IMessageProducerService`, `ITrackService`, `IStorageService`
- KafkaFlow consumers/producers configured with retry logic for delayed broker availability
- `KafkaFlowHostedService` manages background Kafka lifecycle

**ServiceDefaults Extension Methods**:
- `AddServiceDefaults()` - Adds OpenTelemetry, health checks, service discovery, HTTP resilience
- `MapDefaultEndpoints()` - Maps `/health` and `/alive` endpoints (development only)
- `AddDefaultCaching()` / `AddDefaultMessaging()` - Infrastructure shortcuts

### Testing

- Unit tests use xUnit with Shouldly assertions
- Integration tests use `Aspire.Hosting.Testing` for end-to-end scenarios
- Tests tagged with `[Trait("Category", "Aspire")]` require Aspire infrastructure