# Stage 4 — Streaming URL Issuance + Caching

**Goal:** Allow streaming via short-lived presigned GET URLs without proxying bytes.

## Overview

```
┌─────────┐  POST /tracks/{trackId}/stream  ┌─────────────┐
│ Client  │ ───────────────────────────────►│ API Service │
└────┬────┘ ◄─────────────────────────────── └──────┬──────┘
     │       StreamResponse (presigned URL)        │
     │                                             │ 1. Validate ownership/status
     │                                             │ 2. Check cache (Garnet)
     │                                             │ 3. Generate presigned URL if miss
     │                                             │ 4. Encrypt + cache URL
     │                                             ▼
     │                                    ┌─────────────────┐
     │       GET (presigned, range)       │     MinIO       │
     │ ──────────────────────────────────►│ (audio bucket)  │
     │ ◄─────────────────────────────────┘└─────────────────┘
     │       Audio bytes (206 Partial Content)
     │
     │                                    ┌─────────────────┐
     │                                    │     Garnet      │
     │                                    │ (encrypted URL) │
     │                                    └─────────────────┘
```

## Document Index

| Document | Description |
|----------|-------------|
| [01-api-endpoint.md](01-api-endpoint.md) | API endpoint specification |
| [02-streaming-service.md](02-streaming-service.md) | Service layer design |
| [03-cache-behavior.md](03-cache-behavior.md) | Cache key design, TTL, encryption |
| [04-storage-service.md](04-storage-service.md) | Storage service extension |
| [05-range-requests.md](05-range-requests.md) | HTTP range request support |
| [06-configuration.md](06-configuration.md) | Configuration options |
| [07-endpoint-implementation.md](07-endpoint-implementation.md) | Endpoint code |
| [08-observability.md](08-observability.md) | Logging, metrics, tracing |
| [09-resilience.md](09-resilience.md) | Timeouts, circuit breakers |
| [10-security.md](10-security.md) | Security considerations |
| [11-test-strategy.md](11-test-strategy.md) | Testing approach |
| [12-implementation-tasks.md](12-implementation-tasks.md) | Implementation checklist |
| [13-requirements.md](13-requirements.md) | Requirements covered & open items |
