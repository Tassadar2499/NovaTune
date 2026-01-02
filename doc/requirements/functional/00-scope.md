# 0. Scope

## 0.1 In scope
- **API service**: HTTP backend responsible for authentication, uploads, streaming URL issuance, track management, and publishing events.
- **Background workers**: Asynchronous processing for audio metadata/waveforms, analytics aggregation, and lifecycle cleanup triggered by events.

## 0.2 External dependencies
- RavenDB: track metadata, analytics, playlists (system of record).
- MinIO (S3-compatible): audio object storage.
- Redpanda (Kafka-compatible): event streaming backbone.
- Garnet (Redis-compatible): distributed cache for tokens and presigned URLs.

## 0.3 Clarified decisions
- **Identifiers**: external identifiers (`UserId`, `TrackId`, etc.) use ULID and must be consistent across API payloads and events.
- **Topic naming**: `{env}` is the authoritative environment prefix (e.g., `dev`, `staging`, `prod`) and is required/configured per deployment.
- **Consistency model**: clients should expect eventual consistency (e.g., track moves from `Processing` to `Ready` asynchronously).
- **Error contract**: API errors use RFC 7807 Problem Details.
- **Rate limiting**: explicit rate limits are required for login, upload initiation, playback URL issuance, and telemetry ingestion (values TBD/configurable).
