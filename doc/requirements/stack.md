# NovaTune Technology Stack

## Runtime & Framework
- C# 12
- ASP.NET Core (.NET 9.0)
- Dotnet Aspire (orchestration)

## Data & Storage
- RavenDB (document database, sole data store)
- MinIO (S3-compatible object storage for audio files)
- Garnet (Redis-compatible distributed cache from Microsoft)
  - AOF persistence enabled for durability
  - Used for presigned URL caching, session state
  - StackExchange.Redis client

## Messaging
- Redpanda (Kafka-compatible event streaming platform)
  - Replaces both Apache Kafka and RabbitMQ
  - Topics: `{env}-audio-events`, `{env}-track-deletions`
  - JSON message format with schema versioning
  - SASL/SCRAM + TLS in stage/prod environments
- KafkaFlow (.NET Kafka client framework)
  - Typed message handlers with dependency injection
  - Middleware pipeline for serialization/retry
  - Admin dashboard for debugging

## Authentication
- ASP.NET Identity (custom RavenDB IUserStore/IRoleStore implementation required)
- JWT tokens with refresh flow

## Audio Processing
- FFmpeg/FFprobe (metadata extraction, format validation; deployed via base Docker image)

## Observability
- Serilog (structured JSON logging with correlation IDs)
- OpenTelemetry (metrics, traces via Dotnet Aspire)

## API & Gateway
- YARP (reverse proxy, API gateway)
- Scalar (OpenAPI documentation UI)

## Infrastructure
- Docker
- Kubernetes
- GitHub Actions (CI/CD)

## Testing
- xUnit

## Frontend
- Vue.js
- TypeScript
