# 9. Resilience (NF-1.4)

## Timeouts

| Operation | Timeout | Retries |
|-----------|---------|---------|
| Cache read | 500ms | 0 (cache is optional) |
| Cache write | 500ms | 0 (cache is optional) |
| MinIO presign generation | 5s | 1 |
| RavenDB read (Track) | 2s | 1 |

## Circuit Breaker

| Dependency | Failure Threshold | Half-Open After |
|------------|-------------------|-----------------|
| Garnet/Redis | 5 consecutive | 30s |
| MinIO | 5 consecutive | 30s |

## Fail-Open vs Fail-Closed

| Dependency | Behavior |
|------------|----------|
| Cache unavailable | **Fail-open**: Generate presigned URL directly (no caching) |
| MinIO unavailable | **Fail-closed**: Return `503 Service Unavailable` |
| RavenDB unavailable | **Fail-closed**: Return `503 Service Unavailable` |
