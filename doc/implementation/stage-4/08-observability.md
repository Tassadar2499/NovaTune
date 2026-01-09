# 8. Observability (NF-4.x)

## Logging

| Event | Level | Fields |
|-------|-------|--------|
| Stream URL requested | Info | `UserId`, `TrackId`, `CorrelationId` |
| Cache hit | Debug | `TrackId`, `ExpiresAt` |
| Cache miss - generating URL | Debug | `TrackId` |
| Presigned URL generated | Debug | `TrackId`, `ExpiresAt` |
| Cache entry encrypted and stored | Debug | `TrackId`, `KeyVersion` |
| Access denied (ownership) | Warning | `UserId`, `TrackId`, `OwnerId` |
| Track not ready | Warning | `TrackId`, `Status` |
| Cache invalidated | Debug | `TrackId`, `UserId` |

**Redaction (NF-4.5):** Never log presigned URLs or object keys.

## Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `stream_url_requests_total` | Counter | `status` (success/error), `cache_hit` (true/false) |
| `stream_url_request_duration_ms` | Histogram | — |
| `stream_url_cache_hits_total` | Counter | — |
| `stream_url_cache_misses_total` | Counter | — |
| `stream_url_presign_generated_total` | Counter | — |
| `stream_cache_invalidations_total` | Counter | `reason` (deletion/logout/status_change) |

## Tracing

- Propagate `CorrelationId` from request to cache and storage operations
- Span hierarchy:
  - `stream.get_url` (parent)
    - `cache.get` (child)
    - `storage.presign` (child, on cache miss)
    - `cache.set` (child, on cache miss)
