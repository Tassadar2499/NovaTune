# 8. Health Checks (NF-1.2)

## Readiness Requirements

- Redpanda connectivity (consumer can connect)
- RavenDB connectivity (can execute queries)
- MinIO connectivity (can list buckets)
- ffprobe available (`which ffprobe` succeeds)
- ffmpeg available (`which ffmpeg` succeeds)
- Temp directory writable

## Health Endpoint

```
GET /health      → 200 OK if all checks pass
GET /health/live → 200 OK if process is running
```
