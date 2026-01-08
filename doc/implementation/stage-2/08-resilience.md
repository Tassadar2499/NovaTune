# 8. Resilience (NF-1.4)

## Timeouts

| Operation | Timeout | Retries |
|-----------|---------|---------|
| MinIO presign generation | 5s | 1 |
| RavenDB read (UploadSession) | 2s | 1 |
| RavenDB write (Track + Outbox) | 5s | 0 (idempotent via outbox) |
| Redpanda produce (outbox) | 2s | 2 |

## Circuit Breaker

- MinIO: Open after 5 consecutive failures, half-open after 30s
- RavenDB: Open after 5 consecutive failures, half-open after 30s

## Fail-Closed Behavior

Upload initiation fails closed:
- If MinIO unavailable → `503 Service Unavailable`
- If RavenDB unavailable → `503 Service Unavailable`
- If quota check fails → `503 Service Unavailable` (not silent pass)
