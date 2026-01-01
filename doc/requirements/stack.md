# NovaTune Technology Stack

## Runtime & Framework
- C# 12
- ASP.NET Core (.NET 9.0)
- Dotnet Aspire (orchestration)

## Data & Storage
- RavenDB (document database, sole data store)
- MinIO (S3-compatible object storage for audio files)
- NCache (distributed caching for presigned URLs, session state)

## Messaging
- Apache Kafka (event streaming, analytics, audit logs)
- RabbitMQ (background task queues, transient jobs)

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
