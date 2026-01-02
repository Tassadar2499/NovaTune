# Acceptance Criteria

- [ ] `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost` starts all dependencies (RavenDB, MinIO, Redpanda, Garnet).
- [ ] API service becomes ready only when required dependencies are healthy.
- [ ] API service remains ready (degraded) when Garnet is unavailable.
- [ ] Invalid configuration causes startup failure with clear error message.
- [ ] Scalar UI is accessible at `/scalar` (or configured path).
- [ ] Logs are JSON-formatted with `CorrelationId` present.
- [ ] Traces appear in Aspire dashboard for API requests.
- [ ] No secrets are committed to the repository.
